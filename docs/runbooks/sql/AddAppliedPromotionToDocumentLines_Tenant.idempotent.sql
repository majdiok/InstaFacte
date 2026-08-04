-- Idempotent: traçabilité promotion sur lignes documentaires
IF COL_LENGTH('SalesOrderLines', 'AppliedPromotionId') IS NULL
BEGIN
    ALTER TABLE SalesOrderLines ADD AppliedPromotionId uniqueidentifier NULL;
END
GO
IF COL_LENGTH('SalesOrderLines', 'AppliedPromotionName') IS NULL
BEGIN
    ALTER TABLE SalesOrderLines ADD AppliedPromotionName nvarchar(100) NULL;
END
GO
IF COL_LENGTH('QuoteLines', 'AppliedPromotionId') IS NULL
BEGIN
    ALTER TABLE QuoteLines ADD AppliedPromotionId uniqueidentifier NULL;
END
GO
IF COL_LENGTH('QuoteLines', 'AppliedPromotionName') IS NULL
BEGIN
    ALTER TABLE QuoteLines ADD AppliedPromotionName nvarchar(100) NULL;
END
GO
IF COL_LENGTH('InvoiceLines', 'AppliedPromotionId') IS NULL
BEGIN
    ALTER TABLE InvoiceLines ADD AppliedPromotionId uniqueidentifier NULL;
END
GO
IF COL_LENGTH('InvoiceLines', 'AppliedPromotionName') IS NULL
BEGIN
    ALTER TABLE InvoiceLines ADD AppliedPromotionName nvarchar(100) NULL;
END
GO
IF COL_LENGTH('DeliveryNoteLines', 'AppliedPromotionId') IS NULL
BEGIN
    ALTER TABLE DeliveryNoteLines ADD AppliedPromotionId uniqueidentifier NULL;
END
GO
IF COL_LENGTH('DeliveryNoteLines', 'AppliedPromotionName') IS NULL
BEGIN
    ALTER TABLE DeliveryNoteLines ADD AppliedPromotionName nvarchar(100) NULL;
END
GO
