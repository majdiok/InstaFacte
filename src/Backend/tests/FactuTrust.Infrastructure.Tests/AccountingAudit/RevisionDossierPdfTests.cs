using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit.Narrative;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.Templates;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Rendu PDF du dossier de révision.
///
/// <para>Le contenu d'un PDF ne se lit pas raisonnablement depuis un test ; on vérifie donc ce qui
/// se vérifie utilement : que le fichier est un PDF réel — l'export d'audit rendait auparavant du
/// texte brut sous une extension trompeuse — et que le rendu tient sur les cas qui le mettent en
/// difficulté : note vide, impact non chiffrable, texte très long, caractères tunisiens.</para>
/// </summary>
public sealed class RevisionDossierPdfTests
{
    /// <summary>Signature d'en-tête d'un fichier PDF : « %PDF ».</summary>
    private static readonly byte[] PdfMagic = Encoding.ASCII.GetBytes("%PDF");

    private static PdfService NewService() =>
        new(new Mock<IHttpClientFactory>().Object, new Mock<IDocumentTemplateRegistry>().Object);

    private static FirmRevisionNoteItemDto Item(
        int severity = 2,
        decimal? impact = 1500m,
        string title = "Facture fournisseur en double",
        string? clientQuestion = null,
        string workingNote = "Deux enregistrements portent le même numéro chez ce fournisseur.") =>
        new()
        {
            AnomalyId = Guid.NewGuid(),
            RuleCode = "supplier-invoice-duplicate",
            ModuleCode = "purchases",
            Severity = severity,
            Title = title,
            ImpactAmount = impact,
            AccountRef = "4011",
            PieceRef = "FA-001",
            WorkingNote = workingNote,
            ClientQuestion = clientQuestion,
            Action = RevisionAction.Reverse,
            ActionLabel = RevisionAction.LabelFor(RevisionAction.Reverse)
        };

    private static RevisionDossierPdfContext Context(
        IReadOnlyList<FirmRevisionNoteItemDto> items,
        bool aiGenerated = true,
        string? fallbackReason = null,
        string summary = "Le dossier présente des points bloquants à traiter avant clôture.") =>
        new()
        {
            Header = new AccountingReportHeader(
                "Société Test", "1234567/A/B/C/000", "Dossier de révision", "Exercice 2026"),
            Note = new FirmRevisionNoteDto
            {
                Id = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                FiscalYear = 2026,
                GeneratedAt = new DateTime(2026, 8, 15, 10, 30, 0, DateTimeKind.Utc),
                GeneratedByUserName = "chef.mission@cabinet.tn",
                AiGenerated = aiGenerated,
                ModelRef = aiGenerated ? "ollama:qwen2.5:7b-instruct" : null,
                FallbackReason = fallbackReason,
                ExecutiveSummary = summary,
                TotalImpactAmount = items.Sum(i => i.ImpactAmount ?? 0m),
                AnomalyCount = items.Count,
                BlockingCount = items.Count(i => i.Severity == 2),
                Items = items
            },
            ComplianceRate = 82.5m,
            ControlCompletedAt = new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc)
        };

    private static void AssertIsPdf(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, "Le PDF produit est anormalement court.");
        Assert.Equal(PdfMagic, bytes.Take(PdfMagic.Length).ToArray());
    }

    [Fact]
    public async Task Produces_a_real_pdf_file()
    {
        var pdf = await NewService().GenerateRevisionDossierPdfAsync(Context([Item()]));

        AssertIsPdf(pdf);
    }

    /// <summary>Un dossier sans anomalie doit produire un document, pas une exception.</summary>
    [Fact]
    public async Task Renders_an_empty_dossier()
    {
        var context = Context([], summary: "Aucune anomalie ouverte sur la période contrôlée.");

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }

    /// <summary>
    /// Impact absent : le rendu doit imprimer « non chiffrable » et non « 0,000 ». On ne peut pas
    /// le relire dans le binaire, mais on vérifie au moins que le chemin ne casse pas.
    /// </summary>
    [Fact]
    public async Task Renders_an_item_without_a_monetary_impact()
    {
        var context = Context([Item(severity: 0, impact: null, title: "Saisie hors heures ouvrées")]);

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }

    [Fact]
    public async Task Renders_the_three_severity_levels_together()
    {
        var context = Context(
        [
            Item(severity: 2, impact: 5000m),
            Item(severity: 1, impact: 250m, title: "Lettrage incohérent"),
            Item(severity: 0, impact: null, title: "Écriture antidatée")
        ]);

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }

    /// <summary>Le motif du repli est une information de confiance : le rendu doit le porter.</summary>
    [Fact]
    public async Task Renders_a_deterministic_dossier_with_its_fallback_reason()
    {
        var context = Context(
            [Item()],
            aiGenerated: false,
            fallbackReason: "Réponse du modèle inexploitable : dossier rédigé à partir des seuls constats.");

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }

    [Fact]
    public async Task Renders_a_question_to_the_client()
    {
        var context = Context(
            [Item(clientQuestion: "Pouvez-vous confirmer que ces deux factures couvrent des prestations distinctes ?")]);

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }

    /// <summary>Volume réaliste d'un dossier chargé : le document doit paginer sans faillir.</summary>
    [Fact]
    public async Task Renders_a_large_dossier_across_pages()
    {
        var items = Enumerable.Range(0, 60)
            .Select(i => Item(severity: i % 3, impact: i % 2 == 0 ? i * 100m : null,
                title: $"Anomalie n° {i + 1}"))
            .ToList();

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(Context(items)));
    }

    // ── Rapport de contrôle ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Le rapport de contrôle rendait auparavant du <b>texte brut</b> servi en
    /// <c>application/pdf</c> : le fichier n'était pas ouvrable. Ce test verrouille la correction.
    /// </summary>
    [Fact]
    public async Task Audit_report_is_a_real_pdf_and_not_plain_text()
    {
        var context = new AccountingAuditPdfContext
        {
            Header = new AccountingReportHeader(
                "Société Test", "1234567/A/B/C/000", "Rapport de contrôle d'intégrité", "Exercice 2026"),
            Dashboard = new AccountingAuditDashboardDto
            {
                FiscalYear = 2026,
                BlockingCount = 2,
                WarningCount = 5,
                InfoCount = 3,
                AnomalyCount = 10,
                ComplianceRate = 78.5m,
                ComplianceRateDeltaVsPriorYear = -4.2m
            },
            Anomalies =
            [
                new AccountingAnomalyListItemDto
                {
                    Id = Guid.NewGuid(),
                    RuleCode = "supplier-invoice-duplicate",
                    ModuleCode = "purchases",
                    Severity = 2,
                    Category = 12,
                    Title = "Facture fournisseur en double",
                    Description = "Deux enregistrements du même numéro.",
                    DetailSummary = "Deux enregistrements du même numéro.",
                    AccountRef = "4011",
                    Amount = 1500m,
                    Status = 0,
                    DetectedAt = DateTime.UtcNow
                }
            ],
            Modules = [new AccountingControlModuleDto { Code = "purchases", Label = "Achats", Icon = "fa", Count = 1 }],
            TotalAnomalyCount = 10
        };

        AssertIsPdf(await NewService().GenerateAccountingAuditPdfAsync(context));
    }

    [Fact]
    public async Task Audit_report_renders_without_any_anomaly()
    {
        var context = new AccountingAuditPdfContext
        {
            Header = new AccountingReportHeader("Société Test", null, "Rapport", "Exercice 2026"),
            Dashboard = new AccountingAuditDashboardDto { FiscalYear = 2026, ComplianceRate = 100m },
            Anomalies = [],
            Modules = [],
            TotalAnomalyCount = 0
        };

        AssertIsPdf(await NewService().GenerateAccountingAuditPdfAsync(context));
    }

    [Fact]
    public async Task Renders_long_text_and_french_accents()
    {
        var context = Context(
            [Item(workingNote: string.Join(' ', Enumerable.Repeat(
                "Le compte concerné présente un écart qu'il convient de justifier auprès du client.", 20)))],
            summary: "Révision de l'exercice : régularisations à opérer sur les comptes de tiers, " +
                     "et clarification des écritures antidatées près de la clôture.");

        AssertIsPdf(await NewService().GenerateRevisionDossierPdfAsync(context));
    }
}
