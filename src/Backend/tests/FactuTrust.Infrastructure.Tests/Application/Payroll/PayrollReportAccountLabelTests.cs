using FactuTrust.Application.Features.Payroll.Reports;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Libellés de comptes du journal de paie. La table doit couvrir les deux profils d'imputation :
/// sans les comptes SCE, la colonne « Libellé » de l'état et de ses exports se vidait dès la
/// bascule, laissant les montants en face d'un compte sans nom.
/// </summary>
public sealed class PayrollReportAccountLabelTests
{
    [Theory]
    // Comptes communs aux deux profils
    [InlineData("640")]
    [InlineData("647")]
    [InlineData("432")]
    [InlineData("453")]
    [InlineData("421")]
    [InlineData("641")]
    // Comptes du profil SCE
    [InlineData("6611")]
    [InlineData("6612")]
    [InlineData("437")]
    [InlineData("64602")]
    [InlineData("6404")]
    [InlineData("6401")]
    [InlineData("6402")]
    [InlineData("6403")]
    [InlineData("4286")]
    [InlineData("4386")]
    [InlineData("4538")]
    [InlineData("427")]
    [InlineData("421.1")]
    [InlineData("428.1")]
    [InlineData("428.2")]
    public void AccountLabel_IsNeverEmptyForPayrollAccounts(string accountNumber)
    {
        Assert.False(string.IsNullOrWhiteSpace(PayrollReportHelpers.AccountLabel(accountNumber)));
    }

    [Fact]
    public void AccountLabel_MapsEmployeeAuxiliariesToTheCollectiveLabel()
    {
        Assert.Equal("Personnel — rémunérations dues", PayrollReportHelpers.AccountLabel("425"));
        Assert.Equal("Personnel — rémunérations dues", PayrollReportHelpers.AccountLabel("4256854545"));
    }

    [Fact]
    public void AccountLabel_DistinguishesTaxesFromSocialCharges()
    {
        // Le cœur de la correction NCT 01 : TFP et FOPROLOS sont des impôts sur rémunérations (661),
        // pas des charges sociales légales (647), et leur dette n'est pas une retenue à la source.
        Assert.Equal("TFP", PayrollReportHelpers.AccountLabel("6611"));
        Assert.Equal("FOPROLOS", PayrollReportHelpers.AccountLabel("6612"));
        Assert.Equal("Charges sociales de l'employeur", PayrollReportHelpers.AccountLabel("647"));
        Assert.Equal("État — retenues et taxes sur salaires", PayrollReportHelpers.AccountLabel("432"));
        Assert.Equal("État — autres impôts et taxes sur rémunérations", PayrollReportHelpers.AccountLabel("437"));
    }

    [Fact]
    public void AccountLabel_IsEmptyForUnknownAccounts()
    {
        Assert.Equal(string.Empty, PayrollReportHelpers.AccountLabel("707"));
        Assert.Equal(string.Empty, PayrollReportHelpers.AccountLabel(""));
    }
}
