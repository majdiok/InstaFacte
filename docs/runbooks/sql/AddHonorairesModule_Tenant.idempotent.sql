-- Module Honoraires cabinet (factures, devis, encaissements, pièces jointes).
-- MigrationId: 20260801120000_AddHonorairesModule_Tenant
--
-- À exécuter sur CHAQUE base tenant AccountingFirm si la migration EF ne peut pas
-- être appliquée par l'API. Idempotent : rejouable sans effet de bord.
-- Création de tables uniquement — aucune table Sales existante n'est touchée.

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[dbo].[HonorairesInvoices]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesInvoices] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NULL,
        [IssueDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [Status] int NOT NULL,
        [Type] int NOT NULL,
        [FirmClientAssignmentId] uniqueidentifier NOT NULL,
        [ClientName] nvarchar(256) NOT NULL,
        [ClientNif] nvarchar(50) NULL,
        [ClientAddress] nvarchar(500) NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactEmail] nvarchar(256) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [PaymentTerms] nvarchar(2000) NULL,
        [PaymentMethod] nvarchar(100) NULL,
        [BankAccountLabel] nvarchar(200) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [SourceQuoteId] uniqueidentifier NULL,
        [LinkedInvoiceId] uniqueidentifier NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [WithholdingAmount] decimal(18,3) NOT NULL,
        [WithholdingAmountCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [AmountPaid] decimal(18,3) NOT NULL,
        [AmountPaidCurrency] nvarchar(3) NOT NULL,
        [IsRecurring] bit NOT NULL CONSTRAINT [DF_HonorairesInvoices_IsRecurring] DEFAULT 0,
        [RecurrenceFrequency] int NULL,
        [PaidAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_HonorairesInvoices] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_HonorairesInvoices_FirmClientAssignmentId_IssueDate] ON [HonorairesInvoices] ([FirmClientAssignmentId], [IssueDate]);
    CREATE INDEX [IX_HonorairesInvoices_SourceQuoteId] ON [HonorairesInvoices] ([SourceQuoteId]);
    CREATE INDEX [IX_HonorairesInvoices_LinkedInvoiceId] ON [HonorairesInvoices] ([LinkedInvoiceId]);
    CREATE INDEX [IX_HonorairesInvoices_Status] ON [HonorairesInvoices] ([Status]);
    CREATE INDEX [IX_HonorairesInvoices_Type] ON [HonorairesInvoices] ([Type]);
    CREATE UNIQUE INDEX [IX_HonorairesInvoices_Number] ON [HonorairesInvoices] ([Number]) WHERE [Number] IS NOT NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[HonorairesInvoiceLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesInvoiceLines] (
        [Id] uniqueidentifier NOT NULL,
        [HonorairesInvoiceId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [Designation] nvarchar(500) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        [VatRate] int NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [DiscountAmountCurrency] nvarchar(3) NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [VatAmountCurrency] nvarchar(3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_HonorairesInvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HonorairesInvoiceLines_HonorairesInvoices_HonorairesInvoiceId]
            FOREIGN KEY ([HonorairesInvoiceId]) REFERENCES [HonorairesInvoices] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_HonorairesInvoiceLines_HonorairesInvoiceId] ON [HonorairesInvoiceLines] ([HonorairesInvoiceId]);
END;
GO

IF OBJECT_ID(N'[dbo].[HonorairesQuotes]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesQuotes] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NULL,
        [IssueDate] datetime2 NOT NULL,
        [ValidUntil] datetime2 NULL,
        [Status] int NOT NULL,
        [FirmClientAssignmentId] uniqueidentifier NOT NULL,
        [ClientName] nvarchar(256) NOT NULL,
        [ClientNif] nvarchar(50) NULL,
        [ClientAddress] nvarchar(500) NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactEmail] nvarchar(256) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [PaymentTerms] nvarchar(2000) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ConvertedInvoiceId] uniqueidentifier NULL,
        [ConvertedAt] datetime2 NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_HonorairesQuotes] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_HonorairesQuotes_FirmClientAssignmentId_IssueDate] ON [HonorairesQuotes] ([FirmClientAssignmentId], [IssueDate]);
    CREATE INDEX [IX_HonorairesQuotes_ConvertedInvoiceId] ON [HonorairesQuotes] ([ConvertedInvoiceId]);
    CREATE INDEX [IX_HonorairesQuotes_Status] ON [HonorairesQuotes] ([Status]);
    CREATE UNIQUE INDEX [IX_HonorairesQuotes_Number] ON [HonorairesQuotes] ([Number]) WHERE [Number] IS NOT NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[HonorairesQuoteLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesQuoteLines] (
        [Id] uniqueidentifier NOT NULL,
        [HonorairesQuoteId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [Designation] nvarchar(500) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        [VatRate] int NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [DiscountAmountCurrency] nvarchar(3) NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [VatAmountCurrency] nvarchar(3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_HonorairesQuoteLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HonorairesQuoteLines_HonorairesQuotes_HonorairesQuoteId]
            FOREIGN KEY ([HonorairesQuoteId]) REFERENCES [HonorairesQuotes] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_HonorairesQuoteLines_HonorairesQuoteId] ON [HonorairesQuoteLines] ([HonorairesQuoteId]);
END;
GO

IF OBJECT_ID(N'[dbo].[HonorairesPayments]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesPayments] (
        [Id] uniqueidentifier NOT NULL,
        [HonorairesInvoiceId] uniqueidentifier NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AmountCurrency] nvarchar(3) NOT NULL,
        [ClientWithholdingAmount] decimal(18,3) NOT NULL,
        [ClientWithholdingAmountCurrency] nvarchar(3) NOT NULL,
        [Method] int NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(1000) NULL,
        [BankAccountLabel] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_HonorairesPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HonorairesPayments_HonorairesInvoices_HonorairesInvoiceId]
            FOREIGN KEY ([HonorairesInvoiceId]) REFERENCES [HonorairesInvoices] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_HonorairesPayments_HonorairesInvoiceId] ON [HonorairesPayments] ([HonorairesInvoiceId]);
    CREATE INDEX [IX_HonorairesPayments_PaymentDate] ON [HonorairesPayments] ([PaymentDate]);
END;
GO

IF OBJECT_ID(N'[dbo].[HonorairesAttachments]', N'U') IS NULL
BEGIN
    CREATE TABLE [HonorairesAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentKind] int NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [StorageRelativePath] nvarchar(500) NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        [UploadedBy] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_HonorairesAttachments] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_HonorairesAttachments_DocumentKind_DocumentId] ON [HonorairesAttachments] ([DocumentKind], [DocumentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801120000_AddHonorairesModule_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801120000_AddHonorairesModule_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddHonorairesModule%';
-- SELECT OBJECT_ID('HonorairesInvoices'), OBJECT_ID('HonorairesQuotes'), OBJECT_ID('HonorairesPayments'), OBJECT_ID('HonorairesAttachments');
