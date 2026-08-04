using FactuTrust.Domain.Entities.Honoraires;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for firm Honoraires billing (factures / devis / avoirs / encaissements).
/// Parallel to commercial Sales — does not touch Invoices/Quotes tables.
/// </summary>
public partial class TenantDbContext
{
    private static void ConfigureHonoraires(ModelBuilder builder)
    {
        ConfigureHonorairesInvoice(builder);
        ConfigureHonorairesInvoiceLine(builder);
        ConfigureHonorairesQuote(builder);
        ConfigureHonorairesQuoteLine(builder);
        ConfigureHonorairesPayment(builder);
        ConfigureHonorairesAttachment(builder);
    }

    private static void ConfigureHonorairesInvoice(ModelBuilder builder)
    {
        builder.Entity<HonorairesInvoice>(entity =>
        {
            entity.ToTable("HonorairesInvoices");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.Number).HasMaxLength(50);
            entity.Property(i => i.Type).HasConversion<int>().IsRequired();
            entity.Property(i => i.Status).HasConversion<int>().IsRequired();
            entity.Property(i => i.ClientName).HasMaxLength(256).IsRequired();
            entity.Property(i => i.ClientNif).HasMaxLength(50);
            entity.Property(i => i.ClientAddress).HasMaxLength(500);
            entity.Property(i => i.ContactName).HasMaxLength(200);
            entity.Property(i => i.ContactEmail).HasMaxLength(256);
            entity.Property(i => i.ContactPhone).HasMaxLength(50);
            entity.Property(i => i.Reference).HasMaxLength(100);
            entity.Property(i => i.Notes).HasMaxLength(2000);
            entity.Property(i => i.PaymentTerms).HasMaxLength(2000);
            entity.Property(i => i.PaymentMethod).HasMaxLength(100);
            entity.Property(i => i.BankAccountLabel).HasMaxLength(200);
            entity.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            entity.Property(i => i.CancellationReason).HasMaxLength(500);
            entity.Property(i => i.RecurrenceFrequency).HasConversion<int?>();

            entity.Ignore(i => i.AmountDue);
            entity.Ignore(i => i.IsCreditNote);

            ConfigureMoneyOwned(entity, i => i.SubTotal, "SubTotal");
            ConfigureMoneyOwned(entity, i => i.TotalVat, "TotalVat");
            ConfigureMoneyOwned(entity, i => i.WithholdingAmount, "WithholdingAmount");
            ConfigureMoneyOwned(entity, i => i.TotalAmount, "TotalAmount");
            ConfigureMoneyOwned(entity, i => i.AmountPaid, "AmountPaid");

            entity.HasMany(i => i.Lines)
                .WithOne(l => l.Invoice)
                .HasForeignKey(l => l.HonorairesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Payments)
                .WithOne(p => p.Invoice)
                .HasForeignKey(p => p.HonorairesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => new { i.FirmClientAssignmentId, i.IssueDate });
            entity.HasIndex(i => i.SourceQuoteId);
            entity.HasIndex(i => i.LinkedInvoiceId);
            entity.HasIndex(i => i.Number)
                .IsUnique()
                .HasFilter("[Number] IS NOT NULL");
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.Type);
        });
    }

    private static void ConfigureHonorairesInvoiceLine(ModelBuilder builder)
    {
        builder.Entity<HonorairesInvoiceLine>(entity =>
        {
            entity.ToTable("HonorairesInvoiceLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.ActivityCode).HasMaxLength(50);
            entity.Property(l => l.Designation).HasMaxLength(500).IsRequired();
            entity.Property(l => l.Description).HasMaxLength(1000);
            entity.Property(l => l.Quantity).HasPrecision(18, 3);
            entity.Property(l => l.VatRate).HasConversion<int>();
            entity.Property(l => l.DiscountPercent).HasPrecision(5, 2);

            ConfigureMoneyOwned(entity, l => l.UnitPrice, "UnitPrice");
            ConfigureMoneyOwned(entity, l => l.DiscountAmount, "DiscountAmount");
            ConfigureMoneyOwned(entity, l => l.SubTotal, "SubTotal");
            ConfigureMoneyOwned(entity, l => l.VatAmount, "VatAmount");
            ConfigureMoneyOwned(entity, l => l.Total, "Total");

            entity.HasIndex(l => l.HonorairesInvoiceId);
        });
    }

    private static void ConfigureHonorairesQuote(ModelBuilder builder)
    {
        builder.Entity<HonorairesQuote>(entity =>
        {
            entity.ToTable("HonorairesQuotes");
            entity.HasKey(q => q.Id);

            entity.Property(q => q.Number).HasMaxLength(50);
            entity.Property(q => q.Status).HasConversion<int>().IsRequired();
            entity.Property(q => q.ClientName).HasMaxLength(256).IsRequired();
            entity.Property(q => q.ClientNif).HasMaxLength(50);
            entity.Property(q => q.ClientAddress).HasMaxLength(500);
            entity.Property(q => q.ContactName).HasMaxLength(200);
            entity.Property(q => q.ContactEmail).HasMaxLength(256);
            entity.Property(q => q.ContactPhone).HasMaxLength(50);
            entity.Property(q => q.Reference).HasMaxLength(100);
            entity.Property(q => q.Notes).HasMaxLength(2000);
            entity.Property(q => q.PaymentTerms).HasMaxLength(2000);
            entity.Property(q => q.Currency).HasMaxLength(3).IsRequired();

            ConfigureMoneyOwned(entity, q => q.SubTotal, "SubTotal");
            ConfigureMoneyOwned(entity, q => q.TotalVat, "TotalVat");
            ConfigureMoneyOwned(entity, q => q.TotalAmount, "TotalAmount");

            entity.HasMany(q => q.Lines)
                .WithOne(l => l.Quote)
                .HasForeignKey(l => l.HonorairesQuoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(q => new { q.FirmClientAssignmentId, q.IssueDate });
            entity.HasIndex(q => q.ConvertedInvoiceId);
            entity.HasIndex(q => q.Number)
                .IsUnique()
                .HasFilter("[Number] IS NOT NULL");
            entity.HasIndex(q => q.Status);
        });
    }

    private static void ConfigureHonorairesQuoteLine(ModelBuilder builder)
    {
        builder.Entity<HonorairesQuoteLine>(entity =>
        {
            entity.ToTable("HonorairesQuoteLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.ActivityCode).HasMaxLength(50);
            entity.Property(l => l.Designation).HasMaxLength(500).IsRequired();
            entity.Property(l => l.Description).HasMaxLength(1000);
            entity.Property(l => l.Quantity).HasPrecision(18, 3);
            entity.Property(l => l.VatRate).HasConversion<int>();
            entity.Property(l => l.DiscountPercent).HasPrecision(5, 2);

            ConfigureMoneyOwned(entity, l => l.UnitPrice, "UnitPrice");
            ConfigureMoneyOwned(entity, l => l.DiscountAmount, "DiscountAmount");
            ConfigureMoneyOwned(entity, l => l.SubTotal, "SubTotal");
            ConfigureMoneyOwned(entity, l => l.VatAmount, "VatAmount");
            ConfigureMoneyOwned(entity, l => l.Total, "Total");

            entity.HasIndex(l => l.HonorairesQuoteId);
        });
    }

    private static void ConfigureHonorairesPayment(ModelBuilder builder)
    {
        builder.Entity<HonorairesPayment>(entity =>
        {
            entity.ToTable("HonorairesPayments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Method).HasConversion<int>().IsRequired();
            entity.Property(p => p.Reference).HasMaxLength(100);
            entity.Property(p => p.Notes).HasMaxLength(1000);
            entity.Property(p => p.BankAccountLabel).HasMaxLength(200);
            entity.Ignore(p => p.AppliedAmount);

            ConfigureMoneyOwned(entity, p => p.Amount, "Amount");
            ConfigureMoneyOwned(entity, p => p.ClientWithholdingAmount, "ClientWithholdingAmount");

            entity.HasIndex(p => p.HonorairesInvoiceId);
            entity.HasIndex(p => p.PaymentDate);
        });
    }

    private static void ConfigureHonorairesAttachment(ModelBuilder builder)
    {
        builder.Entity<HonorairesAttachment>(entity =>
        {
            entity.ToTable("HonorairesAttachments");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.DocumentKind).HasConversion<int>().IsRequired();
            entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(a => a.StorageRelativePath).HasMaxLength(500).IsRequired();
            entity.Property(a => a.UploadedBy).HasMaxLength(256);

            entity.HasIndex(a => new { a.DocumentKind, a.DocumentId });
        });
    }

    private static void ConfigureMoneyOwned<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, Domain.ValueObjects.Money?>> propertyExpression,
        string columnPrefix)
        where TEntity : class
    {
        entity.OwnsOne(propertyExpression, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName(columnPrefix)
                .HasPrecision(18, 3)
                .IsRequired();
            price.Property(m => m.Currency)
                .HasColumnName(columnPrefix + "Currency")
                .HasMaxLength(3)
                .IsRequired();
        });
    }
}
