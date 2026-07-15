using System.Text;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class TejXmlValidatorTests
{
    private readonly TejXmlValidatorService _sut = new();

    private static byte[] ToBytes(string xml) => Encoding.UTF8.GetBytes(xml);

    [Fact]
    public void Validate_ValidXml_ShouldReturnNoErrors()
    {
        var xml = BuildValidXml();
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WrongRootElement_ShouldReturnError()
    {
        var xml = "<WrongRoot></WrongRoot>";
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("DeclarationsRS"));
    }

    [Fact]
    public void Validate_MissingDeclarant_ShouldReturnError()
    {
        var xml = """<DeclarationsRS VersionSchema="1.0"><ReferenceDeclaration><ActeDepot>0</ActeDepot><AnneeDepot>2026</AnneeDepot><MoisDepot>1</MoisDepot></ReferenceDeclaration></DeclarationsRS>""";
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("Declarant"));
    }

    [Fact]
    public void Validate_MissingReferenceDeclaration_ShouldReturnError()
    {
        var xml = """
        <DeclarationsRS VersionSchema="1.0">
          <Declarant><TypeIdentifiant>1</TypeIdentifiant><Identifiant>0001238A</Identifiant><CategorieContribuable>PM</CategorieContribuable></Declarant>
        </DeclarationsRS>
        """;
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("ReferenceDeclaration"));
    }

    [Fact]
    public void Validate_MissingCertificatBlocks_ShouldReturnError()
    {
        var xml = """
        <DeclarationsRS VersionSchema="1.0">
          <Declarant><TypeIdentifiant>1</TypeIdentifiant><Identifiant>0001238A</Identifiant><CategorieContribuable>PM</CategorieContribuable></Declarant>
          <ReferenceDeclaration><ActeDepot>0</ActeDepot><AnneeDepot>2026</AnneeDepot><MoisDepot>1</MoisDepot></ReferenceDeclaration>
        </DeclarationsRS>
        """;
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("AjouterCertificats") || e.Contains("certificat"));
    }

    [Fact]
    public void Validate_InvalidDateFormat_ShouldReturnError()
    {
        var xml = BuildXmlWithDate("2026-01-15");
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("DD/MM/YYYY"));
    }

    [Fact]
    public void Validate_ValidDateFormat_ShouldPass()
    {
        var xml = BuildXmlWithDate("15/01/2026");
        var errors = _sut.Validate(ToBytes(xml));
        Assert.DoesNotContain(errors, e => e.Contains("DD/MM/YYYY"));
    }

    [Fact]
    public void Validate_NonIntegerAmount_ShouldReturnError()
    {
        var xml = BuildXmlWithAmount("1000.50");
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("millimes") || e.Contains("entier"));
    }

    [Fact]
    public void Validate_IntegerAmount_ShouldPass()
    {
        var xml = BuildXmlWithAmount("1000000");
        var errors = _sut.Validate(ToBytes(xml));
        Assert.DoesNotContain(errors, e => e.Contains("millimes") || e.Contains("entier"));
    }

    [Fact]
    public void Validate_MalformedXml_ShouldReturnError()
    {
        var xml = "not xml at all <<<<>";
        var errors = _sut.Validate(ToBytes(xml));
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("valide"));
    }

    [Fact]
    public void Validate_WrongVersionSchema_ShouldReturnError()
    {
        var xml = BuildValidXml().Replace("VersionSchema=\"1.0\"", "VersionSchema=\"2.0\"");
        var errors = _sut.Validate(ToBytes(xml));
        Assert.Contains(errors, e => e.Contains("VersionSchema"));
    }

    [Fact]
    public void Validate_ValidXml_ShouldNotReportEnvelopeXsdErrors()
    {
        var errors = _sut.Validate(ToBytes(BuildValidXml()));
        Assert.DoesNotContain(errors, e => e.StartsWith("XSD", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildValidXml() =>
        """
        <DeclarationsRS VersionSchema="1.0">
          <Declarant>
            <TypeIdentifiant>1</TypeIdentifiant>
            <Identifiant>0001238A</Identifiant>
            <CategorieContribuable>PM</CategorieContribuable>
          </Declarant>
          <ReferenceDeclaration>
            <ActeDepot>0</ActeDepot>
            <AnneeDepot>2026</AnneeDepot>
            <MoisDepot>1</MoisDepot>
          </ReferenceDeclaration>
          <AjouterCertificats>
            <Certificat>
              <Beneficiaire>
                <TypeIdentifiant>1</TypeIdentifiant>
                <Identifiant>0002345B</Identifiant>
              </Beneficiaire>
              <DatePayement>15/01/2026</DatePayement>
              <Ref_certif_chez_declarant>CERT-2026-01-0001</Ref_certif_chez_declarant>
              <ListeOperations>
                <Operation IdTypeOperation="RS2_000001">
                  <AnneeFacturation>2026</AnneeFacturation>
                  <CNPC>0</CNPC>
                  <P_Charge>0</P_Charge>
                  <MontantHT>1000000</MontantHT>
                  <TauxRS>3000</TauxRS>
                  <MontantTTC>1190000</MontantTTC>
                  <MontantRS>30000</MontantRS>
                  <MontantNetServi>1160000</MontantNetServi>
                </Operation>
              </ListeOperations>
              <TotalPayement>
                <TotalMontantHT>1000000</TotalMontantHT>
                <TotalMontantRS>30000</TotalMontantRS>
                <TotalMontantNetServi>1160000</TotalMontantNetServi>
              </TotalPayement>
            </Certificat>
          </AjouterCertificats>
        </DeclarationsRS>
        """;

    private static string BuildXmlWithDate(string date) =>
        BuildValidXml().Replace("15/01/2026", date);

    private static string BuildXmlWithAmount(string amount) =>
        BuildValidXml().Replace("<MontantHT>1000000</MontantHT>", $"<MontantHT>{amount}</MontantHT>", StringComparison.Ordinal);
}
