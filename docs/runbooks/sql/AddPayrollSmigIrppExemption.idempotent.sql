-- Idempotent migration: Exonération IRPP SMIG (art. 21)

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PayrollYearParameters') AND name = 'SmigIrppExemptionMode')
BEGIN
    ALTER TABLE PayrollYearParameters
        ADD SmigIrppExemptionMode int NOT NULL CONSTRAINT DF_PayrollYearParameters_SmigIrppExemptionMode DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PayrollYearParameters') AND name = 'SmigIrppExemptionRateOverride')
BEGIN
    ALTER TABLE PayrollYearParameters
        ADD SmigIrppExemptionRateOverride decimal(5,2) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Payslips') AND name = 'IrppBeforeSmigExemption')
BEGIN
    ALTER TABLE Payslips
        ADD IrppBeforeSmigExemption decimal(18,3) NOT NULL CONSTRAINT DF_Payslips_IrppBeforeSmigExemption DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Payslips') AND name = 'IrppSmigExemption')
BEGIN
    ALTER TABLE Payslips
        ADD IrppSmigExemption decimal(18,3) NOT NULL CONSTRAINT DF_Payslips_IrppSmigExemption DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalIrppSmigExemption')
BEGIN
    ALTER TABLE PayrollRuns
        ADD TotalIrppSmigExemption decimal(18,3) NOT NULL CONSTRAINT DF_PayrollRuns_TotalIrppSmigExemption DEFAULT 0;
END
