using System.Globalization;
using System.Text.Json;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <inheritdoc cref="IFirmAgentToolExecutor"/>
public sealed class FirmAgentToolExecutor : IFirmAgentToolExecutor
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IFirmPortfolioReadService _portfolio;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly MasterDbContext _masterContext;
    private readonly IEmailService _emailService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmAgentToolExecutor> _logger;

    public FirmAgentToolExecutor(
        IFirmPortfolioReadService portfolio,
        IFirmDossierAccessService dossierAccess,
        ITenantService tenantService,
        ITenantDbContextFactory contextFactory,
        MasterDbContext masterContext,
        IEmailService emailService,
        ICurrentUser currentUser,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<FirmAgentToolExecutor> logger)
    {
        _portfolio = portfolio;
        _dossierAccess = dossierAccess;
        _tenantService = tenantService;
        _contextFactory = contextFactory;
        _masterContext = masterContext;
        _emailService = emailService;
        _currentUser = currentUser;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.FirmAgentEnabled)
            return AiToolResult.Error("L'agent Chef de mission n'est pas activé pour cette plateforme.");

        // Fail-closed délibéré : hors contexte cabinet identifié, on refuse au lieu de retomber sur
        // « aucun filtre ». Un outil IA ne doit jamais élargir un périmètre par défaut.
        if (_currentUser.TenantId is not { } firmTenantId || firmTenantId == Guid.Empty)
            return AiToolResult.Error("Contexte cabinet indisponible.");

        if (!_currentUser.TryGetAccessScope(out var scope))
            return AiToolResult.Error("Profil cabinet requis pour consulter le portefeuille.");

        try
        {
            return toolName switch
            {
                FirmAgentTools.PortfolioOverview => await HandleOverviewAsync(firmTenantId, scope, cancellationToken),
                FirmAgentTools.FiscalDeadlines => await HandleDeadlinesAsync(firmTenantId, scope, arguments, cancellationToken),
                FirmAgentTools.DossierHealth => await HandleDossierHealthAsync(firmTenantId, scope, arguments, cancellationToken),
                FirmAgentTools.CollaboratorWorkload => await HandleWorkloadAsync(firmTenantId, scope, cancellationToken),
                FirmAgentTools.SendReminder => await HandleSendReminderAsync(firmTenantId, scope, arguments, cancellationToken),
                _ => AiToolResult.Error($"Outil cabinet inconnu : {toolName}")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outil cabinet {ToolName} en échec", toolName);
            return AiToolResult.Error("L'analyse du portefeuille a échoué.");
        }
    }

    // ────────────────────────────── Lecture ──────────────────────────────

    private async Task<AiToolResult> HandleOverviewAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken)
    {
        var overview = await _portfolio.GetOverviewAsync(firmTenantId, scope, cancellationToken);

        return AiToolResult.Ok(Serialize(new
        {
            dossiersActifs = overview.ActiveDossiersCount,
            echeancesEnRetard = overview.OverdueCount,
            montantEnRetard = Amount(overview.OverdueEstimatedAmount),
            dossiersAvecRetard = overview.DossiersWithOverdueCount,
            echeancesSous7Jours = overview.UpcomingWithin7DaysCount,
            montantSous7Jours = Amount(overview.UpcomingWithin7DaysEstimatedAmount),
            echeancesAuDela7Jours = overview.UpcomingAfter7DaysCount,
            dossiersInactifs30Jours = overview.InactiveDossiers30DaysCount,
            declarationsTvaBrouillon = overview.VatDraftsCount,
            devise = overview.Currency,
            lectureIncomplete = FanOutNote(overview.FanOut)
        }));
    }

    private async Task<AiToolResult> HandleDeadlinesAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var companyFilter = GetString(args, "company_name");
        var query = new FirmDeadlineQuery
        {
            OnlyOverdue = GetBool(args, "only_overdue") ?? false,
            WithinDays = GetInt(args, "within_days"),
            ObligationType = ParseObligationType(GetString(args, "obligation_type")),
            TopN = GetInt(args, "top_n") ?? 20
        };

        var list = await _portfolio.GetDeadlinesAsync(firmTenantId, scope, query, cancellationToken);

        // Le filtre par nom de dossier est appliqué ici : le modèle raisonne en noms, pas en GUID.
        var items = list.Items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(companyFilter))
        {
            items = items.Where(i => i.CompanyName.Contains(companyFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var rows = items.Select(i => new
        {
            identifiant = i.Id,
            dossier = i.CompanyName,
            identifiantDossier = i.CompanyTenantId,
            obligation = i.ObligationLabel,
            echeance = i.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            joursRestants = i.DaysUntilDue,
            statut = i.StatusDisplay,
            montantEstime = Amount(i.EstimatedAmount),
            responsable = i.ResponsibleName ?? "Non affecté",
            dernierRappel = i.LastReminderAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        }).ToList();

        return AiToolResult.Ok(Serialize(new
        {
            echeances = rows,
            totalCorrespondant = list.TotalMatching,
            listeTronquee = list.TotalMatching > rows.Count,
            devise = list.Currency,
            lectureIncomplete = FanOutNote(list.FanOut)
        }));
    }

    private async Task<AiToolResult> HandleDossierHealthAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var topN = GetInt(args, "top_n") ?? 10;
        var health = await _portfolio.GetDossierHealthAsync(firmTenantId, scope, topN, cancellationToken);

        var rows = health.Items.Select(r => new
        {
            dossier = r.CompanyName,
            identifiantDossier = r.CompanyTenantId,
            scoreRisque = r.RiskScore,
            echeancesEnRetard = r.OverdueCount,
            montantEnRetard = Amount(r.OverdueEstimatedAmount),
            echeancesSous7Jours = r.UpcomingWithin7DaysCount,
            derniereEcriture = r.LastJournalEntryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            sansEcritureDepuis30Jours = r.IsInactive30Days,
            declarationsTvaBrouillon = r.VatDraftsCount,
            gestionnaire = r.AssignedAccountantName ?? "Non affecté",
            donneesIndisponibles = r.ReadFailed
        }).ToList();

        return AiToolResult.Ok(Serialize(new
        {
            dossiers = rows,
            totalDossiers = health.TotalDossiers,
            devise = health.Currency,
            lectureIncomplete = FanOutNote(health.FanOut)
        }));
    }

    private async Task<AiToolResult> HandleWorkloadAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken)
    {
        var workload = await _portfolio.GetCollaboratorWorkloadAsync(firmTenantId, scope, cancellationToken);

        var rows = workload.Items.Select(r => new
        {
            collaborateur = r.CollaboratorName,
            dossiersSuivis = r.DossiersCount,
            echeancesEnRetard = r.OverdueCount,
            echeancesSous7Jours = r.UpcomingWithin7DaysCount,
            montantEnRetard = Amount(r.OverdueEstimatedAmount)
        }).ToList();

        return AiToolResult.Ok(Serialize(new
        {
            collaborateurs = rows,
            echeancesSansResponsable = workload.UnassignedDeadlinesCount,
            devise = workload.Currency,
            lectureIncomplete = FanOutNote(workload.FanOut)
        }));
    }

    // ────────────────────────────── Relance ──────────────────────────────

    /// <summary>
    /// Seule mutation du scope. Trois gardes avant tout envoi : le drapeau dédié, l'ACL du dossier,
    /// et l'anti-doublon quotidien porté par <c>LastReminderAt</c> — la même règle que les rappels
    /// automatiques, pour qu'un rappel manuel et un rappel automatique ne se doublonnent pas.
    /// </summary>
    private async Task<AiToolResult> HandleSendReminderAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        if (!_options.FirmAgentReminderToolEnabled)
            return AiToolResult.Error("L'envoi de rappels par l'assistant est désactivé pour cette plateforme.");

        if (!Guid.TryParse(GetString(args, "deadline_id"), out var deadlineId) || deadlineId == Guid.Empty)
            return AiToolResult.Error("Identifiant d'échéance invalide.");

        if (!Guid.TryParse(GetString(args, "company_tenant_id"), out var companyTenantId) || companyTenantId == Guid.Empty)
            return AiToolResult.Error("Identifiant de dossier invalide.");

        if (!await _dossierAccess.CanAccessClientDossierAsync(firmTenantId, scope, companyTenantId, cancellationToken))
            return AiToolResult.Error("Vous n'êtes pas affecté à ce dossier client.");

        var connectionString = await _tenantService.GetConnectionStringAsync(companyTenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
            return AiToolResult.Error("Dossier client indisponible.");

        await using var ctx = _contextFactory.CreateIsolatedContext(connectionString);

        var entry = await ctx.FiscalScheduleEntries
            .FirstOrDefaultAsync(e => e.Id == deadlineId, cancellationToken);

        if (entry is null)
            return AiToolResult.Error("Échéance introuvable dans ce dossier.");

        if (entry.IsCancelled)
            return AiToolResult.Error("Cette échéance est annulée : aucun rappel n'est envoyé.");

        if (entry.ResponsibleUserId is not { } responsibleUserId)
            return AiToolResult.Error("Cette échéance n'a pas de responsable désigné : affectez-en un avant de relancer.");

        var today = _timeProvider.GetLocalNow().DateTime.Date;
        if (entry.LastReminderAt?.Date == today)
            return AiToolResult.Error("Un rappel a déjà été envoyé aujourd'hui pour cette échéance.");

        var responsible = await _masterContext.Users.AsNoTracking()
            .Where(u => u.Id == responsibleUserId)
            .Select(u => new { u.Email, u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        if (responsible is null || string.IsNullOrWhiteSpace(responsible.Email))
            return AiToolResult.Error("Le responsable de cette échéance n'a pas d'adresse e-mail exploitable.");

        var companyName = await _masterContext.Tenants.AsNoTracking()
            .Where(t => t.Id == companyTenantId)
            .Select(t => t.CompanyName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Dossier client";

        var daysUntil = (int)(entry.DueDate.Date - today).TotalDays;
        await _emailService.SendEmailAsync(
            responsible.Email,
            BuildSubject(entry, daysUntil),
            BuildBody(entry, companyName, daysUntil),
            cancellationToken: cancellationToken);

        var mark = entry.MarkReminder(FiscalReminderChannel.Email, DateTime.UtcNow);
        if (mark.IsFailure)
            return AiToolResult.Error("Le rappel a été envoyé mais n'a pas pu être tracé.");

        entry.SetAuditInfo(_currentUser.Email ?? "assistant", true);
        ctx.FiscalScheduleHistoryEntries.Add(FiscalScheduleHistoryEntry.Create(
            entry.Id,
            "AssistantReminder",
            $"Rappel envoye a {responsible.Email} depuis l'assistant cabinet ({DescribeTiming(daysUntil)}).",
            null,
            null));

        await ctx.SaveChangesAsync(cancellationToken);

        var responsibleName = $"{responsible.FirstName} {responsible.LastName}".Trim();
        return AiToolResult.Ok(Serialize(new
        {
            envoye = true,
            dossier = companyName,
            obligation = entry.ObligationLabel,
            echeance = entry.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            destinataire = string.IsNullOrWhiteSpace(responsibleName) ? responsible.Email : responsibleName
        }));
    }

    private static string DescribeTiming(int daysUntil) => daysUntil switch
    {
        < 0 => $"en retard de {Math.Abs(daysUntil)} jour(s)",
        0 => "échéance aujourd'hui",
        _ => $"J-{daysUntil}"
    };

    private static string BuildSubject(FiscalScheduleEntry entry, int daysUntil)
    {
        var prefix = daysUntil < 0 ? "[EN RETARD] " : string.Empty;
        return $"{prefix}Rappel échéance fiscale — {entry.ObligationLabel} ({entry.DueDate:dd/MM/yyyy})";
    }

    private static string BuildBody(FiscalScheduleEntry entry, string companyName, int daysUntil) =>
        $"""
         <p>Bonjour,</p>
         <p>Rappel concernant une échéance fiscale du dossier <strong>{companyName}</strong> :</p>
         <ul>
           <li>Obligation : {entry.ObligationLabel}</li>
           <li>Échéance : {entry.DueDate:dd/MM/yyyy} ({DescribeTiming(daysUntil)})</li>
           <li>Montant estimé : {entry.EstimatedAmount.ToString("N3", CultureInfo.GetCultureInfo("fr-FR"))} {entry.Currency}</li>
         </ul>
         <p>— Rappel envoyé depuis l'assistant du cabinet</p>
         """;

    // ────────────────────────────── Utilitaires ──────────────────────────────

    /// <summary>
    /// Formulation explicite d'une lecture partielle, destinée à être verbalisée par le modèle.
    /// Renvoyer <c>null</c> quand tout va bien évite de polluer la fenêtre de contexte.
    /// </summary>
    private static string? FanOutNote(FirmFanOutHealthDto fanOut) =>
        fanOut.IsPartial
            ? $"{fanOut.DossiersFailed} dossier(s) sur {fanOut.DossiersFailed + fanOut.DossiersRead} n'ont pas pu être lus : les compteurs sont incomplets."
            : null;

    private static decimal Amount(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static FiscalObligationType? ParseObligationType(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Enum.TryParse<FiscalObligationType>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : null;

    private static string? GetString(Dictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;

    private static int? GetInt(Dictionary<string, object?> args, string key)
    {
        var raw = GetString(args, key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool? GetBool(Dictionary<string, object?> args, string key)
    {
        var raw = GetString(args, key);
        return bool.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string Serialize(object payload) => JsonSerializer.Serialize(payload, SerializeOptions);
}
