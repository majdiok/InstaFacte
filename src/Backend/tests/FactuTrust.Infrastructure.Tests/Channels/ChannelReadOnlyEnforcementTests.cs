using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Test de sécurité clé du canal WhatsApp : la lecture seule est STRUCTURELLE.
///
/// L'orchestrateur de canal envoie toujours <c>ForceReadOnlyTools = true</c>, ce qui force
/// <c>enableMutationTools: false</c> à l'unique couture du pipeline
/// (<c>SendChatMessageCommand</c>) — quel que soit le réglage global
/// <c>Ollama.EnableMutationTools</c> et quelle que soit l'intention détectée dans le message
/// (« crée une facture »…). Ces tests verrouillent les deux moitiés du contrat :
/// <list type="number">
///   <item>le catalogue produit par <see cref="AiToolRegistry"/> avec mutation désactivée ne
///     contient JAMAIS d'outil mutant, pour tous les modes et tous les scopes experts ;</item>
///   <item>l'option est rétro-compatible : défaut <c>false</c> = chemin web historique inchangé
///     (les outils mutants restent proposés côté web quand le réglage global les autorise).</item>
/// </list>
/// </summary>
public sealed class ChannelReadOnlyEnforcementTests
{
    public static TheoryData<AssistantMode, AssistantAgentScope> AllModeScopeCombinations()
    {
        var data = new TheoryData<AssistantMode, AssistantAgentScope>();
        foreach (var mode in Enum.GetValues<AssistantMode>())
        {
            foreach (var scope in Enum.GetValues<AssistantAgentScope>())
            {
                data.Add(mode, scope);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllModeScopeCombinations))]
    public void MutationDisabledCatalog_NeverContainsMutatingTools(AssistantMode mode, AssistantAgentScope scope)
    {
        var definitions = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools: false, scope);

        var mutating = definitions.Where(d => d.IsMutating).Select(d => d.Name).ToList();
        Assert.True(mutating.Count == 0,
            $"Outils mutants exposés en lecture seule (mode {mode}, scope {scope}) : {string.Join(", ", mutating)}");
    }

    [Fact]
    public void MutationDisabledCatalog_StillExposesReadOnlyTools()
    {
        // La lecture seule ne doit pas vider le catalogue : le Q&A WhatsApp reste fonctionnel.
        var definitions = AiToolRegistry.GetDefinitionsForMode(
            AssistantMode.Default, enableMutationTools: false, AssistantAgentScope.None);

        Assert.NotEmpty(definitions);
        Assert.Contains(definitions, d => !d.IsMutating);
    }

    [Fact]
    public void ForceReadOnlyTools_DefaultsToFalse_ForWebBackwardCompatibility()
    {
        // Rétro-compatibilité JSON : un client web existant qui n'envoie pas le champ garde le
        // comportement historique (gating dynamique des outils mutants).
        Assert.False(new ChatRequestOptionsDto().ForceReadOnlyTools);
    }

    [Fact]
    public void WebPath_KeepsMutatingTools_WhenGloballyEnabled()
    {
        // Garde de non-régression web : mutation activée globalement ⇒ le catalogue Default
        // contient toujours des outils mutants (le canal est le SEUL chemin qui les retire).
        var definitions = AiToolRegistry.GetDefinitionsForMode(
            AssistantMode.Default, enableMutationTools: true, AssistantAgentScope.None);

        Assert.Contains(definitions, d => d.IsMutating);
    }
}
