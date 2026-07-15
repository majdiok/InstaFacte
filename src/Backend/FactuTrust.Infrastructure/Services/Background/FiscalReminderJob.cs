using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire quotidien : envoie les rappels e-mail de l'échéancier fiscal pour TOUS les
/// tenants actifs (itération multi-tenant façon <see cref="RecurringEntriesJob"/> — MasterDbContext
/// pour la liste des tenants et la résolution des e-mails responsables, connexion tenant directe
/// pour le travail). Gardé par <see cref="AccountingSettings.FiscalEmailRemindersEnabled"/> ;
/// un tenant en échec n'empêche pas les autres ; l'anti-doublon est porté par
/// <see cref="FiscalReminderService"/> (LastReminderAt).
/// </summary>
public sealed class FiscalReminderJob
{
    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly FiscalReminderService _reminderService;
    private readonly AccountingSettings _settings;
    private readonly ChannelsSettings _channelsSettings;
    private readonly ILogger<FiscalReminderJob> _logger;

    public FiscalReminderJob(
        MasterDbContext master,
        ITenantService tenantService,
        FiscalReminderService reminderService,
        IOptions<AccountingSettings> settings,
        IOptions<ChannelsSettings> channelsSettings,
        ILogger<FiscalReminderJob> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _reminderService = reminderService;
        _settings = settings.Value;
        _channelsSettings = channelsSettings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.FiscalEmailRemindersEnabled)
        {
            _logger.LogInformation("FiscalReminderJob: disabled (FiscalEmailRemindersEnabled=false) — skipping.");
            return;
        }

        var today = DateTime.UtcNow.Date;
        var tenants = await _master.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, Name = t.CompanyName })
            .ToListAsync(cancellationToken);

        var totalSent = 0;
        foreach (var tenant in tenants)
        {
            try
            {
                var connectionString = await _tenantService.GetConnectionStringAsync(tenant.Id, cancellationToken);
                if (string.IsNullOrEmpty(connectionString))
                    continue;

                var options = new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
                    .Options;

                await using var tenantDb = new TenantDbContext(options);

                // Résolution des e-mails des responsables référencés par les échéances de ce tenant.
                // Les ResponsibleUserId sont des ids de la base master (collaborateurs cabinet OU
                // utilisateurs de la société), la résolution est donc indépendante du tenant.
                var responsibleIds = await tenantDb.FiscalScheduleEntries
                    .Where(e => e.ResponsibleUserId != null && !e.IsCancelled)
                    .Select(e => e.ResponsibleUserId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                if (responsibleIds.Count == 0)
                    continue;

                var users = await _master.Users.AsNoTracking()
                    .Where(u => responsibleIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
                    .ToListAsync(cancellationToken);
                var usersById = users.ToDictionary(
                    u => u.Id,
                    u => (Email: u.Email ?? string.Empty, Name: $"{u.FirstName} {u.LastName}".Trim()));

                // Rappels WhatsApp additifs : discussions des responsables liés, résolues depuis la
                // table tenant ChannelIdentityLinks (source de vérité). Null quand la feature est
                // OFF → ProcessTenantAsync garde son comportement historique e-mail seul.
                IReadOnlyDictionary<Guid, string>? whatsAppChatsById = null;
                if (_channelsSettings.Enabled
                    && _channelsSettings.WhatsAppEnabled
                    && _channelsSettings.FiscalWhatsAppRemindersEnabled)
                {
                    whatsAppChatsById = await tenantDb.ChannelIdentityLinks
                        .AsNoTracking()
                        .Where(l => l.ChannelType == Domain.Enums.ChannelType.WhatsApp
                            && l.IsActive
                            && responsibleIds.Contains(l.UserId))
                        .ToDictionaryAsync(l => l.UserId, l => l.ExternalChatId, cancellationToken);
                }

                totalSent += await _reminderService.ProcessTenantAsync(
                    tenantDb, usersById, tenant.Name, today, whatsAppChatsById, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fiscal reminders failed for tenant {TenantId}", tenant.Id);
            }
        }

        _logger.LogInformation(
            "Fiscal reminder job completed: {Count} reminder(s) sent across {Tenants} tenant(s).",
            totalSent, tenants.Count);
    }
}
