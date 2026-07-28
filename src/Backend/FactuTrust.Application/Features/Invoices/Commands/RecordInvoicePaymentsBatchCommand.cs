using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.Invoices.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Enregistre PLUSIEURS règlements sur une même facture, de façon atomique.
///
/// Motivation : le point de vente permet de ventiler un encaissement entre plusieurs modes
/// (espèces + carte + chèque), mais ne créait qu'une seule ligne Payment portant le premier
/// mode, la ventilation réelle finissant concaténée dans le champ texte PaymentTerms.
/// Rapprochement bancaire faussé, comptage de caisse invérifiable, ventilation comptable
/// impossible et statistiques par mode de règlement fausses.
///
/// Chaque mode donne désormais lieu à une ligne Payment distincte. Les N lignes et le statut
/// de la facture sont écrits dans UNE seule transaction : un échec sur le dernier règlement
/// n'en laisse aucun derrière lui.
/// </summary>
public sealed record RecordInvoicePaymentsBatchCommand(
    Guid InvoiceId,
    IReadOnlyList<RecordInvoicePaymentRequest> Payments) : IRequest<Result>;

/// <summary>
/// Handler for <see cref="RecordInvoicePaymentsBatchCommand"/>.
/// </summary>
public sealed class RecordInvoicePaymentsBatchCommandHandler
    : IRequestHandler<RecordInvoicePaymentsBatchCommand, Result>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly AccountingSettings _accountingSettings;

    public RecordInvoicePaymentsBatchCommandHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IOptions<AccountingSettings> accountingSettings)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result> Handle(RecordInvoicePaymentsBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.Payments is null || request.Payments.Count == 0)
            return Result.Failure(Error.Validation("Payments", "Au moins un règlement est requis"));

        var userId = _currentUser.UserId?.ToString() ?? "system";
        var recordedPaymentIds = new List<Guid>();
        var auditEntries = new List<object>();

        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, ct);

            if (invoice is null)
                return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

            if (!invoice.Status.CanBePaymentRecorded())
                return Result.Failure(Error.Validation("Status", "Cette facture ne peut pas recevoir de paiement dans son état actuel"));

            var existingPayments = await _paymentRepository.GetByInvoiceIdAsync(request.InvoiceId, ct);
            var totalApplied = existingPayments
                .Where(p => !p.IsRefunded)
                .Sum(p => p.GetTotalAppliedTowardInvoice());

            var paymentDates = existingPayments.Select(p => p.PaymentDate).ToList();

            foreach (var line in request.Payments)
            {
                var method = line.Method ?? PaymentMethod.Other;

                if (method == PaymentMethod.Traite && !_accountingSettings.EffetDeCommerceEnabled)
                    return Result.Failure(Error.Validation("Method", "Le paiement par traite n'est pas activé"));

                var withholding = line.ClientWithholdingAmount ?? 0m;

                // Le total déjà imputé inclut les règlements du lot déjà résolus : le restant dû
                // décroît règlement après règlement, exactement comme des appels successifs.
                var resolved = InvoicePaymentCalculator.Resolve(invoice, totalApplied, line.Amount, withholding);
                if (resolved.IsFailure)
                    return Result.Failure(resolved.Error);

                var (netReceived, appliedTowardInvoice) = resolved.Value;

                var paymentResult = Payment.Create(
                    invoice,
                    Money.Create(netReceived, invoice.TotalAmount.Currency),
                    line.PaymentDate,
                    method,
                    line.Reference,
                    line.Notes,
                    withholding > 0 ? withholding : null,
                    line.EffetDueDate);

                if (paymentResult.IsFailure)
                    return Result.Failure(paymentResult.Error);

                var payment = paymentResult.Value;
                payment.SetAuditInfo(userId, isUpdate: false);

                await _paymentRepository.AddAsync(payment, ct);

                totalApplied += appliedTowardInvoice;
                paymentDates.Add(line.PaymentDate);
                recordedPaymentIds.Add(payment.Id);
                auditEntries.Add(new { invoice.Number.Value, netReceived, withholding, line.PaymentDate, method });
            }

            // Statut réconcilié une seule fois, sur le total final : réconcilier à chaque
            // itération émettrait plusieurs fois l'événement « facture payée ».
            invoice.ReconcilePaymentStatus(totalApplied, paymentDates.Max());
            invoice.SetAuditInfo(userId, isUpdate: true);

            await _invoiceRepository.UpdateAsync(invoice, ct);

            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        // Audit et notifications hors transaction — l'audit est hash-chaîné dans sa propre
        // transaction, et les écritures comptables conservent la sémantique du chemin unitaire.
        foreach (var entry in auditEntries)
        {
            await _auditService.LogAsync(
                AuditActions.Invoice.Paid,
                "Invoice",
                request.InvoiceId,
                newValues: entry,
                cancellationToken: cancellationToken);
        }

        foreach (var paymentId in recordedPaymentIds)
            await _publisher.Publish(new InvoicePaymentRecordedNotification(paymentId), cancellationToken);

        return Result.Success();
    }
}
