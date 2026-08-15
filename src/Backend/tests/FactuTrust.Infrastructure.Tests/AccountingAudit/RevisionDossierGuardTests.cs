using FactuTrust.Application.Features.Accounting.Audit.Narrative;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Les six garde-fous du dossier de révision.
///
/// <para>C'est la frontière entre ce que le code garantit et ce qu'un modèle de langage produit.
/// Chaque test correspond à une façon dont une réponse peut être fausse, malveillante ou
/// simplement dégradée — et vérifie que la note reste juste malgré tout.</para>
/// </summary>
public sealed class RevisionDossierGuardTests
{
    private static RevisionAnomalyFacts Facts(
        Guid? id = null,
        string ruleCode = "supplier-invoice-duplicate",
        int severity = 2,
        decimal amount = 1500m,
        string title = "Facture en double",
        string description = "Deux enregistrements du même numéro.") =>
        new(
            id ?? Guid.NewGuid(),
            ruleCode,
            "purchases",
            severity,
            title,
            description,
            amount,
            AccountRef: "4011",
            PieceRef: "FA-001",
            Recommendations: ["Annuler la saisie en double."]);

    // ── Garde-fou 1 : le modèle ne peut pas ajouter d'anomalie ────────────────────────────

    [Fact]
    public void An_unknown_anomaly_reference_is_discarded()
    {
        var facts = Facts();
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = Guid.NewGuid().ToString("N"), // référence inventée
                    WorkingNote = "Texte concernant une anomalie qui n'existe pas.",
                    Action = RevisionAction.Reverse
                }
            ]
        };

        var assembly = RevisionDossierGuard.Assemble([facts], response);

        // La note ne contient QUE l'anomalie réelle, avec sa description déterministe.
        var item = Assert.Single(assembly.Items);
        Assert.Equal(facts.Id, item.AnomalyId);
        Assert.Equal(facts.Description, item.WorkingNote);
        Assert.False(assembly.AiGenerated);
    }

    [Fact]
    public void A_duplicated_reference_does_not_overwrite_the_first_writing()
    {
        var facts = Facts();
        var reference = facts.Id.ToString("N");

        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem { AnomalyRef = reference, WorkingNote = "Première rédaction." },
                new RevisionDossierLlmItem { AnomalyRef = reference, WorkingNote = "Seconde rédaction." }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);
        Assert.Equal("Première rédaction.", item.WorkingNote);
    }

    // ── Garde-fou 2 : ensemble fermé d'actions ────────────────────────────────────────────

    [Fact]
    public void An_action_outside_the_closed_set_falls_back_to_the_rule_default()
    {
        var facts = Facts(ruleCode: "supplier-invoice-no-proof");
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = facts.Id.ToString("N"),
                    WorkingNote = "Constat.",
                    // Une consigne qui engagerait le cabinet : elle ne doit jamais passer.
                    Action = "payer_le_fournisseur_immediatement"
                }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);

        Assert.Equal(RevisionAction.RequestInvoice, item.Action);
        Assert.Equal(RevisionAction.LabelFor(RevisionAction.RequestInvoice), item.ActionLabel);
    }

    [Fact]
    public void A_valid_action_is_kept()
    {
        var facts = Facts();
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = facts.Id.ToString("N"),
                    WorkingNote = "Constat.",
                    Action = RevisionAction.Reclassify
                }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);
        Assert.Equal(RevisionAction.Reclassify, item.Action);
    }

    // ── Garde-fou 3 : les faits priment sur la rédaction ──────────────────────────────────

    [Fact]
    public void Severity_account_and_piece_always_come_from_the_facts()
    {
        var facts = Facts(severity: 2);
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = facts.Id.ToString("N"),
                    // Le modèle raconte n'importe quoi : cela ne doit rien changer aux faits.
                    WorkingNote = "Anomalie mineure sur le compte 999999, montant 3 TND, pièce ZZ-000.",
                    Action = RevisionAction.None
                }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);

        Assert.Equal(2, item.Severity);
        Assert.Equal("4011", item.AccountRef);
        Assert.Equal("FA-001", item.PieceRef);
        Assert.Equal(facts.Title, item.Title);
        Assert.Equal(1500m, item.ImpactAmount);
    }

    // ── Garde-fou 4 : impact chiffré seulement quand il a un sens ─────────────────────────

    [Fact]
    public void A_rule_without_monetary_meaning_reports_no_impact()
    {
        // « self-validation » est un défaut de contrôle interne : son montant ne veut rien dire.
        var facts = Facts(ruleCode: "self-validation", amount: 0m);

        var item = Assert.Single(RevisionDossierGuard.BuildDeterministic([facts]).Items);

        Assert.Null(item.ImpactAmount);
    }

    [Fact]
    public void The_total_impact_ignores_non_monetary_rules()
    {
        var monetary = Facts(ruleCode: "supplier-invoice-duplicate", amount: 1000m);
        var procedural = Facts(ruleCode: "off-hours-entry", amount: 999_999m);

        var assembly = RevisionDossierGuard.BuildDeterministic([monetary, procedural]);

        // Seul le montant qui représente un vrai enjeu entre dans le total.
        Assert.Equal(1000m, assembly.TotalImpactAmount);
    }

    // ── Garde-fou 5 : une anomalie non couverte reste complète ────────────────────────────

    [Fact]
    public void An_anomaly_the_model_ignored_keeps_its_deterministic_description()
    {
        var written = Facts(description: "Description A");
        var ignored = Facts(description: "Description B");

        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = written.Id.ToString("N"),
                    WorkingNote = "Rédaction du modèle.",
                    Action = RevisionAction.Reverse
                }
            ]
        };

        var assembly = RevisionDossierGuard.Assemble([written, ignored], response);

        Assert.Equal(2, assembly.Items.Count);
        Assert.Equal("Rédaction du modèle.", assembly.Items[0].WorkingNote);
        Assert.Equal("Description B", assembly.Items[1].WorkingNote);
        Assert.True(assembly.AiGenerated);
    }

    // ── Garde-fou 6 : le dossier n'échoue jamais à cause du modèle ────────────────────────

    [Fact]
    public void Without_any_model_response_the_dossier_is_complete_and_marked_deterministic()
    {
        var assembly = RevisionDossierGuard.BuildDeterministic(
            [Facts(), Facts(severity: 1)], fallbackReason: "Modèle indisponible.");

        Assert.Equal(2, assembly.Items.Count);
        Assert.False(assembly.AiGenerated);
        Assert.Equal("Modèle indisponible.", assembly.FallbackReason);
        Assert.False(string.IsNullOrWhiteSpace(assembly.ExecutiveSummary));
        Assert.All(assembly.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.WorkingNote)));
    }

    [Fact]
    public void An_empty_anomaly_list_yields_a_readable_summary()
    {
        var assembly = RevisionDossierGuard.BuildDeterministic([]);

        Assert.Empty(assembly.Items);
        Assert.Equal(0m, assembly.TotalImpactAmount);
        Assert.Contains("Aucune anomalie", assembly.ExecutiveSummary);
    }

    [Fact]
    public void Counters_come_from_the_facts_not_from_the_model()
    {
        var anomalies = new[] { Facts(severity: 2), Facts(severity: 2), Facts(severity: 1) };

        var assembly = RevisionDossierGuard.BuildDeterministic(anomalies);

        Assert.Equal(3, assembly.AnomalyCount);
        Assert.Equal(2, assembly.BlockingCount);
    }

    // ── Lecture tolérante de la réponse ───────────────────────────────────────────────────

    [Fact]
    public void Parsing_accepts_prose_around_the_json()
    {
        const string raw = """
            Voici le dossier demandé :
            {"executiveSummary":"Synthèse.","items":[{"anomalyRef":"abc","workingNote":"Note.","action":"extourner"}]}
            J'espère que cela convient.
            """;

        var parsed = RevisionDossierGuard.TryParse(raw, out var reason);

        Assert.NotNull(parsed);
        Assert.Null(reason);
        Assert.Equal("Synthèse.", parsed!.ExecutiveSummary);
        Assert.Single(parsed.Items!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Je ne peux pas répondre à cette demande.")]
    [InlineData("{ ceci n'est pas du json }")]
    public void Parsing_rejects_unusable_content_with_a_reason(string? raw)
    {
        var parsed = RevisionDossierGuard.TryParse(raw, out var reason);

        Assert.Null(parsed);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    /// <summary>
    /// Chaîne complète du repli : réponse illisible → aucune rédaction retenue → dossier
    /// déterministe complet, avec le motif conservé pour l'utilisateur.
    /// </summary>
    [Fact]
    public void An_unreadable_response_degrades_to_a_complete_deterministic_dossier()
    {
        var facts = Facts();
        var parsed = RevisionDossierGuard.TryParse("réponse illisible", out var reason);

        var assembly = parsed is null
            ? RevisionDossierGuard.BuildDeterministic([facts], reason)
            : RevisionDossierGuard.Assemble([facts], parsed);

        Assert.Single(assembly.Items);
        Assert.False(assembly.AiGenerated);
        Assert.NotNull(assembly.FallbackReason);
    }

    // ── Normalisation des textes ──────────────────────────────────────────────────────────

    [Fact]
    public void Line_breaks_and_double_spaces_are_normalised()
    {
        var facts = Facts();
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = facts.Id.ToString("N"),
                    WorkingNote = "Première ligne.\n\nSeconde  ligne.\r\n",
                    Action = RevisionAction.None
                }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);

        Assert.DoesNotContain('\n', item.WorkingNote);
        Assert.DoesNotContain("  ", item.WorkingNote);
    }

    [Fact]
    public void An_overlong_note_is_truncated_on_a_word_boundary()
    {
        var facts = Facts();
        var response = new RevisionDossierLlmResponse
        {
            Items =
            [
                new RevisionDossierLlmItem
                {
                    AnomalyRef = facts.Id.ToString("N"),
                    WorkingNote = string.Join(' ', Enumerable.Repeat("mot", 2000)),
                    Action = RevisionAction.None
                }
            ]
        };

        var item = Assert.Single(RevisionDossierGuard.Assemble([facts], response).Items);

        Assert.True(item.WorkingNote.Length <= 1501);
        Assert.EndsWith("…", item.WorkingNote);
        // Coupure sur une frontière de mot : pas de « mo… » en fin de note.
        Assert.DoesNotContain("mo…", item.WorkingNote);
    }
}
