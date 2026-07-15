using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Persistence.Seeds;

/// <summary>
/// Official TEJ operation codes per the Tunisian tax code (Art. 52 and 53 IRPP/IS).
/// Seed via EF Core HasData or manual insert on tenant creation.
/// </summary>
public static class WithholdingTaxTypeSeed
{
    public static IReadOnlyList<WithholdingTaxType> GetSystemTypes()
    {
        var types = new List<WithholdingTaxType>();
        var order = 0;

        // ──── RS1: Loyers (Art. 52-I) ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS1_000001", WithholdingCategory.Loyers,
            "Loyers d'immeubles bâtis — personnes physiques", 15m,
            "Art. 52-I IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS1_000002", WithholdingCategory.Loyers,
            "Loyers d'immeubles bâtis — personnes morales", 15m,
            "Art. 52-I IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS1_000003", WithholdingCategory.Loyers,
            "Loyers de terrains non bâtis", 15m,
            "Art. 52-I IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS1_000004", WithholdingCategory.Loyers,
            "Loyers de matériels, équipements et engins", 15m,
            "Art. 52-I IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS1_000005", WithholdingCategory.Loyers,
            "Loyers hôtels et établissements similaires", 5m,
            "Art. 52-I IRPP", displayOrder: ++order));

        // ──── RS2: Honoraires, commissions, courtages (Art. 52-II) ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS2_000001", WithholdingCategory.Honoraires,
            "Honoraires — personnes physiques résidentes", 3m,
            "Art. 52-II IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS2_000002", WithholdingCategory.Honoraires,
            "Commissions et courtages — résidents", 10m,
            "Art. 52-II IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS2_000003", WithholdingCategory.Honoraires,
            "Rémunérations d'activités occasionnelles", 15m,
            "Art. 52-II IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS2_000004", WithholdingCategory.Honoraires,
            "Jetons de présence (administrateurs)", 20m,
            "Art. 52-II IRPP", displayOrder: ++order));

        // ──── RS3: Revenus de capitaux mobiliers (Art. 52-III) ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS3_000001", WithholdingCategory.RevenusCapitaux,
            "Intérêts des dépôts et comptes courants", 20m,
            "Art. 52-III IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS3_000002", WithholdingCategory.RevenusCapitaux,
            "Intérêts des bons de caisse et obligations", 20m,
            "Art. 52-III IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS3_000003", WithholdingCategory.RevenusCapitaux,
            "Intérêts des prêts entre parties liées", 20m,
            "Art. 52-III IRPP", displayOrder: ++order));

        // ──── RS4: Dividendes ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS4_000001", WithholdingCategory.Dividendes,
            "Dividendes distribués", 10m,
            "Art. 52 IRPP", displayOrder: ++order));

        // ──── RS5: Plus-values de cession ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS5_000001", WithholdingCategory.PlusValues,
            "Plus-values de cession — valeurs mobilières", 10m,
            "Art. 52 IRPP", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS5_000002", WithholdingCategory.PlusValues,
            "Plus-values de cession — immeubles", 15m,
            "Art. 52 IRPP", displayOrder: ++order));

        // ──── RS6: Immobilier / fonds de commerce ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS6_000001", WithholdingCategory.ImmobilierFoncier,
            "Cession de fonds de commerce ou clientèle", 2.5m,
            "Art. 52 IRPP", displayOrder: ++order));

        // ──── RS7: Achats >= seuil (Art. 52-g) ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS7_000001", WithholdingCategory.Achats,
            "Achats ≥ 1 000 TND TTC — IS taux normal (25%)", 1.5m,
            "Art. 52-g IS", minimumThreshold: 1000m, displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS7_000002", WithholdingCategory.Achats,
            "Achats ≥ 1 000 TND TTC — IS taux réduit (15%)", 1m,
            "Art. 52-g IS", minimumThreshold: 1000m, displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS7_000003", WithholdingCategory.Achats,
            "Achats ≥ 1 000 TND TTC — IS taux réduit (10%)", 0.5m,
            "Art. 52-g IS", minimumThreshold: 1000m, displayOrder: ++order));

        // ──── RS8: Jeux et loteries ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS8_000001", WithholdingCategory.Jeux,
            "Gains de jeux, loteries et concours", 25m,
            "Art. 52 IRPP", displayOrder: ++order));

        // ──── RS9: Non-résidents (Art. 53) ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS9_000001", WithholdingCategory.NonResidents,
            "Services rendus par des non-résidents", 15m,
            "Art. 53 IS", applicableToResident: false, applicableToNonResident: true,
            displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS9_000002", WithholdingCategory.NonResidents,
            "Redevances et droits d'auteur — non-résidents", 15m,
            "Art. 53 IS", applicableToResident: false, applicableToNonResident: true,
            displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS9_000003", WithholdingCategory.NonResidents,
            "Intérêts payés à des non-résidents", 20m,
            "Art. 53 IS", applicableToResident: false, applicableToNonResident: true,
            displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS9_000004", WithholdingCategory.NonResidents,
            "Assistance technique — non-résidents", 15m,
            "Art. 53 IS", applicableToResident: false, applicableToNonResident: true,
            displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS9_000005", WithholdingCategory.NonResidents,
            "Rémunérations d'artistes et sportifs non-résidents", 15m,
            "Art. 53 IS", applicableToResident: false, applicableToNonResident: true,
            displayOrder: ++order));

        // ──── RS10: Traitements et salaires ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS10_000001", WithholdingCategory.Salaires,
            "Traitements et salaires — barème progressif", 0m,
            "Art. 44 IRPP", displayOrder: ++order));

        // ──── RS11: Autres retenues ────
        types.Add(WithholdingTaxType.CreateSystem(
            "RS11_000001", WithholdingCategory.Autres,
            "Marchés publics — avances et retenues de garantie", 1.5m,
            "Art. 52 IRPP/IS", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS11_000002", WithholdingCategory.Autres,
            "Rémunérations servies aux établissements stables", 15m,
            "Art. 52 IS", displayOrder: ++order));

        types.Add(WithholdingTaxType.CreateSystem(
            "RS11_000003", WithholdingCategory.Autres,
            "Autres retenues à la source non classées", 15m,
            "Art. 52 IRPP/IS", displayOrder: ++order));

        return types;
    }
}
