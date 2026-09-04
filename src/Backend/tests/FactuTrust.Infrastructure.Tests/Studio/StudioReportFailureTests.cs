using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Un échec d'état doit laisser une issue. Ces tests fixent le contenu de la carte d'échec :
/// ce qui a été tenté, et des reformulations qui, elles, aboutissent.
/// </summary>
public sealed class StudioReportFailureTests
{
    [Fact]
    public void The_attempted_preset_is_named_so_the_user_knows_what_was_tried()
    {
        var failure = StudioReportFailure.Build(
            "créer moi un rapport détaillé d'achats d'articles",
            "Aucun des regroupements demandés n'existe.",
            "achats_par_produit",
            "Année en cours");

        Assert.Equal("achats_par_produit", failure.Preset);
        Assert.Equal(SqlReportPresetCatalog.Find("achats_par_produit")!.DisplayName, failure.Title);
        Assert.Equal("Année en cours", failure.PeriodLabel);
    }

    [Fact]
    public void The_preset_that_just_failed_is_never_suggested_again()
    {
        var failure = StudioReportFailure.Build(
            "rapport d'achats par produit", "Échec.", "achats_par_produit");

        Assert.DoesNotContain(failure.Suggestions, s =>
            string.Equals(s.Preset, "achats_par_produit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Suggestions_are_real_presets_and_are_capped()
    {
        var failure = StudioReportFailure.Build("rapport des ventes par client et par mois", "Échec.");

        Assert.NotEmpty(failure.Suggestions);
        Assert.True(failure.Suggestions.Count <= StudioReportFailure.MaxSuggestions);
        Assert.All(failure.Suggestions, s =>
        {
            Assert.NotNull(SqlReportPresetCatalog.Find(s.Preset));
            Assert.False(string.IsNullOrWhiteSpace(s.Label));
            Assert.Contains(s.Label, s.Prompt, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void An_empty_reason_still_produces_a_sentence()
    {
        var failure = StudioReportFailure.Build("n'importe quoi", "   ");
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
    }

    [Fact]
    public void An_unknown_preset_key_is_ignored_rather_than_echoed()
    {
        // Le modèle peut inventer une clé : la carte ne doit pas prétendre avoir tenté un état
        // qui n'existe pas.
        var failure = StudioReportFailure.Build("rapport des ventes", "Échec.", "etat_imaginaire");

        Assert.Null(failure.Preset);
        Assert.Null(failure.Title);
    }
}
