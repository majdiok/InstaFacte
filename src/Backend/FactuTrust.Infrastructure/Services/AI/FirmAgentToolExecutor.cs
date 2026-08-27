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

    /// <summary>Durée de vie d'une action de relance en attente de confirmation.</summary>
    private static readonly TimeSpan PendingReminderTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fuseau métier unique pour la fenêtre anti-doublon (l. 4.1 du plan) : constante centralisée,
    /// jamais <c>_timeProvider.GetLocalNow()</c> dont le fuseau dépend de l'hôte. Même filet de repli
    /// multi-plateforme que <c>ReportingPeriodResolver</c>/<c>TunisianCalendarService</c>.
    /// </summary>
    private static readonly TimeZoneInfo TunisTimeZone = ResolveTunisTimeZone();

    private static readonly System.Globalization.CultureInfo FrenchDisplayCulture =
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    private static readonly NumberFormatInfo FrenchAmountFormat = BuildFrenchAmountFormat();

    private readonly IFirmPortfolioReadService _portfolio;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly MasterDbContext _masterContext;
    private readonly IEmailService _emailService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IFirmReminderPendingActionStore _pendingActionStore;
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
        IFirmReminderPendingActionStore pendingActionStore,
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
        _pendingActionStore = pendingActionStore;
        _logger = logger;
    }

    public async Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        AiToolExecutionContext? context = null,
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
                FirmAgentTools.SendReminder => await HandleSendReminderAsync(firmTenantId, scope, arguments, context, cancellationToken),
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

    /// <inheritdoc cref="IFirmAgentToolExecutor.ConfirmReminderAsync"/>
    public async Task<AiToolResult> ConfirmReminderAsync(string nonce, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.FirmAgentEnabled)
            return AiToolResult.Error("L'agent Chef de mission n'est pas activé pour cette plateforme.");

        if (!_options.FirmAgentReminderToolEnabled)
            return AiToolResult.Error("L'envoi de rappels par l'assistant est désactivé pour cette plateforme.");

        // Mêmes gardes fail-closed que ExecuteAsync : l'endpoint de confirmation n'est pas exempté.
        if (_currentUser.TenantId is not { } firmTenantId || firmTenantId == Guid.Empty)
            return AiToolResult.Error("Contexte cabinet indisponible.");

        if (!_currentUser.TryGetAccessScope(out var scope))
            return AiToolResult.Error("Profil cabinet requis pour consulter le portefeuille.");

        // Vérification déléguée firm:ai:remind : l'endpoint de confirmation ne passe pas par
        // AiToolExecutor.AuthorizeTool (qui la fait pour le chemin outil), il faut donc la refaire ici.
        if (_currentUser.UserId is not { } callerUserId || !_currentUser.HasPermission(Permissions.Firm.AiRemind))
            return AiToolResult.Error("Vous n'êtes pas autorisé à confirmer une relance.");

        try
        {
            if (!_pendingActionStore.TryConsume(nonce, callerUserId, out var pending) || pending is null)
                return AiToolResult.Error(
                    "Cette confirmation n'est plus valide : elle a expiré, a déjà été traitée, ou ne vous concerne pas.");

            var (error, target) = await LoadReminderTargetAsync(
                firmTenantId, scope, pending.DeadlineId, pending.CompanyTenantId, cancellationToken);
            if (error is not null)
                return error;

            await using var ctx = target!.Context;
            return await SendReminderNowAsync(target, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confirmation de relance cabinet en échec");
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
            montantEnRetardAffichage = AmountDisplay(overview.OverdueEstimatedAmount, overview.Currency),
            dossiersAvecRetard = overview.DossiersWithOverdueCount,
            echeancesSous7Jours = overview.UpcomingWithin7DaysCount,
            montantSous7Jours = Amount(overview.UpcomingWithin7DaysEstimatedAmount),
            montantSous7JoursAffichage = AmountDisplay(overview.UpcomingWithin7DaysEstimatedAmount, overview.Currency),
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
            montantEstimeAffichage = AmountDisplay(i.EstimatedAmount, list.Currency),
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
            montantEnRetardAffichage = AmountDisplay(r.OverdueEstimatedAmount, health.Currency),
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
            montantEnRetard = Amount(r.OverdueEstimatedAmount),
            montantEnRetardAffichage = AmountDisplay(r.OverdueEstimatedAmount, workload.Currency)
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

    /// <summary>Résultat de chargement d'une cible de relance : contexte tenant isolé + données déjà validées.</summary>
    private sealed record ReminderTarget(
        TenantDbContext Context,
        FiscalScheduleEntry Entry,
        string CompanyName,
        string ResponsibleEmail,
        string ResponsibleName);

    /// <summary>
    /// Seule mutation du scope. Gardes avant tout envoi : le drapeau dédié (appelant), l'ACL du
    /// dossier, l'échéance non annulée, un responsable désigné, et l'anti-doublon quotidien porté
    /// par <c>LastReminderAt</c> — la même règle que les rappels automatiques, pour qu'un rappel
    /// manuel et un rappel automatique ne se doublonnent pas. Partagé entre le chemin outil (flag
    /// off) et l'endpoint de confirmation (<see cref="ConfirmReminderAsync"/>) : ni l'un ni l'autre
    /// ne doit pouvoir contourner ces gardes.
    /// </summary>
    private async Task<(AiToolResult? Error, ReminderTarget? Target)> LoadReminderTargetAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Guid deadlineId,
        Guid companyTenantId,
        CancellationToken cancellationToken)
    {
        if (!await _dossierAccess.CanAccessClientDossierAsync(firmTenantId, scope, companyTenantId, cancellationToken))
            return (AiToolResult.Error("Vous n'êtes pas affecté à ce dossier client."), null);

        var connectionString = await _tenantService.GetConnectionStringAsync(companyTenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
            return (AiToolResult.Error("Dossier client indisponible."), null);

        var ctx = _contextFactory.CreateIsolatedContext(connectionString);

        var entry = await ctx.FiscalScheduleEntries
            .FirstOrDefaultAsync(e => e.Id == deadlineId, cancellationToken);

        if (entry is null)
        {
            await ctx.DisposeAsync();
            return (AiToolResult.Error("Échéance introuvable dans ce dossier."), null);
        }

        if (entry.IsCancelled)
        {
            await ctx.DisposeAsync();
            return (AiToolResult.Error("Cette échéance est annulée : aucun rappel n'est envoyé."), null);
        }

        if (entry.ResponsibleUserId is not { } responsibleUserId)
        {
            await ctx.DisposeAsync();
            return (AiToolResult.Error("Cette échéance n'a pas de responsable désigné : affectez-en un avant de relancer."), null);
        }

        if (IsSameTunisBusinessDay(entry.LastReminderAt))
        {
            await ctx.DisposeAsync();
            return (AiToolResult.Error("Un rappel a déjà été envoyé aujourd'hui pour cette échéance."), null);
        }

        var responsible = await _masterContext.Users.AsNoTracking()
            .Where(u => u.Id == responsibleUserId)
            .Select(u => new { u.Email, u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        if (responsible is null || string.IsNullOrWhiteSpace(responsible.Email))
        {
            await ctx.DisposeAsync();
            return (AiToolResult.Error("Le responsable de cette échéance n'a pas d'adresse e-mail exploitable."), null);
        }

        var companyName = await _masterContext.Tenants.AsNoTracking()
            .Where(t => t.Id == companyTenantId)
            .Select(t => t.CompanyName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Dossier client";

        var responsibleName = $"{responsible.FirstName} {responsible.LastName}".Trim();

        return (null, new ReminderTarget(ctx, entry, companyName, responsible.Email, responsibleName));
    }

    private async Task<AiToolResult> HandleSendReminderAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Dictionary<string, object?> args,
        AiToolExecutionContext? context,
        CancellationToken cancellationToken)
    {
        if (!_options.FirmAgentReminderToolEnabled)
            return AiToolResult.Error("L'envoi de rappels par l'assistant est désactivé pour cette plateforme.");

        if (!Guid.TryParse(GetString(args, "deadline_id"), out var deadlineId) || deadlineId == Guid.Empty)
            return AiToolResult.Error("Identifiant d'échéance invalide.");

        if (!Guid.TryParse(GetString(args, "company_tenant_id"), out var companyTenantId) || companyTenantId == Guid.Empty)
            return AiToolResult.Error("Identifiant de dossier invalide.");

        var (error, target) = await LoadReminderTargetAsync(firmTenantId, scope, deadlineId, companyTenantId, cancellationToken);
        if (error is not null)
            return error;

        await using var ctx = target!.Context;

        if (!_options.FirmAgentReminderRequiresConfirmation)
            return await SendReminderNowAsync(target, cancellationToken);

        // Flag actif (défaut) : jamais d'envoi depuis la boucle LLM. On renvoie une PREVIEW +
        // une action en attente que seul l'endpoint de confirmation peut consommer.
        if (_currentUser.UserId is not { } callerUserId)
            return AiToolResult.Error("Utilisateur non identifié.");

        var pending = _pendingActionStore.Create(
            callerUserId,
            context?.ConversationId,
            deadlineId,
            companyTenantId,
            PendingReminderTtl);

        var daysUntil = (int)(target.Entry.DueDate.Date - GetTunisToday()).TotalDays;
        var echeance = target.Entry.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var objet = BuildSubject(target.Entry, daysUntil);
        var displayName = string.IsNullOrWhiteSpace(target.ResponsibleName) ? target.ResponsibleEmail : target.ResponsibleName;

        return AiToolResult.Ok(Serialize(new
        {
            enAttenteConfirmation = true,
            dossier = target.CompanyName,
            obligation = target.Entry.ObligationLabel,
            echeance,
            destinataire = displayName,
            actionEnAttente = new
            {
                kind = "confirm_firm_reminder",
                label = "Confirmer l'envoi de la relance",
                nonce = pending.Nonce,
                expiresAtUtc = pending.ExpiresAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                preview = new
                {
                    responsable = displayName,
                    dossier = target.CompanyName,
                    echeance,
                    objet
                }
            }
        }));
    }

    /// <summary>
    /// Envoi réel, partagé entre le chemin outil (flag désactivé) et
    /// <see cref="ConfirmReminderAsync"/> (flag activé, après consommation du nonce). N'effectue
    /// aucune revalidation : appelant responsable d'avoir chargé <paramref name="target"/> via
    /// <see cref="LoadReminderTargetAsync"/> juste avant.
    /// </summary>
    private async Task<AiToolResult> SendReminderNowAsync(ReminderTarget target, CancellationToken cancellationToken)
    {
        var daysUntil = (int)(target.Entry.DueDate.Date - GetTunisToday()).TotalDays;

        await _emailService.SendEmailAsync(
            target.ResponsibleEmail,
            BuildSubject(target.Entry, daysUntil),
            BuildBody(target.Entry, target.CompanyName, daysUntil),
            cancellationToken: cancellationToken);

        var mark = target.Entry.MarkReminder(FiscalReminderChannel.Email, _timeProvider.GetUtcNow().UtcDateTime);
        if (mark.IsFailure)
            return AiToolResult.Error("Le rappel a été envoyé mais n'a pas pu être tracé.");

        target.Entry.SetAuditInfo(_currentUser.Email ?? "assistant", true);
        target.Context.FiscalScheduleHistoryEntries.Add(FiscalScheduleHistoryEntry.Create(
            target.Entry.Id,
            "AssistantReminder",
            $"Rappel envoye a {target.ResponsibleEmail} depuis l'assistant cabinet ({DescribeTiming(daysUntil)}).",
            null,
            null));

        await target.Context.SaveChangesAsync(cancellationToken);

        var displayName = string.IsNullOrWhiteSpace(target.ResponsibleName) ? target.ResponsibleEmail : target.ResponsibleName;
        return AiToolResult.Ok(Serialize(new
        {
            envoye = true,
            dossier = target.CompanyName,
            obligation = target.Entry.ObligationLabel,
            echeance = target.Entry.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            destinataire = displayName
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
           <li>Montant estimé : {entry.EstimatedAmount.ToString("N3", FrenchDisplayCulture)} {entry.Currency}</li>
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

    /// <summary>
    /// Rendu français prêt à afficher (« 12 345,678 TND », espace insécable de regroupement) posé à
    /// côté du champ brut correspondant — le brut reste la source de vérité pour le LLM et les
    /// exports, l'affichage évite au modèle de reformater lui-même (source d'incohérences).
    /// </summary>
    private static string AmountDisplay(decimal value, string currency) =>
        $"{Amount(value).ToString("N3", FrenchAmountFormat)}\u00A0{currency}";

    /// <summary>Journée métier Tunis courante, dérivée explicitement de l'instant UTC du <see cref="TimeProvider"/>.</summary>
    private DateTime GetTunisToday() =>
        TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, TunisTimeZone).Date;

    /// <summary>
    /// Anti-doublon quotidien (4.1) : compare deux instants UTC après conversion explicite vers la
    /// journée métier Tunis, jamais via <c>GetLocalNow()</c> dont le fuseau dépend de l'hôte.
    /// </summary>
    private bool IsSameTunisBusinessDay(DateTime? lastReminderAtUtc)
    {
        if (lastReminderAtUtc is not { } last)
            return false;

        var lastUtc = DateTime.SpecifyKind(last, DateTimeKind.Utc);
        var lastTunisDay = TimeZoneInfo.ConvertTimeFromUtc(lastUtc, TunisTimeZone).Date;
        return lastTunisDay == GetTunisToday();
    }

    private static TimeZoneInfo ResolveTunisTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
    }

    private static NumberFormatInfo BuildFrenchAmountFormat()
    {
        // fr-FR .NET utilise l'espace fine (U+2009) comme séparateur de milliers ; le rendu attendu
        // côté client est l'espace insécable (U+00A0) — cf. plan Lot 4.2.
        var format = (NumberFormatInfo)FrenchDisplayCulture.NumberFormat.Clone();
        format.NumberGroupSeparator = "\u00A0";
        return format;
    }

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
