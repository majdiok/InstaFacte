using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1, lot 2 — filet de caractérisation posé AVANT d'activer la réservation de stock.
///
/// <c>QuantityReserved</c> vaut 0 depuis toujours : <c>Reserve()</c> n'a jamais été appelé.
/// Donc <c>QuantityAvailable == QuantityOnHand</c> partout. Activer la réservation change la
/// valeur lue par huit sites, dont trois qui DÉCIDENT :
///
///   • StockItem.RecordExit  — refuse la sortie si quantité &gt; disponible ;
///   • les transferts inter-dépôts (ConfirmStockTransferCommand, StockTransferCompletionService) ;
///   • la déduction partielle sur facture, qui plafonne au disponible.
///
/// Le piège : une commande qui réserve son stock bloquerait SA PROPRE livraison, puisque la
/// sortie verrait un disponible amputé de sa propre réservation.
///
/// Ces tests figent les deux régimes — réservation nulle (comportement historique) et
/// réservation active — pour que la bascule soit un choix explicite et non une surprise.
/// </summary>
public sealed class StockReservationCharacterizationTests
{
    // ───────────── Régime historique : aucune réservation ─────────────

    [Fact]
    public void WithoutReservation_AvailableEqualsOnHand()
    {
        var item = StockWith(onHand: 100m);

        Assert.Equal(0m, item.QuantityReserved);
        Assert.Equal(100m, item.QuantityAvailable);
        Assert.Equal(item.QuantityOnHand, item.QuantityAvailable);
    }

    [Fact]
    public void WithoutReservation_ExitUpToOnHandIsAllowed()
    {
        var item = StockWith(onHand: 100m);

        Assert.True(item.RecordExit(100m, MovementReason.Sale).IsSuccess);
        Assert.Equal(0m, item.QuantityOnHand);
    }

    // ───────────── Régime réservé : ce qui change ─────────────

    [Fact]
    public void Reservation_ShrinksAvailableButNotOnHand()
    {
        var item = StockWith(onHand: 100m);

        Assert.True(item.Reserve(30m).IsSuccess);

        Assert.Equal(100m, item.QuantityOnHand);   // le stock physique ne bouge pas
        Assert.Equal(30m, item.QuantityReserved);
        Assert.Equal(70m, item.QuantityAvailable); // seul le disponible baisse
    }

    [Fact]
    public void Reservation_BlocksAnExitThatWouldHavePassed()
    {
        // LE PIÈGE, isolé. Sans libération préalable, la commande qui a réservé
        // empêcherait sa propre livraison.
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(100m).IsSuccess);

        var exit = item.RecordExit(100m, MovementReason.Sale);

        Assert.True(exit.IsFailure);
        Assert.Contains("Stock insuffisant", exit.Error.Description);
        Assert.Equal(100m, item.QuantityOnHand); // rien n'est sorti
    }

    [Fact]
    public void ReleaseThenExit_IsWhatMakesDeliveryPossible()
    {
        // La parade : libérer avant de sortir. C'est exactement ce que fait ReleaseAndExit,
        // en une seule opération pour que l'appelant ne puisse pas oublier l'appariement.
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(100m).IsSuccess);

        Assert.True(item.ReleaseReservation(100m).IsSuccess);
        Assert.True(item.RecordExit(100m, MovementReason.Sale).IsSuccess);

        Assert.Equal(0m, item.QuantityOnHand);
        Assert.Equal(0m, item.QuantityReserved);
    }

    [Fact]
    public void ReleaseAndExit_DoesBothAtomically()
    {
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(40m).IsSuccess);

        var result = item.ReleaseAndExit(40m, MovementReason.Sale, "BL-2026-000001");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(60m, item.QuantityOnHand);
        Assert.Equal(0m, item.QuantityReserved);
        Assert.Equal(60m, item.QuantityAvailable);

        var movement = item.Movements.Last();
        Assert.Equal(MovementType.Exit, movement.Type);
        Assert.Equal(-40m, movement.Quantity);
    }

    [Fact]
    public void ReleaseAndExit_WithoutExplicitClaim_ReleasesOnlyWhatIsActuallyReserved()
    {
        // Réservation partielle (10 sur une sortie de 40) : la sortie doit passer, en ne
        // libérant que les 10 réservés. Cas courant d'une commande partiellement réservée,
        // ou d'un mélange commande / vente directe sur le même article.
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(10m).IsSuccess);

        var result = item.ReleaseAndExit(40m, MovementReason.Sale, "BL-2026-000002");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(60m, item.QuantityOnHand);
        Assert.Equal(0m, item.QuantityReserved);
    }

    [Fact]
    public void ReleaseAndExit_WithAnUnhonourableClaim_LeavesEverythingUntouched()
    {
        // Garde-fou d'atomicité : l'appelant affirme libérer 40 alors que 10 seulement sont
        // réservés. Incohérence de l'appelant — on ne sort rien et on ne libère rien.
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(10m).IsSuccess);

        var result = item.ReleaseAndExit(40m, MovementReason.Sale, "BL-2026-000002", reservedQuantity: 40m);

        Assert.True(result.IsFailure);
        Assert.Equal(100m, item.QuantityOnHand);
        Assert.Equal(10m, item.QuantityReserved); // réservation intacte
    }

    [Fact]
    public void ReleaseAndExit_RestoresTheReservationWhenTheExitFails()
    {
        // La libération réussit mais la sortie échoue (stock physique insuffisant) :
        // la réservation doit être rétablie, sans quoi elle serait silencieusement perdue.
        var item = StockWith(onHand: 30m);
        Assert.True(item.Reserve(30m).IsSuccess);

        var result = item.ReleaseAndExit(50m, MovementReason.Sale, "BL-2026-000004", reservedQuantity: 30m);

        Assert.True(result.IsFailure);
        Assert.Equal(30m, item.QuantityOnHand);
        Assert.Equal(30m, item.QuantityReserved); // réservation rétablie
    }

    [Fact]
    public void ReleaseAndExit_WithoutAnyReservation_BehavesLikeAPlainExit()
    {
        // Cas du drapeau désactivé : rien n'a été réservé, la sortie doit passer telle quelle.
        var item = StockWith(onHand: 100m);

        var result = item.ReleaseAndExit(40m, MovementReason.Sale, "BL-2026-000003");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(60m, item.QuantityOnHand);
        Assert.Equal(0m, item.QuantityReserved);
    }

    [Fact]
    public void Reservation_CannotExceedAvailable()
    {
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(80m).IsSuccess);

        Assert.True(item.Reserve(30m).IsFailure); // 80 + 30 > 100
        Assert.Equal(80m, item.QuantityReserved);
    }

    [Fact]
    public void ReleasingMoreThanReserved_IsRejected()
    {
        var item = StockWith(onHand: 100m);
        Assert.True(item.Reserve(20m).IsSuccess);

        Assert.True(item.ReleaseReservation(50m).IsFailure);
        Assert.Equal(20m, item.QuantityReserved);
    }

    private static StockItem StockWith(decimal onHand)
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.True(item.RecordEntry(onHand, 10m, MovementReason.InitialStock).IsSuccess);
        return item;
    }
}
