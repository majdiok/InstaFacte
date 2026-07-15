using FactuTrust.Application.Features.AI.Commands;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// P3 — routage des outils par intention. QueryLikelyMutating doit renvoyer false pour les questions de
/// LECTURE fréquentes (catalogue allégé) et true pour les demandes d'écriture (outils d'écriture exposés).
/// </summary>
public sealed class QueryLikelyMutatingTests
{
    [Theory]
    [InlineData("Quel est mon chiffre d'affaires ce mois-ci ?")]
    [InlineData("Affiche-moi l'état du stock actuel")]
    [InlineData("Quels sont mes 5 meilleurs clients ?")]
    [InlineData("Quels paiements ai-je reçus cette semaine ?")]
    [InlineData("Quelle est ma marge commerciale ce trimestre ?")]
    [InlineData("Génère un graphique du chiffre d'affaires par produit")]
    [InlineData("Combien ai-je gagné aujourd'hui ?")]
    [InlineData("")]
    [InlineData(null)]
    public void Returns_False_For_Read_Queries(string? text)
    {
        Assert.False(SendChatMessageHandler.QueryLikelyMutating(text));
    }

    [Theory]
    [InlineData("Crée un client nommé Dupont")]
    [InlineData("crée un client nommé Dupont")] // sans accent géré aussi
    [InlineData("Ajoute un nouveau produit au catalogue")]
    [InlineData("Supprime la facture 12")]
    [InlineData("Valide la facture FAC-2026-001")]
    [InlineData("Applique une remise de 10% sur ce produit")]
    [InlineData("Génère un bon de commande pour le fournisseur X")]
    [InlineData("Encaisse le paiement de 500 TND")]
    [InlineData("Modifie le prix du produit A")]
    [InlineData("Ajuste le stock du produit B à 30")]
    [InlineData("Renomme la catégorie Mobilier")]
    public void Returns_True_For_Mutation_Queries(string text)
    {
        Assert.True(SendChatMessageHandler.QueryLikelyMutating(text));
    }

    [Fact]
    public void Returns_True_When_Confirmation_Follows_A_Mutation_Request_In_Recent_Text()
    {
        // Simule le texte combiné (réponse courante + échange récent) : l'utilisateur confirme « oui »
        // après que l'assistant a demandé « Voulez-vous que je crée le client X ? ».
        var combined = "oui Voulez-vous que je crée le client X ? Crée le client X";
        Assert.True(SendChatMessageHandler.QueryLikelyMutating(combined));
    }
}
