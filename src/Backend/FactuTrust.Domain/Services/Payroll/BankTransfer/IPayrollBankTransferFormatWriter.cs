namespace FactuTrust.Domain.Services.Payroll.BankTransfer;

/// <summary>Sérialise un lot de virements dans un format fichier donné.</summary>
public interface IPayrollBankTransferFormatWriter
{
    PayrollBankTransferFormat Format { get; }
    string FileExtension { get; }
    string ContentType { get; }
    byte[] Write(PayrollBankTransferBatch batch);
}
