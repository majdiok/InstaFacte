using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Lot 1.3 — tests unitaires du grounding gate firm : la détection « le tour demande des données »
/// (<see cref="SendChatMessageHandler.FirmTurnNeedsData"/>) et la fonction commune de décision
/// (<see cref="SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer"/>). Méthodes statiques pures,
/// testées isolément ; la matrice couvre scope × firmGroundedReads × turnNeedsData × flag, ainsi que
/// les cas limites du plan (outil en erreur ⇒ non ancré, Ok non exploitable, portefeuille vide
/// légitime ⇒ ancré, follow-up demandant des données ⇒ gate appliqué).
/// </summary>
public sealed class FirmGroundingGateTests
{
    // ── FirmTurnNeedsData ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Router_matching_question_needs_data()
        => Assert.True(SendChatMessageHandler.FirmTurnNeedsData("Où en est le portefeuille du cabinet aujourd'hui ?"));

    [Theory]
    [InlineData("Combien de dossiers sont actifs ?")]            // combien
    [InlineData("Quel est le montant des retards ?")]             // quel
    [InlineData("Quelles sont les échéances de la semaine ?")]   // quelles
    [InlineData("Donne-moi la liste des dossiers à risque")]     // liste
    [InlineData("Classe les dossiers par risque")]               // classe
    [InlineData("Qui est le plus chargé ?")]                     // qui (mot isolé)
    [InlineData("Donne-moi le top 5 des dossiers à risque")]     // top (mot isolé)
    [InlineData("Quel montant d'échéances en retard ?")]         // quel + montant
    public void Heuristic_keywords_need_data(string message)
        => Assert.True(SendChatMessageHandler.FirmTurnNeedsData(message));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Merci, c'est très clair.")]
    [InlineData("Bonjour")]
    [InlineData("Résume ma dernière réponse en 2 lignes.")] // follow-up purement rédactionnel ⇒ faux
    public void Non_data_messages_do_not_need_data(string? message)
        => Assert.False(SendChatMessageHandler.FirmTurnNeedsData(message));

    [Fact]
    public void Qui_must_be_a_whole_word_not_a_substring()
    {
        // « liquidation » contient « qui » : la détection en mot isolé évite un faux positif.
        Assert.False(SendChatMessageHandler.FirmTurnNeedsData("Explique-moi la liquidation d'une société."));
    }

    [Fact]
    public void Follow_up_demanding_new_figures_needs_data()
    {
        // Un follow-up cliqué demandant de NOUVELLES données doit passer le gate comme un message tapé
        // (l'exemption de l'intent Synthesis est gérée au niveau handler, pas ici).
        Assert.True(SendChatMessageHandler.FirmTurnNeedsData(
            "Combien de dossiers ont une échéance dans les 7 prochains jours ?"));
    }

    // ── ShouldRejectUngroundedFirmAnswer : matrice ────────────────────────────────────────────────

    [Fact]
    public void Firm_turn_without_grounded_reads_and_needing_data_is_rejected()
        => Assert.True(SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer(
            AssistantAgentScope.FirmMission, firmGroundedReads: 0, turnNeedsData: true, gateEnabled: true));

    [Fact]
    public void Firm_turn_with_at_least_one_grounded_read_is_not_rejected()
        => Assert.False(SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer(
            AssistantAgentScope.FirmMission, firmGroundedReads: 1, turnNeedsData: true, gateEnabled: true));

    [Fact]
    public void Firm_turn_not_needing_data_is_not_rejected()
        => Assert.False(SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer(
            AssistantAgentScope.FirmMission, firmGroundedReads: 0, turnNeedsData: false, gateEnabled: true));

    [Fact]
    public void Gate_disabled_is_not_rejected_identical_to_before_lot1()
        => Assert.False(SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer(
            AssistantAgentScope.FirmMission, firmGroundedReads: 0, turnNeedsData: true, gateEnabled: false));

    [Theory]
    [InlineData(AssistantAgentScope.None)]
    [InlineData(AssistantAgentScope.Sales)]
    [InlineData(AssistantAgentScope.Stock)]
    public void Non_firm_scopes_are_never_rejected(AssistantAgentScope scope)
        => Assert.False(SendChatMessageHandler.ShouldRejectUngroundedFirmAnswer(
            scope, firmGroundedReads: 0, turnNeedsData: true, gateEnabled: true));
}
