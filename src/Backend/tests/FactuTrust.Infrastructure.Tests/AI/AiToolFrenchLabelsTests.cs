using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolFrenchLabelsTests
{
    /// <summary>
    /// Exhaustivité : tout outil du registre DOIT avoir un libellé FR — sinon la substitution anti-fuite
    /// retombe sur le libellé générique et l'utilisateur perd le sens. Échoue dès qu'un nouvel outil est
    /// ajouté sans libellé.
    /// </summary>
    [Fact]
    public void Every_Registry_Tool_Has_A_French_Label()
    {
        var missing = AiToolRegistry.All
            .Select(t => t.Name)
            .Where(name => !AiToolFrenchLabels.Labels.ContainsKey(name)
                || string.IsNullOrWhiteSpace(AiToolFrenchLabels.Labels[name]))
            .ToList();

        Assert.True(missing.Count == 0, "Outils sans libellé FR : " + string.Join(", ", missing));
    }

    [Fact]
    public void Describe_Falls_Back_To_Generic_For_Unknown_Names()
    {
        Assert.Equal(AiToolFrenchLabels.GenericLabel, AiToolFrenchLabels.Describe("get_totally_unknown_tool"));
    }

    /// <summary>Aucun libellé ne contient de snake_case (sinon la substitution ré-introduirait une fuite).</summary>
    [Fact]
    public void No_Label_Contains_Snake_Case_Tokens()
    {
        foreach (var (name, label) in AiToolFrenchLabels.Labels)
        {
            Assert.False(label.Contains('_'), $"Libellé de {name} contient un underscore : {label}");
        }
    }
}
