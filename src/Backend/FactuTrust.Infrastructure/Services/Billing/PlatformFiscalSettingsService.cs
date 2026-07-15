using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C4 — Lecture / mise à jour des paramètres fiscaux singleton plateforme.
///
/// La table <c>PlatformFiscalSettings</c> contient une seule ligne (créée à la
/// volée la première fois). Toute mutation génère un <c>AuditLog</c> côté
/// orchestration (à brancher dans le contrôleur).
/// </summary>
public sealed class PlatformFiscalSettingsService : IPlatformFiscalSettingsService
{
    private readonly MasterDbContext _db;

    public PlatformFiscalSettingsService(MasterDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformFiscalSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var current = await _db.PlatformFiscalSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            current = PlatformFiscalSettings.CreateDefaults();
            _db.PlatformFiscalSettings.Add(current);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return Map(current);
    }

    public async Task<Result<PlatformFiscalSettingsDto>> UpdateAsync(
        UpdatePlatformFiscalSettingsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<PlatformFiscalSettingsDto>(new Error("FiscalSettings.InvalidRequest", "Requête invalide"));

        var current = await _db.PlatformFiscalSettings.FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            current = PlatformFiscalSettings.CreateDefaults();
            _db.PlatformFiscalSettings.Add(current);
        }

        try
        {
            current.Update(
                nif: request.Nif,
                codeTva: request.CodeTva,
                companyName: request.CompanyName,
                address: request.Address,
                phone: request.Phone,
                email: request.Email,
                website: request.Website,
                iban: request.Iban,
                bankName: request.BankName,
                applyVat: request.ApplyVat,
                defaultVatRate: request.DefaultVatRate,
                timbreFiscalAmount: request.TimbreFiscalAmount,
                applyClientWithholding: request.ApplyClientWithholding,
                clientWithholdingRate: request.ClientWithholdingRate,
                invoiceNumberPrefix: request.InvoiceNumberPrefix,
                receiptNumberPrefix: request.ReceiptNumberPrefix,
                legalMentions: request.LegalMentions);

            current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(current));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PlatformFiscalSettingsDto>(new Error("FiscalSettings.Invalid", ex.Message));
        }
    }

    private static PlatformFiscalSettingsDto Map(PlatformFiscalSettings p) => new()
    {
        Id = p.Id,
        Nif = p.Nif,
        CodeTva = p.CodeTva,
        CompanyName = p.CompanyName,
        Address = p.Address,
        Phone = p.Phone,
        Email = p.Email,
        Website = p.Website,
        Iban = p.Iban,
        BankName = p.BankName,
        ApplyVat = p.ApplyVat,
        DefaultVatRate = p.DefaultVatRate,
        TimbreFiscalAmount = p.TimbreFiscalAmount,
        ApplyClientWithholding = p.ApplyClientWithholding,
        ClientWithholdingRate = p.ClientWithholdingRate,
        InvoiceNumberPrefix = p.InvoiceNumberPrefix,
        ReceiptNumberPrefix = p.ReceiptNumberPrefix,
        LegalMentions = p.LegalMentions,
        UpdatedAt = p.UpdatedAt
    };
}
