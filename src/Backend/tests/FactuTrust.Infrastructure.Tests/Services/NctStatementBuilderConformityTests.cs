using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Fixture de conformité complète NCT (T9) : balance bi-exercices équilibrée n'utilisant que des
/// comptes du catalogue livré (plan §1.4), avec TVA, paie et un scénario de cession (T5). Valide
/// l'intégration des quatre états (bilan, compte de résultat, flux de trésorerie, variation des
/// capitaux propres) et les invariants de réconciliation posés par T1-T8.
/// </summary>
public sealed class NctStatementBuilderConformityTests
{
    // NCT 01 (plan §2) : valeurs présentées nettes (net = débit − crédit) ; somme des nets = 0 par
    // la partie double pour un exercice équilibré. Comptes du catalogue uniquement.
    private static (Dictionary<string, decimal> Cur, Dictionary<string, decimal> Prev, NctDisposalAggregates Disposals) ConformityBiExercice()
    {
        // ── N-1 (exercice comparatif) ──────────────────────────────────────────────
        // Classes 1-5 équilibrées (101 = bouchon) ; compte de résultat N-1 à REX/RAI non nuls mais
        // à résultat net NUL (691 absorbe exactement le RAI). Voir la note CFECART plus bas.
        var prev = new Dictionary<string, decimal>
        {
            ["1011"] = 20000m,    // capital souscrit non appelé (débit — en déduction du capital, T1)
            ["162"]  = -60000m,   // emprunt
            ["213"]  = 40000m,    // immobilisations incorporelles brutes
            ["221"]  = 60000m,    // terrains
            ["222"]  = 100000m,   // constructions
            ["251"]  = 15000m,    // participations
            ["256"]  = 5000m,     // autres immobilisations financières
            ["281"]  = -8000m,    // amortissements incorporelles
            ["2821"] = -5000m,    // amortissements terrains
            ["2822"] = -20000m,   // amortissements constructions
            ["291"]  = -2000m,    // provisions incorporelles
            ["31"]   = 12000m,    // stocks matières
            ["37"]   = 8000m,     // stocks marchandises
            ["39"]   = -3000m,    // provisions sur stocks
            ["401"]  = -18000m,   // fournisseurs
            ["411"]  = 25000m,    // clients
            ["425"]  = -4000m,    // rémunérations dues (paie — jamais 431, plan §1.4)
            ["432"]  = -800m,     // RAS (paie)
            ["4366"] = 1000m,     // TVA déductible (créance)
            ["4367"] = -1500m,    // TVA collectée (dette)
            ["453"]  = -1500m,    // CNSS (paie)
            ["5061"] = -4000m,    // concours bancaire (trésorerie passive)
            ["532"]  = 30000m     // banque
        };
        prev["101"] = -prev.Values.Sum(); // bouchon : Σ(classes 1-5) = 0
        // P&N N-1 : REX = 86 400 − 45 000 = 41 400 ; 691 = 41 400 → résultat net nul.
        prev["601"] = 30000m;
        prev["64"]  = 10000m;
        prev["68"]  = 5000m;
        prev["691"] = 41400m;
        prev["70"]  = -86400m;

        // ── N (exercice courant) ───────────────────────────────────────────────────
        // Apport en capital (+30 000 au capital souscrit) et appel de 5 000 du non appelé
        // (1011 : 20 000 → 15 000) → composante CAP +35 000 ; remboursement d'emprunt (−10 000) ;
        // acquisition incorporelle (+5 000) ; cession d'un bâtiment (222 : 100 000 → 80 000 ;
        // 2822 : −20 000 → −11 000 ; plus-value 736 = 2 000). 532 = bouchon pour équilibrer.
        var cur = new Dictionary<string, decimal>
        {
            ["101"]  = -218200m,
            ["1011"] = 15000m,
            ["162"]  = -50000m,
            ["213"]  = 45000m,
            ["221"]  = 60000m,
            ["222"]  = 80000m,    // cession d'un bâtiment (brut sorti 20 000)
            ["251"]  = 15000m,
            ["256"]  = 5000m,
            ["281"]  = -9000m,    // +1 000 dotation ordinaire
            ["2821"] = -5000m,
            ["2822"] = -11000m,   // −20 000 + 12 000 (sortis à la cession) − 3 000 (dotation)
            ["291"]  = -2000m,
            ["31"]   = 14000m,
            ["37"]   = 7000m,
            ["39"]   = -3500m,
            ["401"]  = -22000m,
            ["411"]  = 33000m,
            ["425"]  = -4500m,
            ["432"]  = -900m,
            ["4366"] = 1500m,
            ["4367"] = -2200m,
            ["453"]  = -1700m,
            ["5061"] = -3000m,
            ["601"]  = 35000m,
            ["64"]   = 13000m,
            ["68"]   = 4000m,
            ["691"]  = 4500m,
            ["70"]   = -100000m,
            ["736"]  = -2000m     // plus-value de cession (crédit)
        };
        cur["532"] = -cur.Values.Sum(); // bouchon : Σ(tous comptes) = 0

        // Cession du bâtiment : amortissements repris = 12 000, prix de cession = 10 000.
        var disposals = new NctDisposalAggregates(DepreciationRemoved: 12000m, Proceeds: 10000m);
        return (cur, prev, disposals);
    }

    [Fact]
    public void BalanceSheet_BothYears_Balanced_NetRubriquesNonNegative_AncByNature()
    {
        var (cur, prev, disposals) = ConformityBiExercice();
        var bs = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals).BalanceSheet;
        decimal A(string code) => bs.Assets.First(l => l.Code == code).Amount;
        decimal PA(string code) => bs.Assets.First(l => l.Code == code).PreviousAmount;

        // Bilan N et N-1 équilibrés (T3/T9) — écart Actif/Passif < tolérance.
        Assert.True(bs.IsBalanced);
        Assert.True(Math.Abs(bs.Difference) < 0.001m);
        Assert.Equal(bs.TotalAssets, bs.TotalEquityAndLiabilities);
        Assert.Equal(bs.PreviousTotalAssets, bs.PreviousTotalEquityAndLiabilities);

        // Rubriques nettes ≥ 0 (T1/T9).
        Assert.True(A("ANC1") >= 0m);
        Assert.True(A("ANC2") >= 0m);
        Assert.True(A("ANC3") >= 0m);
        Assert.True(A("AC1") >= 0m);

        // ANC par nature conformes (T1) : 28x/29x imputés sur la nature corrigée, pas de rubrique
        // d'amortissement isolée. NCT 01 — modèle de bilan : immobilisations présentées nettes.
        Assert.Equal(34000m, A("ANC1"));    // 213 − 281 − 291 = 45 000 − 9 000 − 2 000
        Assert.Equal(124000m, A("ANC2"));   // 221 + 222 − 2821 − 2822 = 60 000 + 80 000 − 5 000 − 11 000
        Assert.Equal(20000m, A("ANC3"));    // 251 + 256
        Assert.Equal(0m, A("ANC4"));        // pas de 27x dans la fixture
        Assert.DoesNotContain(bs.Assets, l => l.Label.Contains("Amortissement"));
        // 1011 en sous-ligne de CP1 (T1), jamais reclassé à l'actif (NCT 01 : composante corrective
        // des capitaux propres, présentation en moins du capital).
        Assert.Contains(bs.EquityAndLiabilities, l => l.Code == "CP1A" && l.Label.Contains("1011"));
        Assert.DoesNotContain(bs.Assets, l => l.Label.Contains("non appelé"));
        // Comparatif N-1 : ANC net par nature également conformes.
        Assert.Equal(30000m, PA("ANC1"));   // 40 000 − 8 000 − 2 000
        Assert.Equal(135000m, PA("ANC2"));  // 60 000 + 100 000 − 5 000 − 20 000
    }

    [Fact]
    public void IncomeStatement_RexRai_Populated_BothYears_NoExtraordinaryLines()
    {
        var (cur, prev, disposals) = ConformityBiExercice();
        var inc = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals).IncomeStatement;
        decimal Prev(string code) => inc.Lines.First(l => l.Code == code).PreviousAmount;

        // REX/RAI N et N-1 renseignés (T4/T9).
        Assert.NotEqual(0m, inc.OperatingResult);   // REX(N) = 50 000
        Assert.NotEqual(0m, inc.ResultBeforeTax);   // RAI(N) = 50 000
        Assert.NotEqual(0m, Prev("REX"));           // REX(N-1) = 41 400
        Assert.NotEqual(0m, Prev("RAI"));           // RAI(N-1) = 41 400
        // Résultat net N-1 nul (691 absorbe le RAI) — condition nécessaire au rapprochement du flux
        // de trésorerie, voir CashFlow_Reconciled.
        Assert.Equal(0m, inc.PreviousNetResult);
        Assert.Equal(45500m, inc.NetResult);        // RAI(N) − 691 = 50 000 − 4 500
        // Aucun élément extraordinaire dans la fixture (pas de 67x/77x/697x) → lignes GEX/PEXX/IEX/RNE
        // omises (T4). Le compte 736 (plus-value de cession) est classé en exploitation, pas extraord.
        Assert.DoesNotContain(inc.Lines, l => l.Code is "GEX" or "PEXX" or "IEX" or "RNE");
        Assert.Equal(102000m, inc.Lines.First(l => l.Code == "PEX").Amount); // 100 000 ventes + 2 000 cession
    }

    [Fact]
    public void CashFlow_Reconciled_WithTvaPayrollDisposal()
    {
        var (cur, prev, disposals) = ConformityBiExercice();
        var cf = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals).CashFlow;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        // CFECART ≈ 0 (IsReconciled = true) avec TVA (4366/4367), paie (425/432/453) et cession (T5/T9).
        Assert.True(cf.IsReconciled);
        Assert.True(Math.Abs(L("CFECART")) < 0.001m);
        Assert.Equal(cf.NetChange, cf.OperatingCashFlow + cf.InvestingCashFlow + cf.FinancingCashFlow);

        // TVA déductible (4366, créance) captée par CFVAC ; TVA collectée (4367) + paie par CFVAD.
        Assert.NotEqual(0m, L("CFVAC"));
        Assert.NotEqual(0m, L("CFVAD"));
        // Cession : CF5 neutralise le gain 736 de l'exploitation, CFINV2 le reclassé en investissement.
        Assert.Equal(-2000m, L("CF5"));
        Assert.Equal(2000m, L("CFINV2"));

        // NOTE — rapprochement du flux (T5 suivi). Le builder reçoit un SEUL dictionnaire comparatif
        // `previousNet` servi à la fois pour le compte de résultat N-1 (qui requiert les classes 6/7
        // porteuses du résultat N-1, cf. T4 « REX/RAI N-1 ≠ 0 ») et pour les soldes d'ouverture du flux
        // (qui devraient être un bilan post-affectation, classes 6/7 soldées). Historiquement incompatibles
        // dès que NetResult(N-1) ≠ 0 (CFECART valait −NetResult(N-1)) : la fixture T9 fixait donc
        // NetResult(N-1) = 0 (691 absorbe le RAI). La ligne de financement annule désormais la part du
        // résultat N-1 non affecté portée en capitaux propres d'ouverture (cf. BuildCashFlow, NetPnl) :
        // CFECART = 0 quel que soit NetResult(N-1), prouvé par CashFlow_PriorYear_Unaffected_Reconciles
        // et CashFlow_ProfitableN_WithDisposalPayrollTva_AndUnaffectedN1_Reconciles. Cette fixture
        // (NetResult(N-1) = 0) reste le cas de non-régression où NetPnl(prev) = 0 (correctif no-op).
    }

    [Fact]
    public void Equity_OpeningEqualsN1Closing_AndFourReconciliationInvariants()
    {
        var (cur, prev, disposals) = ConformityBiExercice();
        var eq = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals).EquityChanges;
        var components = eq.Components;

        // Ouverture(N) = clôture(N-1) (T9). On recalcule la clôture N-1 en traitant N-1 comme l'exercice
        // courant avec un comparatif vide (premier exercice → ouverture nulle).
        var n1Closing = NctStatementBuilder.Build(2025, prev, new Dictionary<string, decimal>(), enabled: true)
            .EquityChanges.ClosingEquity;
        Assert.Equal(n1Closing, eq.OpeningEquity);

        // Quatre invariants de réconciliation (T6) — tiennent par construction, testés ici.
        Assert.Equal(eq.OpeningEquity, components.Sum(c => c.Opening));
        Assert.Equal(eq.ClosingEquity, components.Sum(c => c.Closing));
        Assert.Equal(eq.NetResult, components.Sum(c => c.PeriodResult));
        Assert.Equal(eq.Lines.First(l => l.Code == "EQ2").Amount, components.Sum(c => c.OtherMovements));

        // Cinq composantes présentes (CAP/RES/REP/AUT/RSX) ; RSX porte seul le résultat de l'exercice.
        Assert.Equal(new[] { "CAP", "RES", "REP", "AUT", "RSX" }, components.Select(c => c.Code).ToArray());
        var rsx = components.First(c => c.Code == "RSX");
        Assert.Equal(eq.NetResult, rsx.PeriodResult);
        Assert.All(components.Where(c => c.Code != "RSX"), c => Assert.Equal(0m, c.PeriodResult));
        // Cohérence par composante (T9) : OtherMovements = Clôture − Ouverture − Résultat.
        Assert.All(components, c => Assert.Equal(c.Closing - c.Opening - c.PeriodResult, c.OtherMovements));
        // Apport en capital N (+30 000) et appel de 5 000 du non appelé → composante CAP +35 000.
        Assert.Equal(35000m, components.First(c => c.Code == "CAP").OtherMovements);
    }

    // Plan T5 — fixture de cession isolée : brut 100, amortissements cumulés 60, prix 50, plus-value 10.
    // Écritures du générateur : débit 532 = 50, débit 2821 = 60, crédit 221 = 100, crédit 736 = 10.
    [Fact]
    public void Disposal_Marked_CessionComponentEqualsProceeds_AndReconciles()
    {
        var prev = new Dictionary<string, decimal> { ["221"] = 100m, ["2821"] = -60m }; // avant cession
        var cur = new Dictionary<string, decimal> { ["532"] = 50m, ["736"] = -10m };    // après cession
        var disposals = new NctDisposalAggregates(DepreciationRemoved: 60m, Proceeds: 50m);

        var cf = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals).CashFlow;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        Assert.Equal(0m, L("CF1"));       // (−60) + 60 = 0
        Assert.Equal(-10m, L("CF5"));     // −(gain 736) = −10
        Assert.Equal(0m, cf.OperatingCashFlow);              // 10 (résultat) + 0 + (−10) = 0
        Assert.Equal(40m, L("CFINV1"));   // 100 − 60 = 40
        Assert.Equal(10m, L("CFINV2"));   // +10
        Assert.Equal(50m, cf.InvestingCashFlow);             // 40 + 10 = 50 = prix de cession
        // Invariant T5 : composante cession du flux d'investissement == Proceeds.
        Assert.Equal(disposals.Proceeds, cf.InvestingCashFlow);
        Assert.True(cf.IsReconciled);     // CFECART = 0 (comparatif sans P&N)
        Assert.Equal(50m, cf.NetChange);  // Δtrésorerie = 50
    }

    // Cession équivalente saisie manuellement (sans marquage source) → agrégats nuls.
    //
    // NOTE — écart avec le libellé du plan T5 qui attend « IsReconciled = false » pour ce cas : c'est
    // impossible par construction (CFECART = −NetResult(comparatif) ; ici le comparatif n'a pas de P&N,
    // donc CFECART = 0 quel que soit l'agrégat). Le vrai défaut d'une cession non marquée n'est pas un
    // échec de rapprochement mais une MISCLASSIFICATION : CF1 capte la sortie d'amortissement (−60)
    // comme une fausse dotation et CFINV1 capte le brut sorti (100) comme une acquisition — la composante
    // cession du flux d'investissement (110) ≠ Proceeds (50). Le total (Δtrésorerie) reste exact.
    [Fact]
    public void Disposal_Unmarked_ReconciliationHolds_MisclassificationDocumented()
    {
        var prev = new Dictionary<string, decimal> { ["221"] = 100m, ["2821"] = -60m };
        var cur = new Dictionary<string, decimal> { ["532"] = 50m, ["736"] = -10m };

        var cf = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals: null).CashFlow;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        // Le rapprochement tient (tautologique : comparatif sans P&N → CFECART = 0).
        Assert.True(cf.IsReconciled);
        Assert.Equal(50m, cf.NetChange);                 // Δtrésorerie identique au cas marqué

        // …mais la répartition exploitation/investissement est erronée (misclassification) :
        Assert.Equal(-60m, cf.OperatingCashFlow);        // capte la sortie d'amortissement comme dotation
        Assert.Equal(-60m, L("CF1"));                    // (−60) + 0 (agrégat nul)
        Assert.Equal(110m, cf.InvestingCashFlow);        // brut sorti + résultat reclassé, ≠ Proceeds
        Assert.Equal(100m, L("CFINV1"));                 // brut sorti (100) lu comme acquisition
        Assert.Equal(10m, L("CFINV2"));                  // inchangé (basé sur les soldes 736)
        // La composante cession du flux d'investissement (110) ≠ Proceeds (50) : défaut observable réel.
        Assert.NotEqual(50m, cf.InvestingCashFlow);
    }

    // ── T5 suivi : rapprochement du flux quel que soit NetResult(N-1) ────────────
    //
    // La ligne de financement annule désormais la part du résultat N-1 non affecté portée en capitaux
    // propres d'ouverture (BuildCashFlow, NetPnl). NetPnl(prev) = Σ net(6/7) = −NetResult(N-1) si le
    // résultat est non affecté (encore en 6/7), 0 si l'exercice est clôturé/ancré (écritures de clôture
    // soldant 6/7). CFECART doit valoir 0 (IsReconciled) dans tous les cas ci-dessous.

    // Cas 1 — N-1 bénéficiaire et NON affecté : les classes 6/7 portent le résultat (pas de 121/13x pour
    // le résultat N-1) ; ouverture de N via à-nouveaux (trésorerie reportée + résultat N-1 porté en 131).
    // Aucune activité en N → Δtrésorerie = 0. Sans correctif, CFECART = −40 (−NetResult(N-1)).
    [Fact]
    public void CashFlow_PriorYear_Unaffected_Reconciles()
    {
        // N-1 : produits 100 (70, crédit) / charges 60 (601, débit) ; résultat 40 non affecté, porté
        // par les classes 6/7 ; la trésorerie (532 = 40) équilibre la balance (Σ = 0).
        var prev = new Dictionary<string, decimal>
        {
            ["70"]  = -100m,
            ["601"] = 60m,
            ["532"] = 40m
        };
        // N : aucune activité P&L ; à-nouveaux reportant la trésorerie (532 = 40) et le résultat N-1
        // en 131 (crédit 40). Δtrésorerie = 0, balance équilibrée (Σ = 0).
        var cur = new Dictionary<string, decimal>
        {
            ["532"] = 40m,
            ["131"] = -40m
        };

        var cf = NctStatementBuilder.Build(2026, cur, prev, enabled: true).CashFlow;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        Assert.True(cf.IsReconciled);
        Assert.True(Math.Abs(L("CFECART")) < 0.001m);
        Assert.Equal(0m, cf.NetChange);              // Δtrésorerie = 0
        // NetPnl(prev) = −40 annule le financement issu du 131 d'ouverture : CFFIN = 0.
        Assert.Equal(0m, cf.FinancingCashFlow);
    }

    // Cas 2 — N-1 dont le résultat est déjà affecté (121 « Résultats reportés » mouventé, classes 6/7
    // soldées) : NetPnl(prev) = 0 → correctif no-op. Non-régression : CFECART = 0.
    [Fact]
    public void CashFlow_PriorYear_Affected_NoRegression()
    {
        // N-1 : résultat 40 affecté en 121 (crédit) ; classes 6/7 soldées. Trésorerie 40. Σ = 0.
        var prev = new Dictionary<string, decimal>
        {
            ["532"] = 40m,
            ["121"] = -40m
        };
        // N : aucune activité ; report à l'identique. Δtrésorerie = 0.
        var cur = new Dictionary<string, decimal>
        {
            ["532"] = 40m,
            ["121"] = -40m
        };

        var cf = NctStatementBuilder.Build(2026, cur, prev, enabled: true).CashFlow;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        Assert.True(cf.IsReconciled);
        Assert.True(Math.Abs(L("CFECART")) < 0.001m);
        Assert.Equal(0m, cf.NetChange);
        Assert.Equal(0m, cf.FinancingCashFlow);      // NetPnl(prev) = 0 → no-op
    }

    // Cas 4 — N bénéficiaire avec cession + paie + TVA, ET N-1 bénéficiaire non affecté : extension de
    // la fixture T9 (ConformityBiExercice) où l'on retire l'absorption 691 de N-1 pour rendre son
    // résultat (41 400) non affecté. N conserve son résultat (45 500), sa cession, sa paie et sa TVA.
    [Fact]
    public void CashFlow_ProfitableN_WithDisposalPayrollTva_AndUnaffectedN1_Reconciles()
    {
        var (cur, prev, disposals) = ConformityBiExercice_UnaffectedN1();
        var stmt = NctStatementBuilder.Build(2026, cur, prev, enabled: true, disposals);
        var cf = stmt.CashFlow;
        var inc = stmt.IncomeStatement;
        decimal L(string code) => cf.Lines.First(l => l.Code == code).Amount;

        // Préconditions : N-1 bénéficiaire non affecté (NetResult(N-1) = 41 400), N bénéficiaire (45 500).
        Assert.Equal(41400m, inc.PreviousNetResult);
        Assert.Equal(45500m, inc.NetResult);

        // Rapprochement tient (T5 suivi) : CFECART = 0 malgré NetResult(N-1) ≠ 0.
        Assert.True(cf.IsReconciled);
        Assert.True(Math.Abs(L("CFECART")) < 0.001m);
        Assert.Equal(cf.NetChange, cf.OperatingCashFlow + cf.InvestingCashFlow + cf.FinancingCashFlow);
        // Cession toujours reclassée (T5) : CF5 neutralise le gain 736, CFINV2 le reclassé.
        Assert.Equal(-2000m, L("CF5"));
        Assert.Equal(2000m, L("CFINV2"));
        // TVA + paie toujours captées (CFVAC/CFVAD non nulles).
        Assert.NotEqual(0m, L("CFVAC"));
        Assert.NotEqual(0m, L("CFVAD"));
    }

    // Variante de la fixture T9 : N-1 rendu non affecté (retrait de l'absorption 691, rééquilibrage du
    // bouchon 101) — le résultat N-1 (41 400) demeure porté par les classes 6/7 et les actifs, non
    // repris dans la classe 1 d'ouverture. N (cur) et les agrégats de cession sont inchangés.
    private static (Dictionary<string, decimal> Cur, Dictionary<string, decimal> Prev, NctDisposalAggregates Disposals) ConformityBiExercice_UnaffectedN1()
    {
        var (cur, prev, disposals) = ConformityBiExercice();
        prev.Remove("691");   // le résultat N-1 n'est plus absorbé : il reste en classes 6/7
        var sumExcl101 = prev.Where(kv => kv.Key != "101").Sum(kv => kv.Value);
        prev["101"] = -sumExcl101;   // bouchon : Σ(tous comptes) = 0 (résultat N-1 dans les actifs)
        return (cur, prev, disposals);
    }
}
