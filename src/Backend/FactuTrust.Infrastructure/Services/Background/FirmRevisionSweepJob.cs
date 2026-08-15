using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Balayage nocturne des portefeuilles cabinet : chaque dossier est contrôlé, de sorte que le chef
/// de mission trouve un état à jour au matin plutôt qu'un écran à rafraîchir.
/// </summary>
/// <remarks>
/// <para>
/// <b>Périmètre explicite.</b> Le job passe <c>scope: null</c> — le portefeuille complet du cabinet.
/// Hors requête HTTP, <c>ICurrentUser</c> est vide et l'idiome fail-closed employé dans les
/// contrôleurs retomberait sur « aucun filtre » <i>subi</i> plutôt que choisi. Le rendre explicite
/// évite qu'un futur changement d'ACL modifie silencieusement ce que le job balaie.
/// </para>
/// <para>
/// <b>Isolation des échecs.</b> Un cabinet en panne n'empêche pas les autres d'être traités, et un
/// dossier illisible n'interrompt pas le balayage de son cabinet — <c>IFirmRevisionService</c> s'en
/// charge et rend le compte des échecs.
/// </para>
/// </remarks>
public sealed class FirmRevisionSweepJob
{
    private readonly MasterDbContext _master;
    private readonly IFirmRevisionService _revision;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmRevisionSweepJob> _logger;

    public FirmRevisionSweepJob(
        MasterDbContext master,
        IFirmRevisionService revision,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<FirmRevisionSweepJob> logger)
    {
        _master = master;
        _revision = revision;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.FirmRevisionEnabled || !_options.FirmRevisionSweepEnabled)
        {
            _logger.LogInformation(
                "FirmRevisionSweepJob : désactivé (drapeaux cabinet) — aucun balayage lancé.");
            return;
        }

        var fiscalYear = _timeProvider.GetLocalNow().Year;

        var firmTenantIds = await _master.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.Kind == TenantKind.AccountingFirm)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (firmTenantIds.Count == 0)
        {
            _logger.LogInformation("FirmRevisionSweepJob : aucun cabinet actif.");
            return;
        }

        var firmsProcessed = 0;
        var firmsFailed = 0;
        var dossiersScanned = 0;
        var dossiersFailed = 0;

        foreach (var firmTenantId in firmTenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _revision.SweepAsync(
                    firmTenantId,
                    // Portefeuille complet : choix explicite, cf. remarques de classe.
                    scope: null,
                    fiscalYear,
                    triggeredByUserId: null,
                    triggeredByUserName: "balayage nocturne",
                    cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning(
                        "FirmRevisionSweepJob : échec sur le cabinet {FirmTenantId} — {Error}",
                        firmTenantId, result.Error.Description);
                    firmsFailed++;
                    continue;
                }

                firmsProcessed++;
                dossiersScanned += result.Value.DossiersScanned;
                dossiersFailed += result.Value.DossiersFailed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Un cabinet en panne ne prive pas les autres de leur balayage.
                _logger.LogError(ex,
                    "FirmRevisionSweepJob : exception sur le cabinet {FirmTenantId}", firmTenantId);
                firmsFailed++;
            }
        }

        _logger.LogInformation(
            "FirmRevisionSweepJob terminé : {Firms} cabinet(s) traité(s), {FirmsFailed} en échec, " +
            "{Dossiers} dossier(s) contrôlé(s), {DossiersFailed} dossier(s) illisible(s).",
            firmsProcessed, firmsFailed, dossiersScanned, dossiersFailed);
    }
}
