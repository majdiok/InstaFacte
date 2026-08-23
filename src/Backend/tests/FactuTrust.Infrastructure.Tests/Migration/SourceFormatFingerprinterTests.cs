using System.Text;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services.Migration;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Migration;

/// <summary>
/// Fingerprint déterministe des fichiers de migration (N1/M0) : reconnaissance catalogue
/// (Sage, EBP…), heuristiques génériques de cible, abstention explicite sur fichier vide.
/// Corpus synthétique — aucune donnée client.
/// </summary>
public sealed class SourceFormatFingerprinterTests
{
    private static MigrationFileShape Shape(string csv, string? delimiter = ";")
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Trim().Split(delimiter ?? ";").Select(h => h.Trim()).ToArray();
        return new MigrationFileShape(headers, lines.Length - 1, delimiter, 1);
    }

    [Fact]
    public void Sage_ChartOfAccounts_RecognizedByCatalog()
    {
        var shape = Shape("CG_NUM;CG_INTITC;CG_CLASSE\n411000;Clients;4\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(MigrationSourceSystem.SageLigne100, fp.System);
        Assert.Equal(ReferenceImportTarget.ChartOfAccounts, fp.Target);
        Assert.True(fp.KnownFormatMatched);
        Assert.True(fp.Confidence >= 0.9);
    }

    [Fact]
    public void Sage_ThirdParties_RecognizedByCatalog()
    {
        var shape = Shape("CT_NUM;CT_INTITULE;CT_EMAIL;CT_ADRESSE;CT_VILLE;CT_REGION\nC001;Alpha;a@b.tn;1 rue X;Tunis;Tunis\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(MigrationSourceSystem.SageLigne100, fp.System);
        Assert.Equal(ReferenceImportTarget.ThirdParties, fp.Target);
        Assert.True(fp.KnownFormatMatched);
    }

    [Fact]
    public void Sage_Balance_RecognizedAsOpeningBalance()
    {
        var shape = Shape("CG_NUM;SOLDEDEBIT;SOLDECREDIT\n411000;1500,000;0\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(ReferenceImportTarget.OpeningBalance, fp.Target);
        Assert.True(fp.KnownFormatMatched);
    }

    [Fact]
    public void Ebp_ChartOfAccounts_RecognizedByCatalog()
    {
        var shape = Shape("NumeroCompte;IntituleCompte;ClasseCompte\n411000;Clients;4\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(MigrationSourceSystem.Ebp, fp.System);
        Assert.Equal(ReferenceImportTarget.ChartOfAccounts, fp.Target);
        Assert.True(fp.KnownFormatMatched);
    }

    [Fact]
    public void Generic_Balance_GuessedByHeuristic()
    {
        var shape = Shape("compte;debit;credit\n411000;100;0\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(MigrationSourceSystem.Inconnu, fp.System);
        Assert.False(fp.KnownFormatMatched);
        Assert.Equal(ReferenceImportTarget.OpeningBalance, fp.Target);
        Assert.True(fp.TargetConfidence >= 0.5);
    }

    [Fact]
    public void Generic_ThirdParties_GuessedByHeuristic()
    {
        var shape = Shape("nom;email;ville\nAlpha;a@b.tn;Tunis\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(ReferenceImportTarget.ThirdParties, fp.Target);
        Assert.False(fp.KnownFormatMatched);
    }

    [Fact]
    public void Generic_Chart_GuessedByHeuristic()
    {
        var shape = Shape("numero;intitule;classe\n611;Prestations;6\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(ReferenceImportTarget.ChartOfAccounts, fp.Target);
    }

    [Fact]
    public void Empty_Headers_AbstainsWithWarning()
    {
        var shape = new MigrationFileShape(Array.Empty<string>(), 0, ";", 1);

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(MigrationSourceSystem.Inconnu, fp.System);
        Assert.Equal(0, fp.Confidence);
        Assert.NotEmpty(fp.Warnings);
    }

    [Fact]
    public void MultiSheet_WarnsAboutFirstSheetOnly()
    {
        var shape = new MigrationFileShape(new[] { "compte", "debit", "credit" }, 3, ";", 3);

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Contains(fp.Warnings, w => w.Contains("feuilles"));
    }

    [Fact]
    public void Accented_Headers_AreNormalizedBeforeMatching()
    {
        // « Intitulé » avec accent doit correspondre au synonyme « intitule ».
        var shape = Shape("Numéro;Intitulé;Classe\n611;Prestations;6\n");

        var fp = SourceFormatFingerprinter.Fingerprint(shape);

        Assert.Equal(ReferenceImportTarget.ChartOfAccounts, fp.Target);
    }
}
