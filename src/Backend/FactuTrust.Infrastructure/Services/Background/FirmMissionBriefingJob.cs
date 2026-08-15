using System.Globalization;
using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Brief quotidien de l'agent « Chef de mission » : envoie au responsable de cabinet l'état
/// priorisé de son portefeuille (retards fiscaux, dossiers à risque, charge des collaborateurs).
/// </summary>
/// <remarks>
/// <para>
/// Calqué sur <see cref="FiscalReminderJob"/> : drapeau d'abord avec sortie anticipée, énumération
/// master, isolation des échecs par entité, log de synthèse final.
/// </para>
/// <para>
/// <b>Périmètre.</b> Le brief est construit avec un périmètre d'accès EXPLICITE. Hors requête HTTP,
/// <c>ICurrentUser</c> est vide et l'idiome fail-closed employé ailleurs retomberait sur « aucun
/// filtre » : <see cref="IFirmPortfolioReadService"/> n'y touche pas et exige le scope en paramètre.
/// Les destinataires sont les responsables de cabinet, dont le périmètre est par définition le
/// portefeuille complet du cabinet — d'où <c>scope: null</c>, choisi et non subi.
/// </para>
/// <para>
/// <b>Anti-doublon.</b> Porté par l'index unique de <see cref="FirmMissionBriefingLog"/>, pas par ce
/// code : un redéclenchement Hangfire échoue à l'insertion au lieu d'envoyer un second e-mail.
/// </para>
/// </remarks>
public sealed class FirmMissionBriefingJob
{
    private readonly MasterDbContext _master;
    private readonly IFirmPortfolioReadService _portfolio;
    private readonly IEmailService _emailService;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmMissionBriefingJob> _logger;

    public FirmMissionBriefingJob(
        MasterDbContext master,
        IFirmPortfolioReadService portfolio,
        IEmailService emailService,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<FirmMissionBriefingJob> logger)
    {
        _master = master;
        _portfolio = portfolio;
        _emailService = emailService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.FirmAgentEnabled || !_options.FirmAgentDailyBriefingEnabled)
        {
            _logger.LogInformation("FirmMissionBriefingJob : désactivé (drapeaux cabinet) — aucun envoi.");
            return;
        }

        var today = _timeProvider.GetLocalNow().DateTime.Date;

        var firms = await _master.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.Kind == TenantKind.AccountingFirm)
            .Select(t => new { t.Id, t.CompanyName })
            .ToListAsync(cancellationToken);

        var sent = 0;
        var firmsProcessed = 0;

        foreach (var firm in firms)
        {
            try
            {
                sent += await ProcessFirmAsync(firm.Id, firm.CompanyName, today, cancellationToken);
                firmsProcessed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Un cabinet en échec ne doit jamais interrompre la tournée des autres.
                _logger.LogError(ex, "Brief Chef de mission en échec pour le cabinet {FirmTenantId}", firm.Id);
            }
        }

        _logger.LogInformation(
            "FirmMissionBriefingJob terminé : {Sent} brief(s) envoyé(s) sur {Firms} cabinet(s).",
            sent, firmsProcessed);
    }

    private async Task<int> ProcessFirmAsync(
        Guid firmTenantId,
        string firmName,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var recipients = await GetFirmManagersAsync(firmTenantId, cancellationToken);
        if (recipients.Count == 0)
            return 0;

        var alreadySent = await _master.FirmMissionBriefingLogs.AsNoTracking()
            .Where(l => l.FirmTenantId == firmTenantId && l.BriefingDate == today)
            .Select(l => l.RecipientUserId)
            .ToListAsync(cancellationToken);

        var pending = recipients.Where(r => !alreadySent.Contains(r.Id)).ToList();
        if (pending.Count == 0)
            return 0;

        // Le portefeuille est lu UNE fois par cabinet : tous les responsables ont le même périmètre.
        var overview = await _portfolio.GetOverviewAsync(firmTenantId, scope: null, cancellationToken);
        var health = await _portfolio.GetDossierHealthAsync(firmTenantId, scope: null, topN: 5, cancellationToken);
        var workload = await _portfolio.GetCollaboratorWorkloadAsync(firmTenantId, scope: null, cancellationToken);

        if (overview.ActiveDossiersCount == 0)
        {
            _logger.LogDebug("Cabinet {FirmTenantId} sans dossier actif : brief ignoré.", firmTenantId);
            return 0;
        }

        var subject = BuildSubject(firmName, overview, today);
        var body = BuildBody(firmName, overview, health, workload, today);

        var sent = 0;
        foreach (var recipient in pending)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email))
                continue;

            try
            {
                await _emailService.SendEmailAsync(recipient.Email, subject, body, cancellationToken: cancellationToken);

                _master.FirmMissionBriefingLogs.Add(FirmMissionBriefingLog.Create(
                    firmTenantId,
                    recipient.Id,
                    today,
                    DateTime.UtcNow,
                    overview.OverdueCount,
                    overview.FanOut.IsPartial));

                await _master.SaveChangesAsync(cancellationToken);
                sent++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbUpdateException ex)
            {
                // Violation probable de l'index unique : un autre déclenchement a déjà envoyé ce
                // brief. Ce n'est pas une anomalie — c'est l'anti-doublon qui fait son office.
                _logger.LogWarning(ex,
                    "Brief déjà tracé pour {RecipientUserId} du cabinet {FirmTenantId} : trace ignorée.",
                    recipient.Id, firmTenantId);
                _master.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Envoi du brief en échec pour {RecipientUserId} du cabinet {FirmTenantId}",
                    recipient.Id, firmTenantId);
                _master.ChangeTracker.Clear();
            }
        }

        return sent;
    }

    /// <summary>
    /// Responsables de cabinet actifs. Le rôle vit dans les tables Identity, d'où la jointure
    /// explicite plutôt qu'une colonne sur l'utilisateur.
    /// </summary>
    private async Task<IReadOnlyList<BriefingRecipient>> GetFirmManagersAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken)
    {
        var managerRole = nameof(UserRole.FirmManager);

        return await (
            from user in _master.Users.AsNoTracking()
            join userRole in _master.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _master.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.TenantId == firmTenantId
                  && user.IsActive
                  && role.Name == managerRole
            select new BriefingRecipient(user.Id, user.Email, user.FirstName))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    // ────────────────────────────── Rédaction (déterministe) ──────────────────────────────

    private static string BuildSubject(string firmName, FirmPortfolioOverviewDto overview, DateTime today)
    {
        var prefix = overview.OverdueCount > 0 ? $"[{overview.OverdueCount} en retard] " : string.Empty;
        return $"{prefix}Brief cabinet — {firmName} ({today:dd/MM/yyyy})";
    }

    /// <summary>
    /// Contenu entièrement déterministe : le brief doit partir même si le service IA est indisponible.
    /// C'est la même philosophie que le WhatsApp best-effort des rappels fiscaux — la valeur d'usage
    /// ne dépend jamais d'un composant faillible.
    /// </summary>
    private static string BuildBody(
        string firmName,
        FirmPortfolioOverviewDto overview,
        FirmDossierHealthListDto health,
        FirmCollaboratorWorkloadDto workload,
        DateTime today)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<p>Bonjour,</p><p>Brief du {today:dd/MM/yyyy} pour <strong>{firmName}</strong>.</p>");

        sb.Append("<h3>Portefeuille</h3><ul>");
        sb.Append(CultureInfo.InvariantCulture, $"<li>Dossiers actifs : {overview.ActiveDossiersCount}</li>");
        sb.Append(CultureInfo.InvariantCulture,
            $"<li>Échéances en retard : <strong>{overview.OverdueCount}</strong> sur {overview.DossiersWithOverdueCount} dossier(s) — {Money(overview.OverdueEstimatedAmount)} {overview.Currency}</li>");
        sb.Append(CultureInfo.InvariantCulture,
            $"<li>Échéances sous 7 jours : {overview.UpcomingWithin7DaysCount} — {Money(overview.UpcomingWithin7DaysEstimatedAmount)} {overview.Currency}</li>");
        sb.Append(CultureInfo.InvariantCulture, $"<li>Dossiers sans écriture depuis 30 jours : {overview.InactiveDossiers30DaysCount}</li>");
        sb.Append(CultureInfo.InvariantCulture, $"<li>Déclarations TVA en brouillon : {overview.VatDraftsCount}</li>");
        sb.Append("</ul>");

        var atRisk = health.Items.Where(r => !r.ReadFailed && r.RiskScore > 0).ToList();
        if (atRisk.Count > 0)
        {
            sb.Append("<h3>Dossiers à surveiller</h3><ul>");
            foreach (var row in atRisk)
            {
                var details = new List<string>();
                if (row.OverdueCount > 0)
                    details.Add($"{row.OverdueCount} échéance(s) en retard");
                if (row.IsInactive30Days)
                    details.Add("aucune écriture depuis 30 jours");
                if (row.VatDraftsCount > 0)
                    details.Add($"{row.VatDraftsCount} TVA en brouillon");

                sb.Append(CultureInfo.InvariantCulture,
                    $"<li><strong>{row.CompanyName}</strong> — {string.Join(", ", details)} (gestionnaire : {row.AssignedAccountantName ?? "non affecté"})</li>");
            }
            sb.Append("</ul>");
        }

        var loaded = workload.Items.Where(w => w.OverdueCount > 0).Take(5).ToList();
        if (loaded.Count > 0)
        {
            sb.Append("<h3>Charge des collaborateurs</h3><ul>");
            foreach (var row in loaded)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"<li>{row.CollaboratorName} — {row.OverdueCount} en retard, {row.UpcomingWithin7DaysCount} sous 7 jours, {row.DossiersCount} dossier(s)</li>");
            }
            sb.Append("</ul>");
        }

        if (workload.UnassignedDeadlinesCount > 0)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"<p><strong>Attention :</strong> {workload.UnassignedDeadlinesCount} échéance(s) sans responsable désigné.</p>");
        }

        // Une lecture partielle est dite explicitement : un compteur incomplet présenté comme
        // complet est pire qu'un compteur absent.
        if (overview.FanOut.IsPartial)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"<p><em>Lecture incomplète : {overview.FanOut.DossiersFailed} dossier(s) n'ont pas pu être consultés. Les chiffres ci-dessus sont partiels.</em></p>");
        }

        sb.Append("<p>— Brief automatique du cabinet</p>");
        return sb.ToString();
    }

    private static string Money(decimal value) =>
        value.ToString("N3", CultureInfo.GetCultureInfo("fr-FR"));

    private sealed record BriefingRecipient(Guid Id, string? Email, string? FirstName);
}
