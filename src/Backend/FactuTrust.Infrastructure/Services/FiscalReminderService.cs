using System.Globalization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Rappels e-mail automatiques de l'échéancier fiscal (utilisé par <c>FiscalReminderJob</c>,
/// hors requête HTTP — travaille sur le <see cref="TenantDbContext"/> fourni).
///
/// Règles d'envoi (pour une échéance non annulée, ni déposée/payée/validée, avec responsable) :
/// <list type="bullet">
///   <item>échéance dans exactement N jours, N ∈ <see cref="AccountingSettings.FiscalReminderLeadDays"/> (défaut J-7, J-1) ;</item>
///   <item>ou en retard, avec relance au plus hebdomadaire (dernier rappel absent ou ≥ 7 jours) ;</item>
///   <item>anti-doublon : jamais deux rappels le même jour (<c>LastReminderAt</c>).</item>
/// </list>
/// Chaque envoi réutilise <see cref="FiscalScheduleEntry.MarkReminder"/> (trace + anti-doublon
/// naturel) et ajoute une entrée d'historique « AutoReminder ». Un seul SaveChanges par tenant.
/// </summary>
public sealed class FiscalReminderService
{
    private readonly IEmailService _emailService;
    private readonly IChannelOutboundSender _channelSender;
    private readonly AccountingSettings _settings;
    private readonly ILogger<FiscalReminderService> _logger;

    public FiscalReminderService(
        IEmailService emailService,
        IChannelOutboundSender channelSender,
        IOptions<AccountingSettings> settings,
        ILogger<FiscalReminderService> logger)
    {
        _emailService = emailService;
        _channelSender = channelSender;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <param name="usersById">E-mails/noms des responsables (résolus par le job depuis la base master).</param>
    /// <param name="companyName">Nom du dossier, affiché dans l'e-mail.</param>
    /// <param name="whatsAppChatsById">
    /// Discussions WhatsApp des responsables liés (résolues par le job quand
    /// <c>Channels.FiscalWhatsAppRemindersEnabled</c> est actif). Null = comportement historique
    /// e-mail seul, bit-à-bit identique. Le WhatsApp est additif et best-effort : son échec ne
    /// bloque jamais l'e-mail ni le marquage du rappel.
    /// </param>
    public async Task<int> ProcessTenantAsync(
        TenantDbContext ctx,
        IReadOnlyDictionary<Guid, (string Email, string Name)> usersById,
        string companyName,
        DateTime today,
        IReadOnlyDictionary<Guid, string>? whatsAppChatsById = null,
        CancellationToken cancellationToken = default)
    {
        var leadDays = _settings.FiscalReminderLeadDays is { Length: > 0 }
            ? _settings.FiscalReminderLeadDays
            : [7, 1];

        var candidates = await ctx.FiscalScheduleEntries
            .Where(e => !e.IsCancelled
                        && e.DepositDate == null
                        && e.PaymentDate == null
                        && e.ValidatedAt == null
                        && e.ResponsibleUserId != null)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var entry in candidates)
        {
            var daysUntil = (entry.DueDate.Date - today).Days;
            var isLeadDay = leadDays.Contains(daysUntil);
            var isOverdueWeekly = daysUntil < 0
                && (entry.LastReminderAt is null || entry.LastReminderAt.Value.Date <= today.AddDays(-7));
            if (!isLeadDay && !isOverdueWeekly)
                continue;

            // Anti-doublon : un seul rappel par échéance et par jour (manuel ou automatique).
            if (entry.LastReminderAt?.Date == today)
                continue;

            if (!usersById.TryGetValue(entry.ResponsibleUserId!.Value, out var responsible)
                || string.IsNullOrWhiteSpace(responsible.Email))
            {
                _logger.LogWarning(
                    "Fiscal reminder skipped for entry {EntryId}: responsible {UserId} has no email.",
                    entry.Id, entry.ResponsibleUserId);
                continue;
            }

            var subject = BuildSubject(entry, daysUntil);
            var body = BuildBody(entry, responsible.Name, companyName, daysUntil);
            await _emailService.SendEmailAsync(responsible.Email, subject, body, cancellationToken: cancellationToken);

            // Canal WhatsApp additif (jamais bloquant) : l'e-mail reste le canal primaire et porte
            // l'anti-doublon LastReminderAt, commun aux deux canaux.
            if (whatsAppChatsById is not null
                && whatsAppChatsById.TryGetValue(entry.ResponsibleUserId!.Value, out var chatId)
                && !string.IsNullOrWhiteSpace(chatId))
            {
                var delivered = await _channelSender.SendWhatsAppTextAsync(
                    chatId, BuildWhatsAppText(entry, companyName, daysUntil), cancellationToken);
                if (!delivered)
                {
                    _logger.LogWarning(
                        "Rappel fiscal WhatsApp non délivré pour l'échéance {EntryId} (e-mail envoyé normalement).",
                        entry.Id);
                }
            }

            var mark = entry.MarkReminder(FiscalReminderChannel.Email, DateTime.UtcNow);
            if (mark.IsFailure)
                continue;
            entry.SetAuditInfo("system", true);
            ctx.FiscalScheduleHistoryEntries.Add(FiscalScheduleHistoryEntry.Create(
                entry.Id,
                "AutoReminder",
                $"Rappel automatique envoye a {responsible.Email} ({DescribeTiming(daysUntil)}).",
                null,
                null));
            sent++;
        }

        if (sent > 0)
            await ctx.SaveChangesAsync(cancellationToken);

        return sent;
    }

    private static string DescribeTiming(int daysUntil) => daysUntil switch
    {
        < 0 => $"en retard de {-daysUntil} jour(s)",
        0 => "échéance aujourd'hui",
        _ => $"J-{daysUntil}"
    };

    private static string BuildWhatsAppText(FiscalScheduleEntry entry, string companyName, int daysUntil)
    {
        var amount = entry.EstimatedAmount.ToString("N3", CultureInfo.GetCultureInfo("fr-FR"));
        var prefix = daysUntil < 0 ? "🔴 *EN RETARD* — " : "🔔 ";
        return $"{prefix}*Rappel échéance fiscale* ({DescribeTiming(daysUntil)})\n" +
               $"Dossier : {companyName}\n" +
               $"Obligation : {entry.ObligationLabel} ({entry.FiscalYear})\n" +
               $"Échéance : {entry.DueDate:dd/MM/yyyy}\n" +
               $"Montant estimé : {amount} {entry.Currency}";
    }

    private static string BuildSubject(FiscalScheduleEntry entry, int daysUntil)
    {
        var prefix = daysUntil < 0 ? "[EN RETARD] " : string.Empty;
        return $"{prefix}Rappel échéance fiscale — {entry.ObligationLabel} ({entry.DueDate:dd/MM/yyyy})";
    }

    private static string BuildBody(FiscalScheduleEntry entry, string responsibleName, string companyName, int daysUntil)
    {
        var amount = entry.EstimatedAmount.ToString("N3", CultureInfo.GetCultureInfo("fr-FR"));
        var timing = DescribeTiming(daysUntil);
        return $"""
            <p>Bonjour {responsibleName},</p>
            <p>L'échéance fiscale suivante requiert votre attention (<strong>{timing}</strong>) :</p>
            <ul>
              <li><strong>Dossier :</strong> {companyName}</li>
              <li><strong>Obligation :</strong> {entry.ObligationLabel}</li>
              <li><strong>Exercice :</strong> {entry.FiscalYear}</li>
              <li><strong>Date d'échéance :</strong> {entry.DueDate:dd/MM/yyyy}</li>
              <li><strong>Montant estimé :</strong> {amount} {entry.Currency}</li>
            </ul>
            <p>Merci de procéder au dépôt/paiement puis de mettre à jour l'échéancier fiscal.</p>
            <p>— Rappel automatique InstaFact</p>
            """;
    }
}
