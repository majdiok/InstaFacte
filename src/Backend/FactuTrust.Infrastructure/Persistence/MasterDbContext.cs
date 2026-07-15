using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Communications;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Entities.Channels;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Master database context for tenant management and authentication.
/// Contains shared data across all tenants.
/// </summary>
public class MasterDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IDataProtectionKeyContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantConnectionString> TenantConnectionStrings => Set<TenantConnectionString>();
    public DbSet<UserModuleGrant> UserModuleGrants => Set<UserModuleGrant>();
    public DbSet<AccountingFirmProfile> AccountingFirmProfiles => Set<AccountingFirmProfile>();
    public DbSet<FirmClientAssignment> FirmClientAssignments => Set<FirmClientAssignment>();

    // Public Virtual Street (3D storefront projection)
    public DbSet<StorefrontProfile> StorefrontProfiles => Set<StorefrontProfile>();
    public DbSet<StorefrontProduct> StorefrontProducts => Set<StorefrontProduct>();
    public DbSet<StorefrontOrder> StorefrontOrders => Set<StorefrontOrder>();
    public DbSet<StorefrontOrderItem> StorefrontOrderItems => Set<StorefrontOrderItem>();
    public DbSet<StorefrontPublishingConsent> StorefrontPublishingConsents => Set<StorefrontPublishingConsent>();
    public DbSet<StorefrontOutboxInboxEntry> StorefrontOutboxInboxEntries => Set<StorefrontOutboxInboxEntry>();

    // Data Protection Keys storage
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Lot B2 — 2FA TOTP profile (one-to-one with ApplicationUser, only for platform admins)
    public DbSet<PlatformAdminProfile> PlatformAdminProfiles => Set<PlatformAdminProfile>();

    // Lot B4 — Active sessions tracking + failed login attempts (master DB, audit/security usage)
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<FailedLoginAttempt> FailedLoginAttempts => Set<FailedLoginAttempt>();

    // Lot C1 — Configurable plans + per-plan limits/features/modules + per-tenant module overrides
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanLimit> PlanLimits => Set<PlanLimit>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<PlanModule> PlanModules => Set<PlanModule>();
    public DbSet<TenantModuleOverride> TenantModuleOverrides => Set<TenantModuleOverride>();

    // Lot C2 — Email message audit log (master DB)
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    // Lot C3 — Coupons (codes promo) + redemptions + tenant credits
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<TenantCredit> TenantCredits => Set<TenantCredit>();

    // Lot C4 — Platform invoicing : fiscal settings + invoice sequences + invoices + lines + receipts
    public DbSet<PlatformFiscalSettings> PlatformFiscalSettings => Set<PlatformFiscalSettings>();
    public DbSet<PlatformInvoiceSequence> PlatformInvoiceSequences => Set<PlatformInvoiceSequence>();
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();
    public DbSet<PlatformInvoiceLine> PlatformInvoiceLines => Set<PlatformInvoiceLine>();
    public DbSet<PlatformReceipt> PlatformReceipts => Set<PlatformReceipt>();

    // Lot C5 — Payment providers : Konnect / Paymee / Wire + intents + webhooks
    public DbSet<PaymentProviderConfig> PaymentProviderConfigs => Set<PaymentProviderConfig>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    // Lot C6 — Renouvellement automatique + Dunning
    public DbSet<DunningCampaign> DunningCampaigns => Set<DunningCampaign>();
    public DbSet<DunningState> DunningStates => Set<DunningState>();

    // Configuration IA plateforme (modèle LLM global, singleton)
    public DbSet<PlatformAiSettings> PlatformAiSettings => Set<PlatformAiSettings>();

    // Canaux externes (WhatsApp/Telegram) — index de routage master : identité externe → tenant/user.
    // Source de vérité du lien = tables channel de la base tenant (revalidées à chaque traitement).
    public DbSet<ChannelExternalRoute> ChannelExternalRoutes => Set<ChannelExternalRoute>();
    public DbSet<ChannelLinkCodePointer> ChannelLinkCodePointers => Set<ChannelLinkCodePointer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore domain events - they are not database entities
        builder.Ignore<Domain.Common.DomainEvent>();
        builder.Ignore<TenantCreatedEvent>();
        builder.Ignore<TenantUpdatedEvent>();
        builder.Ignore<TenantDeactivatedEvent>();
        builder.Ignore<TenantReactivatedEvent>();
        builder.Ignore<StorefrontOptInCreatedEvent>();
        builder.Ignore<StorefrontSubmittedForReviewEvent>();
        builder.Ignore<StorefrontPublishedEvent>();
        builder.Ignore<StorefrontRejectedEvent>();
        builder.Ignore<StorefrontSuspendedEvent>();
        builder.Ignore<StorefrontUnpublishedEvent>();
        builder.Ignore<StorefrontProfileUpdatedEvent>();
        builder.Ignore<PublicOrderSubmittedEvent>();

        // Rename Identity tables to more meaningful names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasMany(u => u.ModuleGrants)
                .WithOne()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        // Tenant configuration
        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(t => t.Id);
            
            entity.Property(t => t.CompanyName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(t => t.DatabaseName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(t => t.DatabaseName).IsUnique();

            entity.Property(t => t.Kind)
                .HasDefaultValue(TenantKind.Company)
                .IsRequired();

            entity.HasIndex(t => t.Kind);

            // Value object configurations
            entity.OwnsOne(t => t.NIF, nif =>
            {
                nif.Property(n => n.Value)
                    .HasColumnName("NIF")
                    .HasMaxLength(20)
                    .IsRequired();
            });

            entity.OwnsOne(t => t.Address, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200).IsRequired();
                addr.Property(a => a.StreetLine2).HasColumnName("StreetLine2").HasMaxLength(200);
                addr.Property(a => a.City).HasColumnName("City").HasMaxLength(100).IsRequired();
                addr.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
                addr.Property(a => a.Governorate).HasColumnName("Governorate").HasMaxLength(100).IsRequired();
                addr.Property(a => a.Country).HasColumnName("Country").HasMaxLength(100).IsRequired();
            });

            entity.OwnsOne(t => t.Email, email =>
            {
                email.Property(e => e.Value)
                    .HasColumnName("Email")
                    .HasMaxLength(256)
                    .IsRequired();
            });

            entity.OwnsOne(t => t.Phone, phone =>
            {
                phone.Property(p => p.Value)
                    .HasColumnName("Phone")
                    .HasMaxLength(20)
                    .IsRequired();
                phone.Ignore(p => p.CountryCode);
                phone.Ignore(p => p.LocalNumber);
            });
        });

        // Subscription configuration
        builder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(s => s.Id);

            entity.OwnsOne(s => s.MonthlyPrice, price =>
            {
                price.Property(p => p.Amount).HasColumnName("MonthlyPriceAmount").HasPrecision(18, 3);
                price.Property(p => p.Currency).HasColumnName("MonthlyPriceCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(s => s.AnnualPrice, price =>
            {
                price.Property(p => p.Amount).HasColumnName("AnnualPriceAmount").HasPrecision(18, 3);
                price.Property(p => p.Currency).HasColumnName("AnnualPriceCurrency").HasMaxLength(3);
            });

            // Lot C1 — FK rétro-compatible vers Plans (NULL accepté ; pas de cascade :
            // un Archive d'un Plan ne casse pas les subscriptions historiques).
            entity.HasIndex(s => s.PlanId);
            entity.HasOne<Plan>()
                .WithMany()
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TenantConnectionString configuration
        builder.Entity<TenantConnectionString>(entity =>
        {
            entity.ToTable("TenantConnectionStrings");
            entity.HasKey(t => t.Id);
            
            entity.Property(t => t.EncryptedConnectionString)
                .HasMaxLength(1000)
                .IsRequired();

            entity.HasIndex(t => t.TenantId).IsUnique();
        });

        builder.Entity<AccountingFirmProfile>(entity =>
        {
            entity.ToTable("AccountingFirmProfiles");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.TenantId).IsUnique();
            entity.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.City).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Governorate).HasMaxLength(100).IsRequired();
            entity.Property(p => p.ProfessionalRegistrationNumber).HasMaxLength(100);
            entity.Property(p => p.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(p => p.ContactPhone).HasMaxLength(20).IsRequired();
            entity.HasIndex(p => new { p.IsPublicInDirectory, p.City });
        });

        builder.Entity<FirmClientAssignment>(entity =>
        {
            entity.ToTable("FirmClientAssignments");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.CompanyTenantId, a.FirmTenantId, a.Status });
            entity.HasIndex(a => a.FirmTenantId);
            entity.Property(a => a.Notes).HasMaxLength(1000);
        });

        builder.Entity<UserModuleGrant>(entity =>
        {
            entity.ToTable("UserModuleGrants");
            entity.HasKey(g => g.Id);
            entity.HasIndex(g => new { g.UserId, g.Module }).IsUnique();
            entity.Property(g => g.EnabledFeatureKeys).HasMaxLength(2000);
        });

        // Lot B2 — Platform admin 2FA profile (1-to-1 avec ApplicationUser).
        builder.Entity<PlatformAdminProfile>(entity =>
        {
            entity.ToTable("PlatformAdminProfiles");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.UserId).IsUnique();

            entity.Property(p => p.MfaSecretEncrypted).HasMaxLength(500);
            entity.Property(p => p.RecoveryCodesJson).HasMaxLength(4000);
        });

        // Lot B4 — User active sessions tracking
        builder.Entity<UserSession>(entity =>
        {
            entity.ToTable("UserSessions");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.JwtId).IsUnique();
            entity.HasIndex(s => new { s.UserId, s.RevokedAt });
            entity.HasIndex(s => s.ExpiresAt);

            entity.Property(s => s.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(s => s.UserAgent).HasMaxLength(500);
            entity.Property(s => s.RefreshTokenHash).HasMaxLength(128);
            entity.Property(s => s.RevocationReason).HasMaxLength(300);
        });

        // Lot B4 — Failed login attempts (security audit)
        builder.Entity<FailedLoginAttempt>(entity =>
        {
            entity.ToTable("FailedLoginAttempts");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.Email, a.AttemptAt });
            entity.HasIndex(a => new { a.IpAddress, a.AttemptAt });
            entity.HasIndex(a => a.AttemptAt);

            entity.Property(a => a.Email).HasMaxLength(254).IsRequired();
            entity.Property(a => a.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(a => a.UserAgent).HasMaxLength(500);
        });

        // Lot C1 — Plan configurable + relations cascade
        builder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Code).IsUnique();

            entity.Property(p => p.Code).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.BasePriceTND).HasPrecision(18, 3);
            entity.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            entity.Property(p => p.BillingPeriod).HasConversion<int>();

            entity.HasMany(p => p.Limits)
                .WithOne()
                .HasForeignKey(l => l.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Features)
                .WithOne()
                .HasForeignKey(f => f.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Modules)
                .WithOne()
                .HasForeignKey(m => m.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlanLimit>(entity =>
        {
            entity.ToTable("PlanLimits");
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.PlanId, l.Key }).IsUnique();
            entity.Property(l => l.Key).HasMaxLength(100).IsRequired();
            entity.Property(l => l.Value).HasMaxLength(200).IsRequired();
        });

        builder.Entity<PlanFeature>(entity =>
        {
            entity.ToTable("PlanFeatures");
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.PlanId, f.FeatureKey }).IsUnique();
            entity.Property(f => f.FeatureKey).HasMaxLength(100).IsRequired();
        });

        builder.Entity<PlanModule>(entity =>
        {
            entity.ToTable("PlanModules");
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.PlanId, m.Module }).IsUnique();
        });

        builder.Entity<TenantModuleOverride>(entity =>
        {
            entity.ToTable("TenantModuleOverrides");
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => new { o.TenantId, o.Module }).IsUnique();
            entity.HasIndex(o => o.ExpiresAt);
            entity.Property(o => o.Reason).HasMaxLength(300);
        });

        // Lot C2 — Email messages log
        builder.Entity<EmailMessage>(entity =>
        {
            entity.ToTable("EmailMessages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RelatedTenantId, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.ToEmail).HasMaxLength(254).IsRequired();
            entity.Property(e => e.ToName).HasMaxLength(200);
            entity.Property(e => e.TemplateCode).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ProviderMessageId).HasMaxLength(200);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Status).HasConversion<int>();
        });

        // Lot C3 — Coupons + redemptions + credits
        builder.Entity<Coupon>(entity =>
        {
            entity.ToTable("Coupons");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Code).IsUnique();
            entity.HasIndex(c => new { c.IsActive, c.ValidTo });

            entity.Property(c => c.Code).HasMaxLength(40).IsRequired();
            entity.Property(c => c.Type).HasConversion<int>();
            entity.Property(c => c.Value).HasPrecision(18, 3);
            entity.Property(c => c.Notes).HasMaxLength(500);
        });

        builder.Entity<CouponRedemption>(entity =>
        {
            entity.ToTable("CouponRedemptions");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.CouponId, r.TenantId }).IsUnique();
            entity.HasIndex(r => new { r.TenantId, r.RedeemedAt });

            entity.Property(r => r.AmountSavedTND).HasPrecision(18, 3);

            entity.HasOne<Coupon>()
                .WithMany()
                .HasForeignKey(r => r.CouponId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TenantCredit>(entity =>
        {
            entity.ToTable("TenantCredits");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.TenantId, c.ExpiresAt });
            entity.HasIndex(c => c.RevokedAt);

            entity.Property(c => c.AmountTND).HasPrecision(18, 3);
            entity.Property(c => c.ConsumedAmountTND).HasPrecision(18, 3);
            entity.Property(c => c.Reason).HasMaxLength(500).IsRequired();
            entity.Property(c => c.RevocationReason).HasMaxLength(500);
        });

        // Lot C4 — Platform fiscal settings (singleton ; pas d'index unique car Id seul suffit, l'admin force 1 seule ligne)
        builder.Entity<PlatformFiscalSettings>(entity =>
        {
            entity.ToTable("PlatformFiscalSettings");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nif).HasMaxLength(40).IsRequired();
            entity.Property(p => p.CodeTva).HasMaxLength(40);
            entity.Property(p => p.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Address).HasMaxLength(500).IsRequired();
            entity.Property(p => p.Phone).HasMaxLength(40);
            entity.Property(p => p.Email).HasMaxLength(254);
            entity.Property(p => p.Website).HasMaxLength(254);
            entity.Property(p => p.Iban).HasMaxLength(60);
            entity.Property(p => p.BankName).HasMaxLength(120);
            entity.Property(p => p.DefaultVatRate).HasPrecision(5, 2);
            entity.Property(p => p.TimbreFiscalAmount).HasPrecision(18, 3);
            entity.Property(p => p.ClientWithholdingRate).HasPrecision(5, 2);
            entity.Property(p => p.InvoiceNumberPrefix).HasMaxLength(10).IsRequired();
            entity.Property(p => p.ReceiptNumberPrefix).HasMaxLength(10).IsRequired();
            entity.Property(p => p.LegalMentions).HasMaxLength(2000);
        });

        // Configuration IA plateforme (singleton ; pas d'index unique, l'admin force 1 seule ligne)
        builder.Entity<PlatformAiSettings>(entity =>
        {
            entity.ToTable("PlatformAiSettings");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.DefaultModelRef).HasMaxLength(500);
            entity.Property(p => p.InvoiceImportModelRef).HasMaxLength(500);
            entity.Property(p => p.InferenceDevice).HasConversion<int>().HasDefaultValue(Domain.Enums.OllamaInferenceDevice.Gpu);
            entity.Property(p => p.CreatedBy).HasMaxLength(450);
            entity.Property(p => p.UpdatedBy).HasMaxLength(450);
        });

        // Canaux externes — routage master (identité externe → tenant/user)
        builder.Entity<ChannelExternalRoute>(entity =>
        {
            entity.ToTable("ChannelExternalRoutes");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.ChannelType, r.ExternalUserId }).IsUnique();
            entity.HasIndex(r => new { r.UserId, r.ChannelType });
            entity.HasIndex(r => new { r.TenantId, r.ChannelType, r.IsActive });

            entity.Property(r => r.ChannelType).HasConversion<int>();
            entity.Property(r => r.ExternalUserId).HasMaxLength(128).IsRequired();
            entity.Property(r => r.CreatedBy).HasMaxLength(450);
            entity.Property(r => r.UpdatedBy).HasMaxLength(450);
        });

        // Canaux externes — pointeur master de code de liaison (hash → tenant/user)
        builder.Entity<ChannelLinkCodePointer>(entity =>
        {
            entity.ToTable("ChannelLinkCodePointers");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.ChannelType, p.CodeHash });
            entity.HasIndex(p => p.ExpiresAt);

            entity.Property(p => p.ChannelType).HasConversion<int>();
            entity.Property(p => p.CodeHash).HasMaxLength(128).IsRequired();
            entity.Property(p => p.CreatedBy).HasMaxLength(450);
            entity.Property(p => p.UpdatedBy).HasMaxLength(450);
        });

        // Lot C4 — Platform invoice sequence (numérotation séquentielle par année)
        builder.Entity<PlatformInvoiceSequence>(entity =>
        {
            entity.ToTable("PlatformInvoiceSequences");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.DocumentType, s.Year }).IsUnique();
            entity.Property(s => s.DocumentType).HasMaxLength(20).IsRequired();
            entity.Property(s => s.RowVersion).IsRowVersion();
        });

        // Lot C4 — Platform invoices + lines (cascade)
        builder.Entity<PlatformInvoice>(entity =>
        {
            entity.ToTable("PlatformInvoices");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.Number).IsUnique().HasFilter("[Number] IS NOT NULL");
            entity.HasIndex(i => new { i.TenantId, i.InvoiceDate });
            entity.HasIndex(i => new { i.Status, i.DueDate });
            entity.HasIndex(i => i.SequenceYear);

            entity.Property(i => i.Number).HasMaxLength(40);
            entity.Property(i => i.BillingType).HasConversion<int>();
            entity.Property(i => i.Status).HasConversion<int>();
            entity.Property(i => i.SubtotalHT).HasPrecision(18, 3);
            entity.Property(i => i.VatAmount).HasPrecision(18, 3);
            entity.Property(i => i.DiscountAmount).HasPrecision(18, 3);
            entity.Property(i => i.CreditsApplied).HasPrecision(18, 3);
            entity.Property(i => i.StampDuty).HasPrecision(18, 3);
            entity.Property(i => i.TotalTTC).HasPrecision(18, 3);
            entity.Property(i => i.PdfStorageKey).HasMaxLength(300);
            entity.Property(i => i.LegalMentions).HasMaxLength(2000);
            entity.Property(i => i.FiscalSnapshotJson).HasColumnType("nvarchar(max)");
            entity.Property(i => i.CancelledReason).HasMaxLength(500);

            entity.HasMany(i => i.Lines)
                .WithOne()
                .HasForeignKey(l => l.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Receipts)
                .WithOne()
                .HasForeignKey(r => r.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(i => i.Receipts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<PlatformInvoiceLine>(entity =>
        {
            entity.ToTable("PlatformInvoiceLines");
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => l.InvoiceId);

            entity.Property(l => l.Description).HasMaxLength(500).IsRequired();
            entity.Property(l => l.Quantity).HasPrecision(18, 3);
            entity.Property(l => l.UnitPriceHT).HasPrecision(18, 3);
            entity.Property(l => l.VatRate).HasPrecision(5, 2);
            entity.Property(l => l.LineTotalHT).HasPrecision(18, 3);
            entity.Property(l => l.LineTotalTTC).HasPrecision(18, 3);
        });

        // Lot C4 — Platform receipts (paiements rattachés à une facture)
        builder.Entity<PlatformReceipt>(entity =>
        {
            entity.ToTable("PlatformReceipts");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.ReceiptNumber).IsUnique();
            entity.HasIndex(r => new { r.InvoiceId, r.PaymentDate });

            entity.Property(r => r.ReceiptNumber).HasMaxLength(40).IsRequired();
            entity.Property(r => r.Method).HasConversion<int>();
            entity.Property(r => r.Status).HasConversion<int>();
            entity.Property(r => r.Reference).HasMaxLength(120);
            entity.Property(r => r.AmountTND).HasPrecision(18, 3);
            entity.Property(r => r.ProviderTxId).HasMaxLength(120);
            entity.Property(r => r.ProviderPayload).HasColumnType("nvarchar(max)");
            entity.Property(r => r.PdfStorageKey).HasMaxLength(300);
            entity.Property(r => r.CancelledReason).HasMaxLength(500);
        });

        // Lot C5 — Payment provider config (1 ligne par provider connu)
        builder.Entity<PaymentProviderConfig>(entity =>
        {
            entity.ToTable("PaymentProviderConfigs");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.ProviderCode).IsUnique();

            entity.Property(p => p.ProviderCode).HasMaxLength(20).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(p => p.EncryptedSecretsJson).HasColumnType("nvarchar(max)");
            entity.Property(p => p.WebhookSecretEncrypted).HasMaxLength(500);
            entity.Property(p => p.AllowedReturnDomain).HasMaxLength(254);
        });

        // Lot C5 — Payment intents (1 par tentative de paiement)
        builder.Entity<PaymentIntent>(entity =>
        {
            entity.ToTable("PaymentIntents");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.IdempotencyKey).IsUnique();
            entity.HasIndex(i => new { i.InvoiceId, i.Status });
            entity.HasIndex(i => new { i.TenantId, i.CreatedAt });
            entity.HasIndex(i => new { i.ProviderCode, i.ProviderRef });

            entity.Property(i => i.ProviderCode).HasMaxLength(20).IsRequired();
            entity.Property(i => i.ProviderRef).HasMaxLength(120);
            entity.Property(i => i.AmountTND).HasPrecision(18, 3);
            entity.Property(i => i.Status).HasConversion<int>();
            entity.Property(i => i.IdempotencyKey).HasMaxLength(80).IsRequired();
            entity.Property(i => i.ReturnUrl).HasMaxLength(500);
            entity.Property(i => i.RedirectUrl).HasMaxLength(500);
            entity.Property(i => i.RawProviderPayload).HasColumnType("nvarchar(max)");
            entity.Property(i => i.FailureReason).HasMaxLength(500);
        });

        // Lot C5 — Payment webhook events (idempotence stricte)
        builder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.ToTable("PaymentWebhookEvents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProviderCode, e.ProviderEventId }).IsUnique();
            entity.HasIndex(e => e.ReceivedAt);

            entity.Property(e => e.ProviderCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProviderEventId).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ProcessingError).HasMaxLength(2000);
        });

        // Lot C6 — Dunning campaigns + per-subscription states
        builder.Entity<DunningCampaign>(entity =>
        {
            entity.ToTable("DunningCampaigns");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.IsActive);
            entity.Property(c => c.Name).HasMaxLength(120).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.StepsJson).HasColumnType("nvarchar(max)").IsRequired();
        });

        builder.Entity<DunningState>(entity =>
        {
            entity.ToTable("DunningStates");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.SubscriptionId, s.Outcome });
            entity.HasIndex(s => new { s.Outcome, s.NextActionAt });
            entity.HasIndex(s => s.TenantId);

            entity.Property(s => s.Outcome).HasConversion<int>();
            entity.Property(s => s.LastError).HasMaxLength(1000);
        });

        ConfigureStorefrontProfile(builder);
        ConfigureStorefrontProduct(builder);
        ConfigureStorefrontOrder(builder);
        ConfigureStorefrontOrderItem(builder);
        ConfigureStorefrontPublishingConsent(builder);
        ConfigureStorefrontOutboxInboxEntry(builder);
    }

    private static void ConfigureStorefrontProfile(ModelBuilder builder)
    {
        builder.Entity<StorefrontProfile>(entity =>
        {
            entity.ToTable("StorefrontProfiles");
            entity.HasKey(p => p.Id);

            entity.HasIndex(p => p.TenantId).IsUnique();
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.HasIndex(p => new { p.Status, p.Category });
            entity.HasIndex(p => p.StreetPositionIndex)
                .IsUnique()
                .HasFilter("[StreetPositionIndex] IS NOT NULL AND [Status] = 2");

            entity.Property(p => p.TenantId).IsRequired();
            entity.Property(p => p.Slug).HasMaxLength(60).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(StorefrontProfile.DisplayNameMaxLength).IsRequired();
            entity.Property(p => p.Tagline).HasMaxLength(StorefrontProfile.TaglineMaxLength);
            entity.Property(p => p.DescriptionMarkdown).HasMaxLength(StorefrontProfile.DescriptionMaxLength);
            entity.Property(p => p.BrandPrimaryColorHex).HasMaxLength(7).IsRequired();
            entity.Property(p => p.BrandSecondaryColorHex).HasMaxLength(7).IsRequired();
            entity.Property(p => p.PublicLogoUrl).HasMaxLength(500);
            entity.Property(p => p.PublicCoverImageUrl).HasMaxLength(500);
            entity.Property(p => p.PublicContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(p => p.PublicContactPhone).HasMaxLength(30);
            entity.Property(p => p.PublicContactWhatsApp).HasMaxLength(30);
            entity.Property(p => p.Category).HasConversion<int>();
            entity.Property(p => p.Status).HasConversion<int>();
            entity.Property(p => p.FacadeTheme).HasConversion<int>();
            entity.Property(p => p.OrderSubmissionEnabled).HasDefaultValue(true);
            entity.Property(p => p.RejectionReason).HasMaxLength(StorefrontProfile.RejectionReasonMaxLength);
            entity.Property(p => p.SuspensionReason).HasMaxLength(StorefrontProfile.RejectionReasonMaxLength);
            entity.Property(p => p.ConsentVersion).HasMaxLength(40).IsRequired();

            entity.HasOne<Tenant>()
                .WithOne()
                .HasForeignKey<StorefrontProfile>(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureStorefrontProduct(ModelBuilder builder)
    {
        builder.Entity<StorefrontProduct>(entity =>
        {
            entity.ToTable("StorefrontProducts");
            entity.HasKey(p => p.Id);

            entity.HasIndex(p => new { p.StorefrontProfileId, p.SourceProductId }).IsUnique();
            entity.HasIndex(p => new { p.StorefrontProfileId, p.Slug }).IsUnique();
            entity.HasIndex(p => new { p.IsVisible, p.StorefrontProfileId });

            entity.Property(p => p.StorefrontProfileId).IsRequired();
            entity.Property(p => p.TenantId).IsRequired();
            entity.Property(p => p.SourceProductId).IsRequired();
            entity.Property(p => p.Slug).HasMaxLength(120).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(StorefrontProduct.NameMaxLength).IsRequired();
            entity.Property(p => p.DescriptionSanitized).HasMaxLength(StorefrontProduct.DescriptionMaxLength);
            entity.Property(p => p.PriceAmount).HasPrecision(18, 3);
            entity.Property(p => p.PriceCurrency).HasMaxLength(3).IsRequired();
            entity.Property(p => p.PublicImageUrl).HasMaxLength(500);
            entity.Property(p => p.ImageHash).HasMaxLength(64);
            entity.Property(p => p.CategoryLabel).HasMaxLength(100);
            entity.Property(p => p.StockDisplayStatus).HasConversion<int>();

            entity.HasOne<StorefrontProfile>()
                .WithMany()
                .HasForeignKey(p => p.StorefrontProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureStorefrontOrder(ModelBuilder builder)
    {
        builder.Entity<StorefrontOrder>(entity =>
        {
            entity.ToTable("StorefrontOrders");
            entity.HasKey(o => o.Id);

            entity.HasIndex(o => o.SubmittedAt);
            entity.HasIndex(o => o.Status);

            entity.Property(o => o.GuestFullName).HasMaxLength(StorefrontOrder.GuestNameMaxLength).IsRequired();
            entity.Property(o => o.GuestEmail).HasMaxLength(256).IsRequired();
            entity.Property(o => o.GuestPhone).HasMaxLength(30).IsRequired();
            entity.Property(o => o.GuestNotes).HasMaxLength(StorefrontOrder.GuestNotesMaxLength);
            entity.Property(o => o.Status).HasConversion<int>();
            entity.Property(o => o.IpAddressHash).HasMaxLength(64);
            entity.Property(o => o.UserAgentHash).HasMaxLength(64);
            entity.Property(o => o.TenantDispatchResultsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.OwnsOne(o => o.GuestDeliveryAddress, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("DeliveryStreet").HasMaxLength(200).IsRequired();
                addr.Property(a => a.StreetLine2).HasColumnName("DeliveryStreetLine2").HasMaxLength(200);
                addr.Property(a => a.City).HasColumnName("DeliveryCity").HasMaxLength(100).IsRequired();
                addr.Property(a => a.PostalCode).HasColumnName("DeliveryPostalCode").HasMaxLength(20);
                addr.Property(a => a.Governorate).HasColumnName("DeliveryGovernorate").HasMaxLength(100).IsRequired();
                addr.Property(a => a.Country).HasColumnName("DeliveryCountry").HasMaxLength(100).IsRequired();
            });

            entity.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.StorefrontOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigureStorefrontOrderItem(ModelBuilder builder)
    {
        builder.Entity<StorefrontOrderItem>(entity =>
        {
            entity.ToTable("StorefrontOrderItems");
            entity.HasKey(i => i.Id);

            entity.HasIndex(i => i.StorefrontOrderId);
            entity.HasIndex(i => i.TenantId);

            entity.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            entity.Property(i => i.Quantity).HasPrecision(18, 3);
            entity.Property(i => i.UnitPriceAmount).HasPrecision(18, 3);
        });
    }

    private static void ConfigureStorefrontPublishingConsent(ModelBuilder builder)
    {
        builder.Entity<StorefrontPublishingConsent>(entity =>
        {
            entity.ToTable("StorefrontPublishingConsents");
            entity.HasKey(c => c.Id);

            entity.HasIndex(c => c.TenantId);
            entity.HasIndex(c => c.StorefrontProfileId);

            entity.Property(c => c.TermsVersion).HasMaxLength(40).IsRequired();
            entity.Property(c => c.IpAddressHash).HasMaxLength(64);
            entity.Property(c => c.UserAgent).HasMaxLength(300);
        });
    }

    private static void ConfigureStorefrontOutboxInboxEntry(ModelBuilder builder)
    {
        builder.Entity<StorefrontOutboxInboxEntry>(entity =>
        {
            entity.ToTable("StorefrontOutboxInboxEntries");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.TenantId, e.AggregateType, e.AggregateId, e.SourceVersion })
                .IsUnique();

            entity.Property(e => e.AggregateType).HasMaxLength(80).IsRequired();
        });
    }
}

/// <summary>
/// Application user with extended properties.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    public ICollection<UserModuleGrant> ModuleGrants { get; set; } = new List<UserModuleGrant>();
}

/// <summary>
/// Application role with extended properties.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}

/// <summary>
/// Stores encrypted connection strings for tenant databases.
/// </summary>
public class TenantConnectionString
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EncryptedConnectionString { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
