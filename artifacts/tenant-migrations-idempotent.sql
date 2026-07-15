IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [UserEmail] nvarchar(256) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [IpAddress] nvarchar(50) NOT NULL,
        [UserAgent] nvarchar(500) NULL,
        [PreviousHash] nvarchar(100) NOT NULL,
        [Hash] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Clients] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Type] int NOT NULL,
        [NIF] nvarchar(20) NULL,
        [Street] nvarchar(200) NOT NULL,
        [StreetLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(20) NULL,
        [Governorate] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [ContactPerson] nvarchar(200) NULL,
        [Notes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TradeName] nvarchar(200) NULL,
        [Street] nvarchar(200) NOT NULL,
        [StreetLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(20) NULL,
        [Governorate] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [NIF] nvarchar(20) NOT NULL,
        [CommerceRegistry] nvarchar(50) NULL,
        [VatCode] nvarchar(50) NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [LogoUrl] nvarchar(500) NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [BankName] nvarchar(100) NULL,
        [Iban] nvarchar(34) NULL,
        [Rib] nvarchar(24) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [InvoiceDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [CurrentStep] int NOT NULL,
        [IsConverted] bit NOT NULL,
        [ConvertedInvoiceId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [MetadataJson] nvarchar(max) NULL,
        [SellerId] uniqueidentifier NULL,
        [ClientId] uniqueidentifier NULL,
        [NewClientJson] nvarchar(max) NULL,
        [LinesJson] nvarchar(max) NULL,
        [PaymentLegalJson] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IdempotencyKey] nvarchar(100) NULL,
        [IsSubmitting] bit NOT NULL,
        [SubmissionStartedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_InvoiceDrafts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [InvoiceNumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Prefix] nvarchar(10) NOT NULL,
        [FiscalYear] int NOT NULL,
        [CurrentSequence] int NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InvoiceNumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Type] int NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [VatRate] int NOT NULL,
        [Unit] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [QuoteNumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Prefix] nvarchar(10) NOT NULL,
        [FiscalYear] int NOT NULL,
        [CurrentSequence] int NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_QuoteNumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [PaymentTerms] nvarchar(500) NULL,
        [PaymentMethod] nvarchar(max) NULL,
        [BankName] nvarchar(max) NULL,
        [Iban] nvarchar(max) NULL,
        [Rib] nvarchar(max) NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [SignatureHash] nvarchar(500) NULL,
        [SignedAt] datetime2 NULL,
        [SignedBy] nvarchar(256) NULL,
        [SentAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [SourceQuoteId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Quotes] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [ExpiryDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [TermsAndConditions] nvarchar(2000) NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [SentAt] datetime2 NULL,
        [AcceptedAt] datetime2 NULL,
        [RejectedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [ConvertedInvoiceId] uniqueidentifier NULL,
        [ConvertedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Quotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Quotes_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [InvoiceLines] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductDescription] nvarchar(1000) NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NULL,
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
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLines_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_InvoiceLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Method] int NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [IsRefunded] bit NOT NULL,
        [RefundedAt] datetime2 NULL,
        [RefundReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE TABLE [QuoteLines] (
        [Id] uniqueidentifier NOT NULL,
        [QuoteId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductDescription] nvarchar(1000) NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NULL,
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
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_QuoteLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuoteLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_QuoteLines_Quotes_QuoteId] FOREIGN KEY ([QuoteId]) REFERENCES [Quotes] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType] ON [AuditLogs] ([EntityType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Clients_IsActive] ON [Clients] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Clients_Name] ON [Clients] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_IsActive] ON [Companies] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_IsDefault] ON [Companies] ([IsDefault]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_Name] ON [Companies] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDrafts_ClientId] ON [InvoiceDrafts] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDrafts_ExpiresAt] ON [InvoiceDrafts] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDrafts_IdempotencyKey] ON [InvoiceDrafts] ([IdempotencyKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDrafts_IsConverted] ON [InvoiceDrafts] ([IsConverted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDrafts_SellerId] ON [InvoiceDrafts] ([SellerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_InvoiceId] ON [InvoiceLines] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_ProductId] ON [InvoiceLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceNumberSequences_TenantId_Prefix_FiscalYear] ON [InvoiceNumberSequences] ([TenantId], [Prefix], [FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_ClientId] ON [Invoices] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_DueDate] ON [Invoices] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_IssueDate] ON [Invoices] ([IssueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_SourceQuoteId] ON [Invoices] ([SourceQuoteId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_Status] ON [Invoices] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate] ON [Payments] ([PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Code] ON [Products] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsActive] ON [Products] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Name] ON [Products] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_QuoteLines_ProductId] ON [QuoteLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_QuoteLines_QuoteId] ON [QuoteLines] ([QuoteId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuoteNumberSequences_TenantId_Prefix_FiscalYear] ON [QuoteNumberSequences] ([TenantId], [Prefix], [FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Quotes_ClientId] ON [Quotes] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Quotes_ConvertedInvoiceId] ON [Quotes] ([ConvertedInvoiceId]) WHERE [ConvertedInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Quotes_ExpiryDate] ON [Quotes] ([ExpiryDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Quotes_IssueDate] ON [Quotes] ([IssueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    CREATE INDEX [IX_Quotes_Status] ON [Quotes] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124230121_InitialTenantCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260124230121_InitialTenantCreate', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    ALTER TABLE [Products] ADD [IsStockManaged] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE TABLE [StockItems] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [QuantityOnHand] decimal(18,4) NOT NULL,
        [QuantityReserved] decimal(18,4) NOT NULL,
        [MinimumStock] decimal(18,4) NOT NULL,
        [MaximumStock] decimal(18,4) NULL,
        [AverageCost] decimal(18,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_StockItems] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE TABLE [Warehouses] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Address] nvarchar(500) NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] uniqueidentifier NOT NULL,
        [StockItemId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Reason] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [BalanceAfter] decimal(18,4) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [OccurredAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [StockItems] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockItems_IsActive] ON [StockItems] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockItems_ProductId] ON [StockItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockItems_ProductId_WarehouseId] ON [StockItems] ([ProductId], [WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockItems_WarehouseId] ON [StockItems] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_OccurredAt] ON [StockMovements] ([OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Reason] ON [StockMovements] ([Reason]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Reference] ON [StockMovements] ([Reference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_StockItemId] ON [StockMovements] ([StockItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_StockItemId_OccurredAt] ON [StockMovements] ([StockItemId], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Type] ON [StockMovements] ([Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_Warehouses_IsActive] ON [Warehouses] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    CREATE INDEX [IX_Warehouses_IsDefault] ON [Warehouses] ([IsDefault]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260201163911_AddStockModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260201163911_AddStockModule', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE TABLE [PhysicalInventories] (
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_PhysicalInventories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE TABLE [InventoryCountLines] (
        [Id] uniqueidentifier NOT NULL,
        [InventoryId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductCode] nvarchar(50) NULL,
        [TheoreticalQuantity] decimal(18,4) NOT NULL,
        [CountedQuantity] decimal(18,4) NULL,
        [CountedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_InventoryCountLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryCountLines_PhysicalInventories_InventoryId] FOREIGN KEY ([InventoryId]) REFERENCES [PhysicalInventories] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryCountLines_InventoryId] ON [InventoryCountLines] ([InventoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryCountLines_InventoryId_ProductId] ON [InventoryCountLines] ([InventoryId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryCountLines_ProductId] ON [InventoryCountLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_PhysicalInventories_StartedAt] ON [PhysicalInventories] ([StartedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_PhysicalInventories_Status] ON [PhysicalInventories] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_PhysicalInventories_WarehouseId] ON [PhysicalInventories] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    CREATE INDEX [IX_PhysicalInventories_WarehouseId_Status] ON [PhysicalInventories] ([WarehouseId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260206030438_AddPhysicalInventory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260206030438_AddPhysicalInventory', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE TABLE [DeliveryNotes] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [DeliveryDate] datetime2 NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [DeliveryAddress] nvarchar(500) NOT NULL,
        [DeliveryCity] nvarchar(100) NULL,
        [DeliveryPostalCode] nvarchar(20) NULL,
        [RecipientName] nvarchar(200) NULL,
        [RecipientSignature] nvarchar(max) NULL,
        [SignedAt] datetime2 NULL,
        [FailureReason] nvarchar(500) NULL,
        [FailedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [InvoiceId] uniqueidentifier NULL,
        [InvoicedAt] datetime2 NULL,
        [AllowGroupInvoicing] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_DeliveryNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryNotes_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DeliveryNotes_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE TABLE [DeliveryNoteLines] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryNoteId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [Designation] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Unit] nvarchar(50) NOT NULL,
        [UnitPriceHT] decimal(18,3) NOT NULL,
        [VatRatePercent] int NOT NULL,
        [OrderedQuantity] decimal(18,4) NOT NULL,
        [DeliveredQuantity] decimal(18,4) NOT NULL,
        [RejectedQuantity] decimal(18,4) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DeliveryNoteLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId] FOREIGN KEY ([DeliveryNoteId]) REFERENCES [DeliveryNotes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DeliveryNoteLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_ClientId] ON [DeliveryNotes] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_DeliveryDate] ON [DeliveryNotes] ([DeliveryDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_InvoiceId] ON [DeliveryNotes] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_IssueDate] ON [DeliveryNotes] ([IssueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_Status] ON [DeliveryNotes] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNoteLines_DeliveryNoteId] ON [DeliveryNoteLines] ([DeliveryNoteId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    CREATE INDEX [IX_DeliveryNoteLines_ProductId] ON [DeliveryNoteLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210002530_AddDeliveryNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210002530_AddDeliveryNotes', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211120000_FixDeliveryNoteColumns'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'ProductCode')
                        BEGIN
                            ALTER TABLE [DeliveryNoteLines] ADD [ProductCode] nvarchar(50) NOT NULL DEFAULT N'';
                        END
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211120000_FixDeliveryNoteColumns'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'UnitPriceHT')
                        BEGIN
                            ALTER TABLE [DeliveryNoteLines] ADD [UnitPriceHT] decimal(18,3) NOT NULL DEFAULT 0;
                        END
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211120000_FixDeliveryNoteColumns'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'VatRatePercent')
                        BEGIN
                            ALTER TABLE [DeliveryNoteLines] ADD [VatRatePercent] int NOT NULL DEFAULT 0;
                        END
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211120000_FixDeliveryNoteColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211120000_FixDeliveryNoteColumns', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Type] int NOT NULL,
        [NIF] nvarchar(20) NULL,
        [Street] nvarchar(200) NOT NULL,
        [StreetLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(20) NULL,
        [Governorate] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [ContactPerson] nvarchar(200) NULL,
        [PaymentTermDays] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [ExpectedDeliveryDate] datetime2 NULL,
        [Status] int NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [ConfirmedAt] datetime2 NULL,
        [ReceivedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE TABLE [PurchaseOrderLines] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductDescription] nvarchar(1000) NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        [VatRate] int NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [VatAmountCurrency] nvarchar(3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PurchaseOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrderLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_ProductId] ON [PurchaseOrderLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_PurchaseOrderId] ON [PurchaseOrderLines] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_OrderDate] ON [PurchaseOrders] ([OrderDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_Status] ON [PurchaseOrders] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [PurchaseOrders] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_Suppliers_IsActive] ON [Suppliers] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    CREATE INDEX [IX_Suppliers_Name] ON [Suppliers] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214020122_AddSuppliersAndPurchaseOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214020122_AddSuppliersAndPurchaseOrders', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE TABLE [SupplierInvoices] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceNumber] nvarchar(100) NOT NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [DueDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [ExternalReference] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [TotalVat] decimal(18,3) NOT NULL,
        [TotalVatCurrency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [TotalAmountCurrency] nvarchar(3) NOT NULL,
        [PaidAt] datetime2 NULL,
        [PaymentReference] nvarchar(200) NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_SupplierInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierInvoices_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierInvoices_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE TABLE [SupplierInvoiceLines] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierInvoiceId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [ProductDescription] nvarchar(1000) NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        [VatRate] int NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [SubTotalCurrency] nvarchar(3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [VatAmountCurrency] nvarchar(3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SupplierInvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierInvoiceLines_SupplierInvoices_SupplierInvoiceId] FOREIGN KEY ([SupplierInvoiceId]) REFERENCES [SupplierInvoices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_SupplierInvoiceId] ON [SupplierInvoiceLines] ([SupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_DueDate] ON [SupplierInvoices] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_InvoiceDate] ON [SupplierInvoices] ([InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_PurchaseOrderId] ON [SupplierInvoices] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_Status] ON [SupplierInvoices] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_SupplierId] ON [SupplierInvoices] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215150801_AddSupplierInvoices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260215150801_AddSupplierInvoices', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217233615_AddPurchaseOrderInvoicedAt'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD [InvoicedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217233615_AddPurchaseOrderInvoicedAt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260217233615_AddPurchaseOrderInvoicedAt', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260222171129_AddSupplierInvoiceNumberUniqueIndex'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupplierInvoices_InvoiceNumber] ON [SupplierInvoices] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260222171129_AddSupplierInvoiceNumberUniqueIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260222171129_AddSupplierInvoiceNumberUniqueIndex', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260224210605_BackfillPaymentsForPaidInvoices'
)
BEGIN
                    INSERT INTO Payments (Id, InvoiceId, Amount, Currency, PaymentDate, Method, Reference, Notes, IsRefunded, RefundedAt, RefundReason, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, Version)
                    SELECT
                        NEWID(),
                        i.Id,
                        i.TotalAmount,
                        ISNULL(i.TotalAmountCurrency, 'TND'),
                        i.PaidAt,
                        99,
                        NULL,
                        'Migration: paiement historique avant support multi-paiements',
                        0,
                        NULL,
                        NULL,
                        i.PaidAt,
                        NULL,
                        i.UpdatedBy,
                        NULL,
                        0
                    FROM Invoices i
                    WHERE i.Status = 4
                      AND i.PaidAt IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM Payments p WHERE p.InvoiceId = i.Id)
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260224210605_BackfillPaymentsForPaidInvoices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260224210605_BackfillPaymentsForPaidInvoices', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227161540_AddProductPurchasePrice'
)
BEGIN
    ALTER TABLE [Products] ADD [PurchasePrice] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227161540_AddProductPurchasePrice'
)
BEGIN
    ALTER TABLE [Products] ADD [PurchasePriceCurrency] nvarchar(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227161540_AddProductPurchasePrice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260227161540_AddProductPurchasePrice', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227162618_AddCommercialStatsIndexes'
)
BEGIN
    CREATE INDEX [IX_Invoices_Status_IssueDate] ON [Invoices] ([Status], [IssueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227162618_AddCommercialStatsIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260227162618_AddCommercialStatsIndexes', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303203736_AddSupplierPayments'
)
BEGIN
    DROP INDEX [IX_Invoices_Status_IssueDate] ON [Invoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303203736_AddSupplierPayments'
)
BEGIN
    CREATE TABLE [SupplierPayments] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierInvoiceId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AmountCurrency] nvarchar(3) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Method] int NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_SupplierPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierPayments_SupplierInvoices_SupplierInvoiceId] FOREIGN KEY ([SupplierInvoiceId]) REFERENCES [SupplierInvoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303203736_AddSupplierPayments'
)
BEGIN
    CREATE INDEX [IX_SupplierPayments_PaymentDate] ON [SupplierPayments] ([PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303203736_AddSupplierPayments'
)
BEGIN
    CREATE INDEX [IX_SupplierPayments_SupplierInvoiceId] ON [SupplierPayments] ([SupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303203736_AddSupplierPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260303203736_AddSupplierPayments', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304233920_AddProductCategory'
)
BEGIN
    ALTER TABLE [Products] ADD [Category] nvarchar(100) NOT NULL DEFAULT N'general';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304233920_AddProductCategory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304233920_AddProductCategory', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCategories_Code] ON [ProductCategories] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    CREATE INDEX [IX_ProductCategories_IsActive] ON [ProductCategories] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
                    INSERT INTO [ProductCategories] ([Id], [Code], [Name], [DisplayOrder], [IsActive], [CreatedAt])
                    VALUES ('11111111-1111-1111-1111-111111111111', 'GENERAL', N'General', 0, 1, GETUTCDATE());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    ALTER TABLE [Products] ADD [CategoryId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
                    UPDATE [Products] SET [CategoryId] = '11111111-1111-1111-1111-111111111111' WHERE [CategoryId] IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'CategoryId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Products] ALTER COLUMN [CategoryId] uniqueidentifier NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Category');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Products] DROP COLUMN [Category];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304235400_ReplaceProductCategoryWithProductCategoryTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304235400_ReplaceProductCategoryWithProductCategoryTable', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305223644_RemoveInvoiceSentStatus'
)
BEGIN
                    UPDATE Invoices SET Status = 2 WHERE Status = 3
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305223644_RemoveInvoiceSentStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305223644_RemoveInvoiceSentStatus', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305231813_AddProductImageUrl'
)
BEGIN
    ALTER TABLE [Products] ADD [ImageUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305231813_AddProductImageUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305231813_AddProductImageUrl', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
    ALTER TABLE [PhysicalInventories] ADD [Reference] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
    CREATE TABLE [InventoryNumberSequences] (
        [Year] int NOT NULL,
        [LastSequence] int NOT NULL,
        CONSTRAINT [PK_InventoryNumberSequences] PRIMARY KEY ([Year])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
                    WITH Ordered AS (
                        SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS Rn
                        FROM PhysicalInventories
                        WHERE Reference IS NULL
                    )
                    UPDATE p
                    SET p.Reference = 'INVE-' + RIGHT('000000' + CAST(o.Rn AS NVARCHAR(10)), 6)
                    FROM PhysicalInventories p
                    INNER JOIN Ordered o ON p.Id = o.Id
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PhysicalInventories]') AND [c].[name] = N'Reference');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PhysicalInventories] DROP CONSTRAINT [' + @var2 + '];');
    EXEC(N'UPDATE [PhysicalInventories] SET [Reference] = N'''' WHERE [Reference] IS NULL');
    ALTER TABLE [PhysicalInventories] ALTER COLUMN [Reference] nvarchar(50) NOT NULL;
    ALTER TABLE [PhysicalInventories] ADD DEFAULT N'' FOR [Reference];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
    CREATE INDEX [IX_PhysicalInventories_Reference] ON [PhysicalInventories] ([Reference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315000000_AddInventoryReferenceAndSequence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260315000000_AddInventoryReferenceAndSequence', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315100000_UniqueInventoryReference'
)
BEGIN
                    ;WITH DupRefs AS (
                        SELECT Reference FROM PhysicalInventories GROUP BY Reference HAVING COUNT(*) > 1
                    ),
                    DuplicateRows AS (
                        SELECT p.Id, p.Reference,
                               ROW_NUMBER() OVER (PARTITION BY p.Reference ORDER BY p.CreatedAt, p.Id) AS rn
                        FROM PhysicalInventories p
                        INNER JOIN DupRefs d ON p.Reference = d.Reference
                    ),
                    MaxSeq AS (
                        SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Reference, 6, 6) AS INT)), 0) AS M FROM PhysicalInventories
                    ),
                    Numbered AS (
                        SELECT dr.Id, ms.M + ROW_NUMBER() OVER (ORDER BY dr.Id) AS NewSeq
                        FROM DuplicateRows dr
                        CROSS JOIN MaxSeq ms
                        WHERE dr.rn > 1
                    )
                    UPDATE p
                    SET p.Reference = 'INVE-' + RIGHT('000000' + CAST(n.NewSeq AS NVARCHAR(10)), 6)
                    FROM PhysicalInventories p
                    INNER JOIN Numbered n ON p.Id = n.Id
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315100000_UniqueInventoryReference'
)
BEGIN
    DROP INDEX [IX_PhysicalInventories_Reference] ON [PhysicalInventories];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315100000_UniqueInventoryReference'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PhysicalInventories_Reference] ON [PhysicalInventories] ([Reference]) WHERE [Reference] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260315100000_UniqueInventoryReference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260315100000_UniqueInventoryReference', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE TABLE [CashExpenseNumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [CurrentSequence] int NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CashExpenseNumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE TABLE [CashExpenses] (
        [Id] uniqueidentifier NOT NULL,
        [ExpenseNumber] nvarchar(50) NOT NULL,
        [ExpenseNumberPrefix] nvarchar(10) NOT NULL,
        [ExpenseNumberYear] int NOT NULL,
        [ExpenseNumberSequence] int NOT NULL,
        [ExpenseDate] datetime2 NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AmountCurrency] nvarchar(3) NOT NULL,
        [Label] nvarchar(500) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_CashExpenses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashExpenseNumberSequences_TenantId_FiscalYear] ON [CashExpenseNumberSequences] ([TenantId], [FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_ExpenseDate] ON [CashExpenses] ([ExpenseDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashExpenses_ExpenseNumber] ON [CashExpenses] ([ExpenseNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_Method] ON [CashExpenses] ([Method]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_Reference] ON [CashExpenses] ([Reference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_Status] ON [CashExpenses] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320202734_AddCashDeskExpenses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320202734_AddCashDeskExpenses', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320204954_AddCashExpenseCategory'
)
BEGIN
    ALTER TABLE [CashExpenses] ADD [Category] int NOT NULL DEFAULT 99;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320204954_AddCashExpenseCategory'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_Category] ON [CashExpenses] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320204954_AddCashExpenseCategory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320204954_AddCashExpenseCategory', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320225042_AddBankAccounts'
)
BEGIN
    CREATE TABLE [BankAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BankCode] nvarchar(32) NOT NULL,
        [BankName] nvarchar(100) NOT NULL,
        [Designation] nvarchar(200) NULL,
        [AgencyName] nvarchar(200) NULL,
        [Rib] nvarchar(20) NOT NULL,
        [Iban] nvarchar(34) NOT NULL,
        [SwiftBic] nvarchar(11) NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankAccounts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320225042_AddBankAccounts'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_CompanyId] ON [BankAccounts] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320225042_AddBankAccounts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankAccounts_CompanyId_Iban] ON [BankAccounts] ([CompanyId], [Iban]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320225042_AddBankAccounts'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_IsDefault] ON [BankAccounts] ([IsDefault]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320225042_AddBankAccounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320225042_AddBankAccounts', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    ALTER TABLE [CashExpenses] ADD [OperationType] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    ALTER TABLE [CashExpenses] ADD [RevenueCategory] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CashExpenses]') AND [c].[name] = N'Category');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [CashExpenses] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [CashExpenses] ALTER COLUMN [Category] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    CREATE INDEX [IX_CashExpenses_OperationType] ON [CashExpenses] ([OperationType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    DROP INDEX [IX_CashExpenseNumberSequences_TenantId_FiscalYear] ON [CashExpenseNumberSequences];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    ALTER TABLE [CashExpenseNumberSequences] ADD [Prefix] nvarchar(10) NOT NULL DEFAULT N'DEP';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashExpenseNumberSequences_TenantId_FiscalYear_Prefix] ON [CashExpenseNumberSequences] ([TenantId], [FiscalYear], [Prefix]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321200000_AddCashOperationTypeAndRevenueCategory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321200000_AddCashOperationTypeAndRevenueCategory', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE TABLE [BankDepositNumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [CurrentSequence] int NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BankDepositNumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE TABLE [BankDeposits] (
        [Id] uniqueidentifier NOT NULL,
        [DepositNumber] nvarchar(50) NOT NULL,
        [DepositNumberYear] int NOT NULL,
        [DepositNumberSequence] int NOT NULL,
        [DepositType] int NOT NULL,
        [DepositDate] datetime2 NOT NULL,
        [BankAccountId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AmountCurrency] nvarchar(3) NOT NULL,
        [DepositSlipReference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [CashOperationId] uniqueidentifier NOT NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_BankDeposits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankDeposits_BankAccounts_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BankDeposits_CashExpenses_CashOperationId] FOREIGN KEY ([CashOperationId]) REFERENCES [CashExpenses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankDepositNumberSequences_TenantId_FiscalYear] ON [BankDepositNumberSequences] ([TenantId], [FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE INDEX [IX_BankDeposits_BankAccountId] ON [BankDeposits] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE INDEX [IX_BankDeposits_CashOperationId] ON [BankDeposits] ([CashOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE INDEX [IX_BankDeposits_DepositDate] ON [BankDeposits] ([DepositDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE INDEX [IX_BankDeposits_DepositDate_Status] ON [BankDeposits] ([DepositDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankDeposits_DepositNumber] ON [BankDeposits] ([DepositNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321224820_AddBankDeposits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321224820_AddBankDeposits', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    CREATE TABLE [Taxes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [TaxType] int NOT NULL,
        [ValueType] int NOT NULL,
        [Value] decimal(18,3) NOT NULL,
        [ApplicableContext] int NOT NULL,
        [IsAppliedToProducts] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Taxes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    CREATE INDEX [IX_Taxes_DisplayOrder] ON [Taxes] ([DisplayOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    CREATE INDEX [IX_Taxes_IsActive] ON [Taxes] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    CREATE INDEX [IX_Taxes_TaxType] ON [Taxes] ([TaxType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    INSERT INTO [Taxes] ([Id],[Name],[TaxType],[ValueType],[Value],[ApplicableContext],[IsAppliedToProducts],[IsActive],[IsSystem],[DisplayOrder],[CreatedAt],[UpdatedAt],[CreatedBy],[UpdatedBy],[Version])
    VALUES
    ('11111111-1111-4111-8111-111111110001', N'TVA 0 %', 0, 0, 0, 0, 0, 1, 1, 0, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
    ('11111111-1111-4111-8111-111111110007', N'TVA 7 %', 0, 0, 7, 0, 0, 1, 1, 1, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
    ('11111111-1111-4111-8111-111111110013', N'TVA 13 %', 0, 0, 13, 0, 0, 1, 1, 2, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
    ('11111111-1111-4111-8111-111111110019', N'TVA 19 %', 0, 0, 19, 0, 1, 1, 1, 3, SYSUTCDATETIME(), NULL, NULL, NULL, 1),
    ('22222222-2222-4222-8222-222222220001', N'Timbre fiscal', 1, 1, 1.000, 0, 0, 1, 1, 10, SYSUTCDATETIME(), NULL, NULL, NULL, 1);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321231924_AddTaxes_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321231924_AddTaxes_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [WarehouseId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD [WarehouseId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [WarehouseId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [DeliveryNotes] ADD [WarehouseId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE TABLE [StockTransfers] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [NumberPrefix] nvarchar(10) NOT NULL,
        [NumberYear] int NOT NULL,
        [NumberSequence] int NOT NULL,
        [TransferDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [SourceWarehouseId] uniqueidentifier NOT NULL,
        [DestinationWarehouseId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(2000) NULL,
        [ConfirmedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_StockTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockTransfers_Warehouses_DestinationWarehouseId] FOREIGN KEY ([DestinationWarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_Warehouses_SourceWarehouseId] FOREIGN KEY ([SourceWarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE TABLE [StockTransferLines] (
        [Id] uniqueidentifier NOT NULL,
        [StockTransferId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [RequestedQuantity] decimal(18,3) NOT NULL,
        [TransferredQuantity] decimal(18,3) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StockTransferLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockTransferLines_StockTransfers_StockTransferId] FOREIGN KEY ([StockTransferId]) REFERENCES [StockTransfers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_WarehouseId] ON [SupplierInvoices] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_WarehouseId] ON [PurchaseOrders] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_Invoices_WarehouseId] ON [Invoices] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_DeliveryNotes_WarehouseId] ON [DeliveryNotes] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransferLines_ProductId] ON [StockTransferLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransferLines_StockTransferId] ON [StockTransferLines] ([StockTransferId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_DestinationWarehouseId] ON [StockTransfers] ([DestinationWarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_SourceWarehouseId] ON [StockTransfers] ([SourceWarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_Status] ON [StockTransfers] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_TransferDate] ON [StockTransfers] ([TransferDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [DeliveryNotes] ADD CONSTRAINT [FK_DeliveryNotes_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD CONSTRAINT [FK_PurchaseOrders_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD CONSTRAINT [FK_SupplierInvoices_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322162014_AddMultiWarehouseSchema_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260322162014_AddMultiWarehouseSchema_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [AccountingPeriods] (
        [Id] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [Month] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsClosed] bit NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosedBy] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingPeriods] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [ChartOfAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [AccountNumber] nvarchar(32) NOT NULL,
        [Label] nvarchar(300) NOT NULL,
        [AccountClass] int NOT NULL,
        [ParentAccountNumber] nvarchar(32) NULL,
        [NatureType] int NOT NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [Level] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChartOfAccounts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [JournalEntrySequences] (
        [Id] uniqueidentifier NOT NULL,
        [JournalCode] nvarchar(10) NOT NULL,
        [FiscalYear] int NOT NULL,
        [LastSequence] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_JournalEntrySequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [LetteringGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(16) NOT NULL,
        [AccountNumber] nvarchar(32) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [LetteredAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_LetteringGroups] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [VatDeclarations] (
        [Id] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [CollectedVat19] decimal(18,3) NOT NULL,
        [CollectedVat19Currency] nvarchar(3) NOT NULL,
        [CollectedVat13] decimal(18,3) NOT NULL,
        [CollectedVat13Currency] nvarchar(3) NOT NULL,
        [CollectedVat7] decimal(18,3) NOT NULL,
        [CollectedVat7Currency] nvarchar(3) NOT NULL,
        [DeductibleVatGoods] decimal(18,3) NOT NULL,
        [DeductibleVatGoodsCurrency] nvarchar(3) NOT NULL,
        [DeductibleVatAssets] decimal(18,3) NOT NULL,
        [DeductibleVatAssetsCurrency] nvarchar(3) NOT NULL,
        [PreviousCredit] decimal(18,3) NOT NULL,
        [PreviousCreditCurrency] nvarchar(3) NOT NULL,
        [VatDue] decimal(18,3) NOT NULL,
        [VatDueCurrency] nvarchar(3) NOT NULL,
        [CreditToCarry] decimal(18,3) NOT NULL,
        [CreditToCarryCurrency] nvarchar(3) NOT NULL,
        [Status] int NOT NULL,
        [SubmittedAt] datetime2 NULL,
        [LockedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_VatDeclarations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] uniqueidentifier NOT NULL,
        [EntryNumber] int NOT NULL,
        [JournalCode] nvarchar(10) NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [Label] nvarchar(500) NOT NULL,
        [SourceEntityType] nvarchar(80) NULL,
        [SourceEntityId] uniqueidentifier NULL,
        [IsAutoGenerated] bit NOT NULL,
        [AccountingPeriodId] uniqueidentifier NOT NULL,
        [IsReversed] bit NOT NULL,
        [ReversedByEntryId] uniqueidentifier NULL,
        [ReversesEntryId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntries_AccountingPeriods_AccountingPeriodId] FOREIGN KEY ([AccountingPeriodId]) REFERENCES [AccountingPeriods] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [LetteringGroupMembers] (
        [Id] uniqueidentifier NOT NULL,
        [LetteringGroupId] uniqueidentifier NOT NULL,
        [JournalEntryLineId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_LetteringGroupMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LetteringGroupMembers_LetteringGroups_LetteringGroupId] FOREIGN KEY ([LetteringGroupId]) REFERENCES [LetteringGroups] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE TABLE [JournalEntryLines] (
        [Id] uniqueidentifier NOT NULL,
        [JournalEntryId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [AccountNumber] nvarchar(32) NOT NULL,
        [Label] nvarchar(500) NOT NULL,
        [DebitAmount] decimal(18,3) NOT NULL,
        [DebitCurrency] nvarchar(3) NOT NULL,
        [CreditAmount] decimal(18,3) NOT NULL,
        [CreditCurrency] nvarchar(3) NOT NULL,
        [LetteringCode] nvarchar(16) NULL,
        [ThirdPartyId] uniqueidentifier NULL,
        [ThirdPartyKind] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_JournalEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AccountingPeriods_FiscalYear_Month] ON [AccountingPeriods] ([FiscalYear], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChartOfAccounts_AccountNumber] ON [ChartOfAccounts] ([AccountNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_AccountingPeriodId] ON [JournalEntries] ([AccountingPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_JournalCode_EntryNumber_EntryDate] ON [JournalEntries] ([JournalCode], [EntryNumber], [EntryDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_SourceEntityType_SourceEntityId] ON [JournalEntries] ([SourceEntityType], [SourceEntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_AccountNumber] ON [JournalEntryLines] ([AccountNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [JournalEntryLines] ([JournalEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntrySequences_JournalCode_FiscalYear] ON [JournalEntrySequences] ([JournalCode], [FiscalYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_LetteringGroupMembers_JournalEntryLineId] ON [LetteringGroupMembers] ([JournalEntryLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_LetteringGroupMembers_LetteringGroupId] ON [LetteringGroupMembers] ([LetteringGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VatDeclarations_Year_Month] ON [VatDeclarations] ([Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111001'', N''401'', N''Fournisseurs d''''exploitation'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111002'', N''4011'', N''Fournisseurs - achats'', 4, N''401'', 1, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111003'', N''411'', N''Clients'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111004'', N''4111'', N''Clients - ventes'', 4, N''411'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111005'', N''4341'', N''Retenue à la source'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111006'', N''43651'', N''TVA à payer'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111007'', N''43666'', N''TVA déductible sur biens et services'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111008'', N''43662'', N''TVA déductible sur immobilisations'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111009'', N''43667'', N''Crédit de TVA à reporter'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111010'', N''43671'', N''TVA collectée'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111011'', N''436711'', N''TVA collectée sur débits'', 4, N''43671'', 1, CAST(1 AS bit), CAST(1 AS bit), 6, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111012'', N''436712'', N''TVA collectée sur encaissements'', 4, N''43671'', 1, CAST(1 AS bit), CAST(1 AS bit), 6, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111013'', N''4368'', N''Taxes sur le CA à régulariser'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111014'', N''532'', N''Banques'', 5, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111015'', N''5321'', N''Banques - comptes en dinars'', 5, N''532'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111016'', N''5324'', N''Banques - comptes en devises'', 5, N''532'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111017'', N''541'', N''Caisse siège social'', 5, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111018'', N''5411'', N''Caisse en dinars'', 5, N''541'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111019'', N''607'', N''Achats de marchandises'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111020'', N''701'', N''Ventes de produits finis'', 7, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111021'', N''705'', N''Prestations de services'', 7, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111022'', N''707'', N''Ventes de marchandises'', 7, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111023'', N''6611'', N''Taxe de formation professionnelle (TFP)'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111024'', N''6612'', N''FOPROLOS'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111025'', N''6654'', N''Droits d''''enregistrement et de timbre'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111026'', N''691'', N''Impôts sur les bénéfices'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111027'', N''45311'', N''CNSS'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111028'', N''101'', N''Capital social'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111111029'', N''131'', N''Résultat bénéficiaire'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000030'', N''10'', N''Capital et réserves assimilées'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000031'', N''102'', N''Capital appelé non versé'', 1, N''10'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000032'', N''105'', N''Primes liées au capital'', 1, N''10'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000033'', N''106'', N''Écarts de réévaluation'', 1, N''10'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000034'', N''108'', N''Comptes de l''''exploitant'', 1, N''10'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000035'', N''109'', N''Actionnaires : capital souscrit non appelé'', 1, N''10'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000036'', N''11'', N''Réserves'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000037'', N''12'', N''Report à nouveau'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000038'', N''13'', N''Résultat net de l''''exercice'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000039'', N''135'', N''Résultat net : perte'', 1, N''13'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000040'', N''14'', N''Subventions d''''investissement'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000041'', N''15'', N''Provisions réglementées'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000042'', N''16'', N''Emprunts et dettes assimilées'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000043'', N''17'', N''Dettes de crédit-bail et assimilées'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000044'', N''18'', N''Comptes de liaison des établissements'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000045'', N''19'', N''Provisions pour risques et charges'', 1, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000046'', N''20'', N''Immobilisations incorporelles'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000047'', N''21'', N''Immobilisations corporelles'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000048'', N''211'', N''Terrains'', 2, N''21'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000049'', N''212'', N''Constructions'', 2, N''21'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000050'', N''213'', N''Installations techniques'', 2, N''21'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000051'', N''218'', N''Autres immobilisations corporelles'', 2, N''21'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000052'', N''22'', N''Immobilisations mises en concession'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000053'', N''23'', N''Immobilisations en cours'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000054'', N''231'', N''Immobilisations corporelles en cours'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000055'', N''232'', N''Immobilisations incorporelles en cours'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000056'', N''233'', N''Immobilisations financières en cours'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000057'', N''235'', N''Immobilisations incorporelles'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000058'', N''2351'', N''Frais de développement'', 2, N''235'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000059'', N''237'', N''Avances et acomptes sur immobilisations'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000060'', N''238'', N''Avances et acomptes versés sur commandes'', 2, N''23'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000061'', N''24'', N''Immobilisations financières'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000062'', N''25'', N''Titres immobilisés'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000063'', N''251'', N''Titres du portefeuille d''''immobilisation'', 2, N''25'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000064'', N''252'', N''Titres immobilisés de l''''activité de portefeuille'', 2, N''25'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000065'', N''258'', N''Titres immobilisés autres'', 2, N''25'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000066'', N''26'', N''Participations et créances rattachées'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000067'', N''27'', N''Autres immobilisations financières'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000068'', N''28'', N''Amortissements des immobilisations'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000069'', N''281'', N''Amortissements des immobilisations corporelles'', 2, N''28'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000070'', N''29'', N''Provisions pour dépréciation des immobilisations'', 2, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000071'', N''31'', N''Matières premières et fournitures'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000072'', N''311'', N''Matières premières'', 3, N''31'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000073'', N''312'', N''Matières et fournitures consommables'', 3, N''31'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000074'', N''313'', N''Emballages'', 3, N''31'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000075'', N''32'', N''Autres approvisionnements'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000076'', N''33'', N''En-cours de production de biens'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000077'', N''34'', N''En-cours de production de services'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000078'', N''35'', N''Stocks de produits'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000079'', N''36'', N''Stocks provenant d''''immobilisations'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000080'', N''37'', N''Stocks de marchandises'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000081'', N''38'', N''Provisions pour dépréciation des stocks'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000082'', N''39'', N''Provisions pour dépréciation des en-cours'', 3, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000083'', N''40'', N''Fournisseurs et comptes rattachés'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000084'', N''403'', N''Fournisseurs - effets à payer'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000085'', N''404'', N''Fournisseurs d''''immobilisations'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000086'', N''405'', N''Fournisseurs de biens et services'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000087'', N''406'', N''Fournisseurs - factures non parvenues'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000088'', N''407'', N''Fournisseurs - autres avoirs'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000089'', N''408'', N''Fournisseurs - autres dettes'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000090'', N''409'', N''Fournisseurs - rabais, remises, ristournes'', 4, N''40'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000091'', N''41'', N''Clients et comptes rattachés'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000092'', N''412'', N''Clients - effets à recevoir'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000093'', N''413'', N''Clients - autres avoirs'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000094'', N''414'', N''Clients - créances douteuses'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000095'', N''415'', N''Clients - autres créances'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000096'', N''416'', N''Clients - factures à établir'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000097'', N''417'', N''Clients - produits à recevoir'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000098'', N''418'', N''Clients - autres avoirs à recevoir'', 4, N''41'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000099'', N''42'', N''Personnel et comptes rattachés'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000100'', N''43'', N''Sécurité sociale et autres organismes sociaux'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000101'', N''431'', N''Sécurité sociale'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000102'', N''432'', N''Autres organismes sociaux'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000103'', N''433'', N''Caisse de retraite'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000104'', N''435'', N''Charges sociales à payer'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000105'', N''437'', N''Autres charges sociales'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000106'', N''438'', N''Organismes sociaux - autres'', 4, N''43'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000107'', N''44'', N''État et autres collectivités publiques'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000108'', N''441'', N''État - subventions à recevoir'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000109'', N''442'', N''État - impôts et taxes recouvrables'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000110'', N''443'', N''État - TVA due'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000111'', N''444'', N''État - autres impôts'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000112'', N''445'', N''État - autres créances'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000113'', N''446'', N''État - autres dettes'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000114'', N''447'', N''État - autres comptes'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000115'', N''448'', N''État - charges à payer'', 4, N''44'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000116'', N''45'', N''Groupe et associés'', 4, NULL, 1, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000117'', N''451'', N''Groupe - comptes courants'', 4, N''45'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000118'', N''452'', N''Associés - comptes courants'', 4, N''45'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000119'', N''455'', N''Associés - opérations courantes'', 4, N''45'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000120'', N''456'', N''Associés - dividendes à payer'', 4, N''45'', 1, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000121'', N''46'', N''Débiteurs et créditeurs divers'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000122'', N''47'', N''Comptes de régularisation'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000123'', N''48'', N''Comptes de répartition périodique des charges'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000124'', N''49'', N''Provisions pour dépréciation des comptes de tiers'', 4, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000125'', N''51'', N''Valeurs mobilières de placement'', 5, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000126'', N''511'', N''Titres du portefeuille de placement'', 5, N''51'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000127'', N''512'', N''Titres à court terme'', 5, N''51'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000128'', N''52'', N''Instruments de trésorerie'', 5, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000129'', N''53'', N''Banques, établissements financiers et caisses'', 5, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000130'', N''531'', N''Caisse siège social'', 5, N''53'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000131'', N''533'', N''Caisse succursales'', 5, N''53'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000132'', N''534'', N''Régies d''''avances et d''''accréditifs'', 5, N''53'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000133'', N''535'', N''Virements internes'', 5, N''53'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000134'', N''536'', N''Chèques postaux'', 5, N''53'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000135'', N''60'', N''Achats'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000136'', N''601'', N''Achats stockés - Matières premières'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000137'', N''602'', N''Achats stockés - Autres approvisionnements'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000138'', N''603'', N''Variations des stocks'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000139'', N''604'', N''Achats d''''études et prestations de services'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000140'', N''605'', N''Achats de matériel, équipements et travaux'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000141'', N''606'', N''Achats non stockés de matières et fournitures'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000142'', N''608'', N''Frais accessoires sur achats'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000143'', N''609'', N''Rabais, remises et ristournes obtenus sur achats'', 6, N''60'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000144'', N''61'', N''Services extérieurs'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000145'', N''611'', N''Sous-traitance générale'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000146'', N''612'', N''Redevances de crédit-bail'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000147'', N''613'', N''Locations'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000148'', N''614'', N''Charges locatives et de copropriété'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000149'', N''615'', N''Entretien et réparations'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000150'', N''616'', N''Primes d''''assurances'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000151'', N''617'', N''Études et recherches'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000152'', N''618'', N''Divers'', 6, N''61'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000153'', N''62'', N''Autres services extérieurs'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000154'', N''63'', N''Impôts, taxes et versements assimilés'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000155'', N''64'', N''Charges de personnel'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000156'', N''651'', N''Redevances pour concessions, brevets, licences'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000157'', N''652'', N''Jetons de présence'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000158'', N''653'', N''Rémunérations d''''intermédiaires et honoraires'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000159'', N''654'', N''Pertes sur créances irrécouvrables'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000160'', N''655'', N''Quotes-parts de résultat sur opérations faites en commun'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000161'', N''656'', N''Charges de personnel externalisé'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000162'', N''657'', N''Autres charges de personnel'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000163'', N''658'', N''Charges diverses de gestion courante'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000164'', N''659'', N''Charges exceptionnelles'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000165'', N''66'', N''Charges financières'', 6, NULL, 0, CAST(1 AS bit), CAST(1 AS bit), 2, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000166'', N''661'', N''Charges d''''intérêts'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000167'', N''6613'', N''Pertes de change'', 6, N''661'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000168'', N''6614'', N''Escomptes accordés'', 6, N''661'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000169'', N''6615'', N''Charges assimilées'', 6, N''661'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000170'', N''662'', N''Pertes sur créances liées à des participations'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000171'', N''663'', N''Pertes sur titres de placement'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000172'', N''664'', N''Pertes sur instruments de trésorerie'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000173'', N''665'', N''Charges exceptionnelles financières'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000174'', N''6651'', N''Pénalités et amendes'', 6, N''665'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000175'', N''6652'', N''Divers'', 6, N''665'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000176'', N''6653'', N''Charges sur opérations de gestion'', 6, N''665'', 0, CAST(1 AS bit), CAST(1 AS bit), 4, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000177'', N''666'', N''Dotations aux amortissements financiers'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000178'', N''667'', N''Dotations aux provisions financières'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000179'', N''668'', N''Autres charges financières'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] ON;
    EXEC(N'INSERT INTO [ChartOfAccounts] ([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], [IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES (''11111111-1111-1111-1111-111111000180'', N''669'', N''Charges financières de gestion courante'', 6, N''66'', 0, CAST(1 AS bit), CAST(1 AS bit), 3, ''2026-01-01T00:00:00.0000000Z'', NULL, N''system'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Label', N'AccountClass', N'ParentAccountNumber', N'NatureType', N'IsSystem', N'IsActive', N'Level', N'CreatedAt', N'UpdatedAt', N'CreatedBy', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ChartOfAccounts]'))
        SET IDENTITY_INSERT [ChartOfAccounts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326233329_AddAccountingModule_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326233329_AddAccountingModule_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [AssignedUserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [AssignedUserName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE TABLE [Opportunities] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [AssignedUserId] uniqueidentifier NOT NULL,
        [AssignedUserName] nvarchar(200) NOT NULL,
        [Stage] int NOT NULL,
        [ExpectedAmount] decimal(18,3) NOT NULL,
        [ExpectedAmountCurrency] nvarchar(3) NOT NULL,
        [Probability] int NOT NULL,
        [ExpectedCloseDate] datetime2 NOT NULL,
        [ActualCloseDate] datetime2 NULL,
        [LostReason] nvarchar(1000) NULL,
        [LinkedQuoteId] uniqueidentifier NULL,
        [LinkedInvoiceId] uniqueidentifier NULL,
        [Notes] nvarchar(2000) NULL,
        [Source] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Opportunities] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE TABLE [QuoteTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(300) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [DefaultNotes] nvarchar(2000) NULL,
        [DefaultTermsAndConditions] nvarchar(4000) NULL,
        [DefaultValidityDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [UsageCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_QuoteTemplates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE TABLE [SalesActivities] (
        [Id] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [OpportunityId] uniqueidentifier NULL,
        [AssignedUserId] uniqueidentifier NOT NULL,
        [AssignedUserName] nvarchar(200) NOT NULL,
        [DueDate] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Priority] int NOT NULL,
        [ReminderDate] datetime2 NULL,
        [LinkedEntityType] nvarchar(100) NULL,
        [LinkedEntityId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SalesActivities] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE TABLE [SalesTargets] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [UserName] nvarchar(200) NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [TargetAmount] decimal(18,3) NOT NULL,
        [TargetAmountCurrency] nvarchar(3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SalesTargets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE TABLE [QuoteTemplateLines] (
        [Id] uniqueidentifier NOT NULL,
        [QuoteTemplateId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [CustomUnitPrice] decimal(18,3) NULL,
        [CustomUnitPriceCurrency] nvarchar(3) NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_QuoteTemplateLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuoteTemplateLines_QuoteTemplates_QuoteTemplateId] FOREIGN KEY ([QuoteTemplateId]) REFERENCES [QuoteTemplates] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_Clients_AssignedUserId] ON [Clients] ([AssignedUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_Opportunities_AssignedUserId] ON [Opportunities] ([AssignedUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_Opportunities_ClientId] ON [Opportunities] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_Opportunities_Stage] ON [Opportunities] ([Stage]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_QuoteTemplateLines_ProductId] ON [QuoteTemplateLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_QuoteTemplateLines_QuoteTemplateId] ON [QuoteTemplateLines] ([QuoteTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_SalesActivities_AssignedUserId] ON [SalesActivities] ([AssignedUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_SalesActivities_ClientId] ON [SalesActivities] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_SalesActivities_DueDate] ON [SalesActivities] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE INDEX [IX_SalesActivities_OpportunityId] ON [SalesActivities] ([OpportunityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesTargets_UserId_Year_Month] ON [SalesTargets] ([UserId], [Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328022433_AddClientAssignedUserColumns_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260328022433_AddClientAssignedUserColumns_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [Activity] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [CountryCode] nvarchar(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [DateOfBirth] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [DefaultWithholdingRate] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [DefaultWithholdingTaxTypeId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [IsResident] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [IsSubjectToWithholding] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [TejIdentificationType] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [IsSubjectToWithholding] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [NetAmountAfterWithholding] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [WithholdingAmount] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [WithholdingCertificateId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [WithholdingRate] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [WithholdingTaxTypeId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Companies] ADD [EstablishmentCode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Companies] ADD [TejAdherentSince] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Companies] ADD [TejCategory] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [Activity] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [CountryCode] nvarchar(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [DateOfBirth] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [IsResident] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    ALTER TABLE [Clients] ADD [TejIdentificationType] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE TABLE [WithholdingTaxCertificates] (
        [Id] uniqueidentifier NOT NULL,
        [CertificateNumber] nvarchar(50) NOT NULL,
        [DeclarantCompanyId] uniqueidentifier NOT NULL,
        [BeneficiaryType] int NOT NULL,
        [BeneficiaryId] uniqueidentifier NULL,
        [BeneficiaryName] nvarchar(300) NOT NULL,
        [IdentificationType] int NOT NULL,
        [IdentificationNumber] nvarchar(50) NOT NULL,
        [BeneficiaryCategory] int NOT NULL,
        [IsResident] bit NOT NULL,
        [CountryCode] nvarchar(3) NULL,
        [DateOfBirth] datetime2 NULL,
        [BeneficiaryAddress] nvarchar(500) NULL,
        [BeneficiaryEmail] nvarchar(256) NULL,
        [BeneficiaryPhone] nvarchar(20) NULL,
        [BeneficiaryActivity] nvarchar(200) NULL,
        [PaymentDate] datetime2 NOT NULL,
        [BillingYear] int NOT NULL,
        [HasCNPC] bit NOT NULL,
        [HasPriseEnCharge] bit NOT NULL,
        [TotalAmountHT] decimal(18,3) NOT NULL,
        [TotalAmountTVA] decimal(18,3) NOT NULL,
        [TotalAmountTTC] decimal(18,3) NOT NULL,
        [TotalAmountWithheld] decimal(18,3) NOT NULL,
        [TotalNetPaid] decimal(18,3) NOT NULL,
        [Status] int NOT NULL,
        [TejSubmissionId] nvarchar(100) NULL,
        [SourceInvoiceId] uniqueidentifier NULL,
        [SourceSupplierInvoiceId] uniqueidentifier NULL,
        [SourcePaymentId] uniqueidentifier NULL,
        [ValidatedAt] datetime2 NULL,
        [ValidatedBy] nvarchar(256) NULL,
        [SubmittedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [SignatureHash] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_WithholdingTaxCertificates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE TABLE [WithholdingTaxTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Category] int NOT NULL,
        [Label] nvarchar(300) NOT NULL,
        [LabelAr] nvarchar(300) NULL,
        [DefaultRate] decimal(5,2) NOT NULL,
        [ArticleReference] nvarchar(100) NULL,
        [ApplicableToResident] bit NOT NULL,
        [ApplicableToNonResident] bit NOT NULL,
        [MinimumThreshold] decimal(18,3) NULL,
        [IsActive] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_WithholdingTaxTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE TABLE [WithholdingTaxCertificateLines] (
        [Id] uniqueidentifier NOT NULL,
        [CertificateId] uniqueidentifier NOT NULL,
        [OperationCode] nvarchar(20) NOT NULL,
        [WithholdingTaxTypeId] uniqueidentifier NOT NULL,
        [AmountHT] decimal(18,3) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [AmountTVA] decimal(18,3) NOT NULL,
        [AmountTTC] decimal(18,3) NOT NULL,
        [WithholdingRate] decimal(5,2) NOT NULL,
        [AmountWithheld] decimal(18,3) NOT NULL,
        [NetAmountPaid] decimal(18,3) NOT NULL,
        [Currency] nvarchar(3) NULL,
        [ExchangeRate] decimal(18,6) NULL,
        [AmountWithheldForeignCurrency] decimal(18,3) NULL,
        [AmountTTCForeignCurrency] decimal(18,3) NULL,
        [NetAmountPaidForeignCurrency] decimal(18,3) NULL,
        [AdditionalTaxesJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WithholdingTaxCertificateLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WithholdingTaxCertificateLines_WithholdingTaxCertificates_CertificateId] FOREIGN KEY ([CertificateId]) REFERENCES [WithholdingTaxCertificates] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificateLines_CertificateId] ON [WithholdingTaxCertificateLines] ([CertificateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificateLines_WithholdingTaxTypeId] ON [WithholdingTaxCertificateLines] ([WithholdingTaxTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_BeneficiaryId] ON [WithholdingTaxCertificates] ([BeneficiaryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_CertificateNumber] ON [WithholdingTaxCertificates] ([CertificateNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_DeclarantCompanyId] ON [WithholdingTaxCertificates] ([DeclarantCompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_PaymentDate] ON [WithholdingTaxCertificates] ([PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_PaymentDate_Status] ON [WithholdingTaxCertificates] ([PaymentDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_SourceSupplierInvoiceId] ON [WithholdingTaxCertificates] ([SourceSupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxCertificates_Status] ON [WithholdingTaxCertificates] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxTypes_Category] ON [WithholdingTaxTypes] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WithholdingTaxTypes_Code] ON [WithholdingTaxTypes] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxTypes_DisplayOrder] ON [WithholdingTaxTypes] ([DisplayOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    CREATE INDEX [IX_WithholdingTaxTypes_IsActive] ON [WithholdingTaxTypes] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401013029_AddWithholdingTaxModule_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260401013029_AddWithholdingTaxModule_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401022000_AddTejXmlExportLogs_Tenant'
)
BEGIN
    CREATE TABLE [TejXmlExportLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [SubmissionType] int NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [Sha256Hex] nvarchar(64) NOT NULL,
        [CertificateCount] int NOT NULL,
        [IsValid] bit NOT NULL,
        [ValidationErrorSummary] nvarchar(4000) NULL,
        [ExportedByEmail] nvarchar(320) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TejXmlExportLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401022000_AddTejXmlExportLogs_Tenant'
)
BEGIN
    CREATE INDEX [IX_TejXmlExportLogs_CreatedAt] ON [TejXmlExportLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401022000_AddTejXmlExportLogs_Tenant'
)
BEGIN
    CREATE INDEX [IX_TejXmlExportLogs_Year_Month] ON [TejXmlExportLogs] ([Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260401022000_AddTejXmlExportLogs_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260401022000_AddTejXmlExportLogs_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402120000_AddPaymentClientWithholdingAmount_Tenant'
)
BEGIN
    ALTER TABLE [Payments] ADD [ClientWithholdingAmount] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260402120000_AddPaymentClientWithholdingAmount_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260402120000_AddPaymentClientWithholdingAmount_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [ElectronicInvoiceSentAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [ElectronicInvoiceTtn] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [IssuerCompanyId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant'
)
BEGIN
    CREATE INDEX [IX_Invoices_IssuerCompanyId] ON [Invoices] ([IssuerCompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260403140000_AddInvoiceIssuerAndElectronicInvoice_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403220000_AddWithholdingFiscalYearParameters_Tenant'
)
BEGIN
    CREATE TABLE [WithholdingFiscalYearParameters] (
        [Id] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [Rs7TtcThresholdTnd] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WithholdingFiscalYearParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403220000_AddWithholdingFiscalYearParameters_Tenant'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WithholdingFiscalYearParameters_FiscalYear] ON [WithholdingFiscalYearParameters] ([FiscalYear]) WHERE [FiscalYear] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260403220000_AddWithholdingFiscalYearParameters_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260403220000_AddWithholdingFiscalYearParameters_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404000000_RemoveWithholdingTaxCertificates_Tenant'
)
BEGIN
    DROP TABLE [WithholdingTaxCertificateLines];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404000000_RemoveWithholdingTaxCertificates_Tenant'
)
BEGIN
    DROP TABLE [WithholdingTaxCertificates];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404000000_RemoveWithholdingTaxCertificates_Tenant'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierInvoices]') AND [c].[name] = N'WithholdingCertificateId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [SupplierInvoices] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [SupplierInvoices] DROP COLUMN [WithholdingCertificateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260404000000_RemoveWithholdingTaxCertificates_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260404000000_RemoveWithholdingTaxCertificates_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407000000_AddInvoiceFiscalStamp_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [FiscalStampAmount] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407000000_AddInvoiceFiscalStamp_Tenant'
)
BEGIN
    ALTER TABLE [Invoices] ADD [FiscalStampCurrency] nvarchar(3) NOT NULL DEFAULT N'TND';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407000000_AddInvoiceFiscalStamp_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407000000_AddInvoiceFiscalStamp_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407100000_AddSupplierInvoiceFiscalStamp_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [FiscalStampAmount] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407100000_AddSupplierInvoiceFiscalStamp_Tenant'
)
BEGIN
    ALTER TABLE [SupplierInvoices] ADD [FiscalStampCurrency] nvarchar(3) NOT NULL DEFAULT N'TND';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407100000_AddSupplierInvoiceFiscalStamp_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407100000_AddSupplierInvoiceFiscalStamp_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [LastMessageAt] datetime2 NOT NULL,
        [SelectedModel] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    CREATE TABLE [ConversationMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        [ToolName] nvarchar(100) NULL,
        [ToolCallId] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_ConversationMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    CREATE INDEX [IX_Conversations_UserId] ON [Conversations] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    CREATE INDEX [IX_Conversations_LastMessageAt] ON [Conversations] ([LastMessageAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    CREATE INDEX [IX_ConversationMessages_ConversationId_SortOrder] ON [ConversationMessages] ([ConversationId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407120000_AddAiConversations_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407120000_AddAiConversations_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Conversations]') AND [c].[name] = N'SelectedModel');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Conversations] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Conversations] ALTER COLUMN [SelectedModel] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConversationMessages]') AND [c].[name] = N'ToolCallId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ConversationMessages] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [ConversationMessages] ALTER COLUMN [ToolCallId] nvarchar(128) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    ALTER TABLE [ConversationMessages] ADD [ToolCallsJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    CREATE TABLE [TenantAiProviders] (
        [Id] uniqueidentifier NOT NULL,
        [ProviderKey] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NULL,
        [BaseUrl] nvarchar(500) NULL,
        [EncryptedApiKey] nvarchar(4000) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [LastValidatedAtUtc] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_TenantAiProviders] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TenantAiProviders_ProviderKey] ON [TenantAiProviders] ([ProviderKey]) WHERE [ProviderKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408120000_AddTenantAiProvidersAndMessageToolCalls_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE TABLE [ChannelIdentityLinks] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [ExternalUserId] nvarchar(128) NOT NULL,
        [ExternalChatId] nvarchar(128) NOT NULL,
        [VerifiedAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelIdentityLinks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE TABLE [ChannelInboundMessageLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [ExternalMessageId] nvarchar(150) NOT NULL,
        [ExternalUserId] nvarchar(128) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TraceId] nvarchar(120) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelInboundMessageLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE TABLE [ChannelLinkCodes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ChannelType] int NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [AttemptCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelLinkCodes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_ChannelType_ExternalUserId] ON [ChannelIdentityLinks] ([ChannelType], [ExternalUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE INDEX [IX_ChannelIdentityLinks_IsActive] ON [ChannelIdentityLinks] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_UserId_ChannelType] ON [ChannelIdentityLinks] ([UserId], [ChannelType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId] ON [ChannelInboundMessageLogs] ([ChannelType], [ExternalMessageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE INDEX [IX_ChannelInboundMessageLogs_UserId] ON [ChannelInboundMessageLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_ChannelType_CodeHash] ON [ChannelLinkCodes] ([ChannelType], [CodeHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_ExpiresAt] ON [ChannelLinkCodes] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    CREATE INDEX [IX_ChannelLinkCodes_UserId_ChannelType] ON [ChannelLinkCodes] ([UserId], [ChannelType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409134141_AddChannelIngressSupport_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409134141_AddChannelIngressSupport_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF OBJECT_ID(N'[ChannelIdentityLinks]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ChannelIdentityLinks](
            [Id] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [ChannelType] int NOT NULL,
            [ExternalUserId] nvarchar(128) NOT NULL,
            [ExternalChatId] nvarchar(128) NOT NULL,
            [VerifiedAt] datetime2 NOT NULL,
            [LastSeenAt] datetime2 NULL,
            [IsActive] bit NOT NULL,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NULL,
            [CreatedBy] nvarchar(max) NULL,
            [UpdatedBy] nvarchar(max) NULL,
            CONSTRAINT [PK_ChannelIdentityLinks] PRIMARY KEY ([Id])
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_ChannelType_ExternalUserId' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
    BEGIN
        CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_ChannelType_ExternalUserId]
        ON [ChannelIdentityLinks] ([ChannelType], [ExternalUserId]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_UserId_ChannelType' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
    BEGIN
        CREATE UNIQUE INDEX [IX_ChannelIdentityLinks_UserId_ChannelType]
        ON [ChannelIdentityLinks] ([UserId], [ChannelType]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelIdentityLinks_IsActive' AND [object_id] = OBJECT_ID(N'[ChannelIdentityLinks]'))
    BEGIN
        CREATE INDEX [IX_ChannelIdentityLinks_IsActive]
        ON [ChannelIdentityLinks] ([IsActive]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF OBJECT_ID(N'[ChannelLinkCodes]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ChannelLinkCodes](
            [Id] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [ChannelType] int NOT NULL,
            [CodeHash] nvarchar(128) NOT NULL,
            [ExpiresAt] datetime2 NOT NULL,
            [ConsumedAt] datetime2 NULL,
            [AttemptCount] int NOT NULL CONSTRAINT [DF_ChannelLinkCodes_AttemptCount] DEFAULT 0,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NULL,
            [CreatedBy] nvarchar(max) NULL,
            [UpdatedBy] nvarchar(max) NULL,
            CONSTRAINT [PK_ChannelLinkCodes] PRIMARY KEY ([Id])
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_ChannelType_CodeHash' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
    BEGIN
        CREATE INDEX [IX_ChannelLinkCodes_ChannelType_CodeHash]
        ON [ChannelLinkCodes] ([ChannelType], [CodeHash]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_UserId_ChannelType' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
    BEGIN
        CREATE INDEX [IX_ChannelLinkCodes_UserId_ChannelType]
        ON [ChannelLinkCodes] ([UserId], [ChannelType]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelLinkCodes_ExpiresAt' AND [object_id] = OBJECT_ID(N'[ChannelLinkCodes]'))
    BEGIN
        CREATE INDEX [IX_ChannelLinkCodes_ExpiresAt]
        ON [ChannelLinkCodes] ([ExpiresAt]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF OBJECT_ID(N'[ChannelInboundMessageLogs]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ChannelInboundMessageLogs](
            [Id] uniqueidentifier NOT NULL,
            [ChannelType] int NOT NULL,
            [ExternalMessageId] nvarchar(150) NOT NULL,
            [ExternalUserId] nvarchar(128) NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [TraceId] nvarchar(120) NOT NULL,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NULL,
            [CreatedBy] nvarchar(max) NULL,
            [UpdatedBy] nvarchar(max) NULL,
            CONSTRAINT [PK_ChannelInboundMessageLogs] PRIMARY KEY ([Id])
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId' AND [object_id] = OBJECT_ID(N'[ChannelInboundMessageLogs]'))
    BEGIN
        CREATE UNIQUE INDEX [IX_ChannelInboundMessageLogs_ChannelType_ExternalMessageId]
        ON [ChannelInboundMessageLogs] ([ChannelType], [ExternalMessageId]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ChannelInboundMessageLogs_UserId' AND [object_id] = OBJECT_ID(N'[ChannelInboundMessageLogs]'))
    BEGIN
        CREATE INDEX [IX_ChannelInboundMessageLogs_UserId]
        ON [ChannelInboundMessageLogs] ([UserId]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF COL_LENGTH('CashExpenses', 'Origin') IS NULL
    BEGIN
        ALTER TABLE [CashExpenses] ADD [Origin] int NOT NULL CONSTRAINT [DF_CashExpenses_Origin] DEFAULT 0;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF COL_LENGTH('CashExpenses', 'SourceType') IS NULL
    BEGIN
        ALTER TABLE [CashExpenses] ADD [SourceType] nvarchar(50) NULL;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF COL_LENGTH('CashExpenses', 'SourceId') IS NULL
    BEGIN
        ALTER TABLE [CashExpenses] ADD [SourceId] uniqueidentifier NULL;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
    BEGIN
        CREATE INDEX [IX_CashExpenses_SourceType_SourceId]
        ON [CashExpenses] ([SourceType], [SourceId]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CashExpenses_Origin_SourceType_SourceId' AND [object_id] = OBJECT_ID(N'[CashExpenses]'))
    BEGIN
        CREATE UNIQUE INDEX [IX_CashExpenses_Origin_SourceType_SourceId]
        ON [CashExpenses] ([Origin], [SourceType], [SourceId])
        WHERE [SourceId] IS NOT NULL;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420211624_AddCashOperationOriginTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260420211624_AddCashOperationOriginTracking', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421222213_AddDemoDataset_Tenant'
)
BEGIN
    CREATE TABLE [DemoDatasets] (
        [Id] uniqueidentifier NOT NULL,
        [Version] nvarchar(32) NOT NULL,
        [AppliedAtUtc] datetime2 NOT NULL,
        [AppliedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DemoDatasets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421222213_AddDemoDataset_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260421222213_AddDemoDataset_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    ALTER TABLE [Quotes] ADD [OriginStorefrontOrderId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    ALTER TABLE [Products] ADD [IsPubliclyListed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    CREATE TABLE [StorefrontOutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [AggregateType] nvarchar(100) NOT NULL,
        [AggregateId] uniqueidentifier NOT NULL,
        [SourceVersion] int NOT NULL,
        [EventType] nvarchar(200) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [AttemptCount] int NOT NULL DEFAULT 0,
        [LastError] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StorefrontOutboxMessages] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Quotes_OriginStorefrontOrderId] ON [Quotes] ([OriginStorefrontOrderId]) WHERE [OriginStorefrontOrderId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    CREATE INDEX [IX_Products_IsPubliclyListed] ON [Products] ([IsPubliclyListed]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    CREATE INDEX [IX_StorefrontOutbox_AggregateId] ON [StorefrontOutboxMessages] ([AggregateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    CREATE INDEX [IX_StorefrontOutbox_Pending] ON [StorefrontOutboxMessages] ([ProcessedAt], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422212244_AddStorefrontPublishing_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422212244_AddStorefrontPublishing_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

