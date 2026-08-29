using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Assemble la liasse NCT à partir des soldes de comptes (net = débit − crédit) de l'exercice N et N-1.
/// Bilan et compte de résultat sont EXACTS (Actif=Passif ; résultat net = produits − charges).
/// Flux de trésorerie (méthode indirecte) et variation des capitaux propres incluent une ligne de
/// rapprochement explicite. Aucune requête ici : les soldes sont fournis par le service appelant.
/// </summary>
public static class NctStatementBuilder
{
    private const decimal Tolerance = 0.001m;

    public static NctFinancialStatementsDto Build(
        int fiscalYear,
        IReadOnlyDictionary<string, decimal> currentNet,
        IReadOnlyDictionary<string, decimal> previousNet,
        bool enabled,
        NctDisposalAggregates? disposals = null,
        IReadOnlyList<string>? externalWarnings = null)
    {
        disposals ??= new NctDisposalAggregates(0m, 0m);

        var balanceSheet = BuildBalanceSheet(currentNet, previousNet, externalWarnings ?? Array.Empty<string>());
        var income = BuildIncomeStatement(currentNet, previousNet);
        var cashFlow = BuildCashFlow(currentNet, previousNet, income.NetResult, disposals);
        var equity = BuildEquityChanges(currentNet, previousNet, income.NetResult);
        var notes = BuildNotes(currentNet, previousNet);

        return new NctFinancialStatementsDto
        {
            FiscalYear = fiscalYear,
            BalanceSheet = balanceSheet,
            IncomeStatement = income,
            CashFlow = cashFlow,
            EquityChanges = equity,
            Notes = notes,
            NctStatementsEnabled = enabled
        };
    }

    // ── Bilan ─────────────────────────────────────────────────────────────────

    private static NctBalanceSheetDto BuildBalanceSheet(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev,
        IReadOnlyList<string> externalWarnings)
    {
        decimal CurA(Func<string, decimal, bool> p) => SumWhere(cur, p);
        decimal PrevA(Func<string, decimal, bool> p) => SumWhere(prev, p);

        // Actif non courant, net par nature (T1) : les amortissements/provisions (28x/29x) ne
        // forment plus une rubrique propre — chacun s'impute sur la rubrique de la nature qu'il
        // corrige (281/291 → ANC1, 282/284/292/293/294 → ANC2, 295/296 → ANC3). 27x seul → ANC4.
        bool IsAnc1(string a) => Root2(a) == "21" || Root3(a) is "281" or "291";
        bool IsAnc2(string a) => Root2(a) is "22" or "23" or "24" || Root3(a) is "282" or "284" or "292" or "293" or "294";
        bool IsAnc3(string a) => Root2(a) is "25" or "26" || Root3(a) is "295" or "296";
        bool IsAnc4(string a) => Root2(a) == "27";

        var incorp = (CurA((a, n) => Cls(a) == 2 && IsAnc1(a)), PrevA((a, n) => Cls(a) == 2 && IsAnc1(a)));
        var corp = (CurA((a, n) => Cls(a) == 2 && IsAnc2(a)), PrevA((a, n) => Cls(a) == 2 && IsAnc2(a)));
        var fin = (CurA((a, n) => Cls(a) == 2 && IsAnc3(a)), PrevA((a, n) => Cls(a) == 2 && IsAnc3(a)));
        var autresNc = (CurA((a, n) => Cls(a) == 2 && IsAnc4(a)), PrevA((a, n) => Cls(a) == 2 && IsAnc4(a)));
        var ancTotal = (incorp.Item1 + corp.Item1 + fin.Item1 + autresNc.Item1,
                         incorp.Item2 + corp.Item2 + fin.Item2 + autresNc.Item2);

        var stocks = (CurA((a, n) => Cls(a) == 3), PrevA((a, n) => Cls(a) == 3));
        var clients = (CurA((a, n) => Cls(a) == 4 && n > 0 && IsClient(a)), PrevA((a, n) => Cls(a) == 4 && n > 0 && IsClient(a)));
        var otherCurAssets = (CurA((a, n) => Cls(a) == 4 && n > 0 && !IsClient(a)), PrevA((a, n) => Cls(a) == 4 && n > 0 && !IsClient(a)));
        var cash = (CurA((a, n) => Cls(a) == 5 && n > 0), PrevA((a, n) => Cls(a) == 5 && n > 0));
        var acTotal = (stocks.Item1 + clients.Item1 + otherCurAssets.Item1 + cash.Item1,
                       stocks.Item2 + clients.Item2 + otherCurAssets.Item2 + cash.Item2);

        var assets = new List<NctLineDto>
        {
            Line("ANC", "ACTIFS NON COURANTS", ancTotal, subtotal: true, level: 0),
            Line("ANC1", "Immobilisations incorporelles", incorp, level: 1),
            Line("ANC2", "Immobilisations corporelles", corp, level: 1),
            Line("ANC3", "Immobilisations financières", fin, level: 1),
            Line("ANC4", "Autres actifs non courants", autresNc, level: 1),
            Line("AC", "ACTIFS COURANTS", acTotal, subtotal: true, level: 0),
            Line("AC1", "Stocks", stocks, level: 1),
            Line("AC2", "Clients et comptes rattachés", clients, level: 1),
            Line("AC3", "Autres actifs courants", otherCurAssets, level: 1),
            Line("AC4", "Liquidités et équivalents", cash, level: 1)
        };
        var totalAssets = (ancTotal.Item1 + acTotal.Item1, ancTotal.Item2 + acTotal.Item2);
        assets.Add(Line("TA", "TOTAL DES ACTIFS", totalAssets, subtotal: true, level: 0));

        // Capitaux propres et passifs — valeurs présentées en crédit − débit (= −net).
        decimal CurL(Func<string, decimal, bool> p) => -SumWhere(cur, p);
        decimal PrevL(Func<string, decimal, bool> p) => -SumWhere(prev, p);

        var capital = (CurL((a, n) => a.StartsWith("10")), PrevL((a, n) => a.StartsWith("10")));
        // 1011 « Capital souscrit − non appelé » (T1) : composante corrective, déjà en déduction dans
        // CP1 (le prédicat StartsWith("10") l'inclut) ; sous-ligne informative, jamais reclassée à l'actif.
        var capital1011 = (CurL((a, n) => a.StartsWith("1011")), PrevL((a, n) => a.StartsWith("1011")));

        var reserves = (CurL((a, n) => Cls(a) == 1 && (a.StartsWith("11") || a.StartsWith("14"))),
                        PrevL((a, n) => Cls(a) == 1 && (a.StartsWith("11") || a.StartsWith("14"))));
        var retained = (CurL((a, n) => a.StartsWith("12")), PrevL((a, n) => a.StartsWith("12")));

        // T2 — Résultat de l'exercice = solde comptable du compte 13x (crédit − débit) + résultat
        // calculé sur les classes 6/7 non encore soldées. Anti-double-compte : après affectation du
        // résultat (13x soldé vers 11x/12x/457), la composante 13x est nulle et seul Result(cur)
        // porte le résultat en cours ; après à-nouveaux (résultat N-1 porté en 131/135 par l'AN),
        // 13x est mouvementé mais les classes 6/7 de N-1 ne réapparaissent jamais dans "cur"
        // (T7 : LoadYearNetAsync n'alimente jamais les comptes 6/7 avec un historique).
        var bal13Cur = CurL((a, n) => a.StartsWith("13"));
        var bal13Prev = PrevL((a, n) => a.StartsWith("13"));
        var resultN = Result(cur) + bal13Cur;
        var resultPrev = Result(prev) + bal13Prev;
        var equityTotal = (capital.Item1 + reserves.Item1 + retained.Item1 + resultN,
                           capital.Item2 + reserves.Item2 + retained.Item2 + resultPrev);

        // Passifs non courants : provisions (15), emprunts (16,17) et autres classe 1 hors capitaux
        // propres (10/11/12/13/14) — 13x en est désormais exclu (T2, évite le double compte avec CP4).
        bool IsNonCurrentLiab(string a) =>
            Cls(a) == 1 && !a.StartsWith("10") && !a.StartsWith("11") && !a.StartsWith("12")
                        && !a.StartsWith("13") && !a.StartsWith("14");
        var nonCurrentLiab = (CurL((a, n) => IsNonCurrentLiab(a)), PrevL((a, n) => IsNonCurrentLiab(a)));
        var provisions = (CurL((a, n) => a.StartsWith("15")), PrevL((a, n) => a.StartsWith("15")));

        var suppliers = (CurL((a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a)), PrevL((a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a)));
        var otherCurLiab = (CurL((a, n) => (Cls(a) == 4 && n < 0 && !IsSupplier(a)) || (Cls(a) == 5 && n < 0)),
                            PrevL((a, n) => (Cls(a) == 4 && n < 0 && !IsSupplier(a)) || (Cls(a) == 5 && n < 0)));
        var currentLiab = (suppliers.Item1 + otherCurLiab.Item1, suppliers.Item2 + otherCurLiab.Item2);

        var passif = new List<NctLineDto>
        {
            Line("CP", "CAPITAUX PROPRES", equityTotal, subtotal: true, level: 0),
            Line("CP1", "Capital", capital, level: 1),
            Line("CP1A", "dont capital souscrit non appelé (1011)", capital1011, level: 2),
            Line("CP2", "Réserves", reserves, level: 1),
            Line("CP3", "Résultats reportés", retained, level: 1),
            Line("CP4", "Résultat de l'exercice", (resultN, resultPrev), level: 1),
            Line("PNC", "PASSIFS NON COURANTS", nonCurrentLiab, subtotal: true, level: 0),
            Line("PNC1", "Provisions pour risques et charges", provisions, level: 1),
            Line("PC", "PASSIFS COURANTS", currentLiab, subtotal: true, level: 0),
            Line("PC1", "Fournisseurs et comptes rattachés", suppliers, level: 1),
            Line("PC2", "Autres passifs courants", otherCurLiab, level: 1)
        };
        var totalEq = (equityTotal.Item1 + nonCurrentLiab.Item1 + currentLiab.Item1,
                       equityTotal.Item2 + nonCurrentLiab.Item2 + currentLiab.Item2);
        passif.Add(Line("TP", "TOTAL CAPITAUX PROPRES ET PASSIFS", totalEq, subtotal: true, level: 0));

        var difference = totalAssets.Item1 - totalEq.Item1;

        // T3 — Avertissements de qualité des données.
        var warnings = new List<string>(externalWarnings);
        if (Math.Abs(difference) >= Tolerance)
            warnings.Add($"Bilan non équilibré : écart de {difference:0.###} entre le total des actifs et le total des capitaux propres et passifs.");
        if (stocks.Item1 < 0) warnings.Add("Stocks présentent un solde négatif — vérifier la saisie.");
        if (clients.Item1 < 0) warnings.Add("Clients et comptes rattachés présentent un solde négatif — vérifier la saisie.");
        if (cash.Item1 < 0) warnings.Add("Liquidités et équivalents présentent un solde négatif — vérifier la saisie.");

        // T1 — Anomalies de sens du compte 1011.
        var net1011 = SumWhere(cur, (a, n) => a.StartsWith("1011")); // débit − crédit (sens naturel : débiteur)
        var capitalAppele = CurL((a, n) => a.StartsWith("10") && !a.StartsWith("1011"));
        if (net1011 > 0 && net1011 > capitalAppele)
            warnings.Add("Capital souscrit non appelé (1011) supérieur au capital appelé — vérifier la saisie.");
        if (net1011 < 0)
            warnings.Add("Compte 1011 en position créditrice — anomalie de sens (1011 est normalement débiteur).");

        return new NctBalanceSheetDto
        {
            Assets = assets,
            EquityAndLiabilities = passif,
            TotalAssets = totalAssets.Item1,
            TotalEquityAndLiabilities = totalEq.Item1,
            PreviousTotalAssets = totalAssets.Item2,
            PreviousTotalEquityAndLiabilities = totalEq.Item2,
            IsBalanced = Math.Abs(difference) < Tolerance,
            Difference = difference,
            Warnings = warnings
        };
    }

    // ── Compte de résultat ────────────────────────────────────────────────────

    /// <summary>Décomposition du compte de résultat (résultat ordinaire / extraordinaire — T4).</summary>
    private sealed record IncomeComputation(
        decimal ProdExpl, decimal ChargeExpl, decimal ProdFin, decimal ChargeFin, decimal TaxOrdinaire,
        decimal Rex, decimal Rfi, decimal Rai, decimal Rao,
        decimal GainsExtra, decimal PertesExtra, decimal ImpotExtra, decimal Rne, decimal Rn);

    private static IncomeComputation ComputeIncome(IReadOnlyDictionary<string, decimal> d)
    {
        decimal Prod(Func<string, bool> p) => -SumWhere(d, (a, n) => Cls(a) == 7 && p(a));
        decimal Charge(Func<string, bool> p) => SumWhere(d, (a, n) => Cls(a) == 6 && p(a));

        bool FinancialProd(string a) => a.StartsWith("75") || a.StartsWith("76");
        bool FinancialCharge(string a) => a.StartsWith("65") || a.StartsWith("66");
        bool Tax(string a) => a.StartsWith("69");
        bool Extra(string a) => a.StartsWith("67") || a.StartsWith("77");

        var prodExpl = Prod(a => !FinancialProd(a) && !Extra(a));
        var chargeExpl = Charge(a => !FinancialCharge(a) && !Tax(a) && !Extra(a));
        var prodFin = Prod(FinancialProd);
        var chargeFin = Charge(FinancialCharge);
        var taxOrdinaire = Charge(a => Tax(a) && !a.StartsWith("697"));

        var rex = prodExpl - chargeExpl;
        var rfi = prodFin - chargeFin;
        var rai = rex + rfi;
        var rao = rai - taxOrdinaire;

        var gainsExtra = Prod(a => a.StartsWith("77"));
        var pertesExtra = Charge(a => a.StartsWith("67"));
        var impotExtra = Charge(a => a.StartsWith("697"));
        var rne = gainsExtra - pertesExtra - impotExtra;

        var rn = rao + rne;

        return new IncomeComputation(prodExpl, chargeExpl, prodFin, chargeFin, taxOrdinaire,
            rex, rfi, rai, rao, gainsExtra, pertesExtra, impotExtra, rne, rn);
    }

    private static NctIncomeStatementDto BuildIncomeStatement(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev)
    {
        var c = ComputeIncome(cur);
        var p = ComputeIncome(prev);

        var lines = new List<NctLineDto>
        {
            Line("PEX", "Produits d'exploitation", (c.ProdExpl, p.ProdExpl), level: 0),
            Line("CEX", "Charges d'exploitation", (c.ChargeExpl, p.ChargeExpl), level: 0),
            Line("REX", "Résultat d'exploitation", (c.Rex, p.Rex), subtotal: true, level: 0),
            Line("PFI", "Produits financiers", (c.ProdFin, p.ProdFin), level: 0),
            Line("CFI", "Charges financières", (c.ChargeFin, p.ChargeFin), level: 0),
            Line("RAI", "Résultat des activités ordinaires avant impôt", (c.Rai, p.Rai), subtotal: true, level: 0),
            Line("IMP", "Impôt sur les bénéfices (ordinaire)", (c.TaxOrdinaire, p.TaxOrdinaire), level: 0),
            Line("RAO", "Résultat des activités ordinaires après impôt", (c.Rao, p.Rao), subtotal: true, level: 0)
        };

        // Éléments extraordinaires (67/77/697) présentés séparément — omis si tous les soldes sont
        // nuls sur les deux exercices (pas de lignes vides, T4).
        var hasExtra = c.GainsExtra != 0m || c.PertesExtra != 0m || c.ImpotExtra != 0m
                       || p.GainsExtra != 0m || p.PertesExtra != 0m || p.ImpotExtra != 0m;
        if (hasExtra)
        {
            lines.Add(Line("GEX", "Gains extraordinaires", (c.GainsExtra, p.GainsExtra), level: 0));
            lines.Add(Line("PEXX", "Pertes extraordinaires", (c.PertesExtra, p.PertesExtra), level: 0));
            lines.Add(Line("IEX", "Impôt sur éléments extraordinaires", (c.ImpotExtra, p.ImpotExtra), level: 0));
            lines.Add(Line("RNE", "Résultat extraordinaire net", (c.Rne, p.Rne), subtotal: true, level: 0));
        }

        lines.Add(Line("RN", "RÉSULTAT NET", (c.Rn, p.Rn), subtotal: true, level: 0));

        return new NctIncomeStatementDto
        {
            Lines = lines,
            OperatingResult = c.Rex,
            FinancialResult = c.Rfi,
            ResultBeforeTax = c.Rai,
            NetResult = c.Rn,
            PreviousNetResult = p.Rn
        };
    }

    // ── Tableau de flux de trésorerie (méthode indirecte) ──────────────────────

    private static NctCashFlowDto BuildCashFlow(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev,
        decimal netResult, NctDisposalAggregates disposals)
    {
        decimal Net(IReadOnlyDictionary<string, decimal> d, Func<string, bool> p) => SumWhere(d, (a, n) => p(a));

        // CF1 — Ajustements non monétaires : variation des comptes de correction (amortissements,
        // provisions sur stocks/créances/risques, provisions pour risques 15x) en solde CRÉDIT,
        // corrigée des amortissements sortis lors des cessions (sinon la sortie du brut lors d'une
        // cession serait à tort lue comme une reprise opérationnelle — arbitrage 5, T5).
        bool CorrectionAcct(string a) => a.StartsWith("28") || a.StartsWith("29") || a.StartsWith("39")
                                          || a.StartsWith("49") || a.StartsWith("59") || a.StartsWith("15");
        decimal CorrectionCredit(IReadOnlyDictionary<string, decimal> d) => -Net(d, CorrectionAcct);
        var dCorrection = CorrectionCredit(cur) - CorrectionCredit(prev);
        var cf1 = dCorrection + disposals.DepreciationRemoved;

        // BFR complet : stocks hors provisions (39x, dans CF1), clients/fournisseurs inchangés,
        // + autres créances/dettes (classe 4 hors client/fournisseur/TVA-paie 49x, déjà dans CF1).
        decimal Stocks(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 3 && !a.StartsWith("39"));
        decimal Clients(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 4 && n > 0 && IsClient(a));
        decimal Suppliers(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a));
        decimal OtherReceivables(IReadOnlyDictionary<string, decimal> d) =>
            SumWhere(d, (a, n) => Cls(a) == 4 && n > 0 && !IsClient(a) && !a.StartsWith("49"));
        decimal OtherPayables(IReadOnlyDictionary<string, decimal> d) =>
            -SumWhere(d, (a, n) => Cls(a) == 4 && n < 0 && !IsSupplier(a) && !a.StartsWith("49"));

        var dStocks = Stocks(cur) - Stocks(prev);
        var dClients = Clients(cur) - Clients(prev);
        var dSuppliers = Suppliers(cur) - Suppliers(prev);
        var dOtherRecv = OtherReceivables(cur) - OtherReceivables(prev);
        var dOtherPay = OtherPayables(cur) - OtherPayables(prev);

        // Cessions : neutralisation du résultat de cession (636/736) de l'exploitation, reclassé en
        // investissement (arbitrage 5, T5) — indépendant de l'agrégat (basé sur les soldes 636/736).
        decimal Gains736(IReadOnlyDictionary<string, decimal> d) => -Net(d, a => a.StartsWith("736"));
        decimal Pertes636(IReadOnlyDictionary<string, decimal> d) => Net(d, a => a.StartsWith("636"));
        var cf5 = -Gains736(cur) + Pertes636(cur);
        var cfinv2 = -cf5;

        var operating = netResult + cf1 - dStocks - dClients + dSuppliers - dOtherRecv + dOtherPay + cf5;

        decimal GrossImmo(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 2 && !a.StartsWith("28") && !a.StartsWith("29"));
        var dGross = GrossImmo(cur) - GrossImmo(prev);
        var cfinv1 = -dGross - disposals.DepreciationRemoved;
        var investing = cfinv1 + cfinv2;

        // Financement : Δ(classe 1 hors 15x, provisions déjà en CF1) + Δcrédit(dettes financières
        // courantes 50x hors 506x, le concours bancaire restant en trésorerie négative).
        decimal RawClass1(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 1 && Root2(a) != "15");
        decimal Raw50(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 5 && Root2(a) == "50" && !a.StartsWith("506"));
        var dClass1 = RawClass1(cur) - RawClass1(prev);
        var d50 = Raw50(cur) - Raw50(prev);

        // T5 suivi — résultat N-1 non affecté. Le comparatif unique `prev` sert à la fois au compte de
        // résultat N-1 (qui requiert les classes 6/7 porteuses du résultat N-1) et au bilan d'ouverture
        // du flux (qui devrait être post-affectation, 6/7 soldés). Dès que NetResult(N-1) ≠ 0 et que ce
        // résultat est encore non affecté (porté par 6/7, NON repris dans la classe 1 d'ouverture —
        // l'injection synthétique 121 de LoadYearNetAsync ne porte que les résultats antérieurs à N-1),
        // le bilan d'ouverture est déséquilibré d'exactement NetResult(N-1) : sans correction,
        // CFECART = −NetResult(N-1). NetPnl(d) = Σ net(6/7) = −NetResult(year) si non affecté, 0 si
        // clôturé/ancré (écritures de clôture soldant 6/7). On retranche du financement la part du
        // résultat N-1 seulement portée en capitaux propres d'ouverture : ouverture(equity) =
        // classe1(prev) − NetPnl(prev) = classe1(prev) + NetResult(N-1). No-op quand NetPnl(prev) = 0
        // (résultat affecté/ancré) → pas de régression pour les tenants déjà rapprochés.
        decimal NetPnl(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 6 || Cls(a) == 7);
        var financing = -dClass1 - d50 + NetPnl(prev);

        var computed = operating + investing + financing;

        // Trésorerie : classe 5 hors dettes financières (50x) et provisions (59x), plus le concours
        // bancaire (506x) réintégré en négatif (trésorerie passive) — arbitrage 5.
        decimal Cash(IReadOnlyDictionary<string, decimal> d) =>
            Net(d, a => Cls(a) == 5 && Root2(a) != "50" && Root2(a) != "59") + Net(d, a => a.StartsWith("506"));
        var openingCash = Cash(prev);
        var closingCash = Cash(cur);
        var actualChange = closingCash - openingCash;
        var gap = actualChange - computed;

        var lines = new List<NctLineDto>
        {
            Line("CF0", "Résultat net de l'exercice", (netResult, 0m), level: 1),
            Line("CF1", "Dotations nettes aux amortissements et provisions", (cf1, 0m), level: 1),
            Line("CF2", "Variation des stocks", (-dStocks, 0m), level: 1),
            Line("CF3", "Variation des créances clients", (-dClients, 0m), level: 1),
            Line("CF4", "Variation des dettes fournisseurs", (dSuppliers, 0m), level: 1),
            Line("CFVAC", "Variation des autres créances", (-dOtherRecv, 0m), level: 1),
            Line("CFVAD", "Variation des autres dettes", (dOtherPay, 0m), level: 1),
            Line("CF5", "Neutralisation du résultat de cession d'immobilisations", (cf5, 0m), level: 1),
            Line("CFOP", "Flux de trésorerie liés à l'exploitation", (operating, 0m), subtotal: true, level: 0),
            Line("CFINV1", "Acquisitions nettes d'immobilisations", (cfinv1, 0m), level: 1),
            Line("CFINV2", "Résultat de cession reclassé", (cfinv2, 0m), level: 1),
            Line("CFINV", "Flux de trésorerie liés à l'investissement", (investing, 0m), subtotal: true, level: 0),
            Line("CFFIN", "Flux de trésorerie liés au financement", (financing, 0m), subtotal: true, level: 0),
            Line("CFECART", "Écart de rapprochement", (gap, 0m), level: 1),
            Line("CFVAR", "Variation de trésorerie", (actualChange, 0m), subtotal: true, level: 0),
            Line("CFOPEN", "Trésorerie à l'ouverture", (openingCash, 0m), level: 1),
            Line("CFCLOSE", "Trésorerie à la clôture", (closingCash, 0m), subtotal: true, level: 0)
        };

        return new NctCashFlowDto
        {
            Lines = lines,
            OperatingCashFlow = operating,
            InvestingCashFlow = investing,
            FinancingCashFlow = financing,
            NetChange = actualChange,
            OpeningCash = openingCash,
            ClosingCash = closingCash,
            IsReconciled = Math.Abs(gap) < Tolerance
        };
    }

    // ── Variation des capitaux propres ─────────────────────────────────────────

    private static NctEquityChangeDto BuildEquityChanges(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev, decimal netResult)
    {
        decimal ByPrefix(IReadOnlyDictionary<string, decimal> d, string prefix) => -SumWhere(d, (a, n) => a.StartsWith(prefix));
        decimal Bal13(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => a.StartsWith("13"));

        // Cohérent avec le bilan (T2) : le résultat porté par 13x (après affectation partielle ou
        // à-nouveaux) s'ajoute au résultat calculé sur les classes 6/7 non soldées.
        var openingEquity = ByPrefix(prev, "10") + ByPrefix(prev, "11") + ByPrefix(prev, "12") + ByPrefix(prev, "14")
                             + Result(prev) + Bal13(prev);
        var closingEquity = ByPrefix(cur, "10") + ByPrefix(cur, "11") + ByPrefix(cur, "12") + ByPrefix(cur, "14")
                             + netResult + Bal13(cur);
        var otherMovements = closingEquity - openingEquity - netResult;

        var lines = new List<NctLineDto>
        {
            Line("EQ0", "Capitaux propres à l'ouverture", (openingEquity, 0m), subtotal: true, level: 0),
            Line("EQ1", "Résultat de l'exercice", (netResult, 0m), level: 1),
            Line("EQ2", "Autres variations (affectations, distributions, capital)", (otherMovements, 0m), level: 1),
            Line("EQ3", "Capitaux propres à la clôture", (closingEquity, 0m), subtotal: true, level: 0)
        };

        // T6 — Décomposition à cinq composantes (CAP/RES/REP/AUT/RSX). Invariants de réconciliation,
        // tenant par construction : ΣOpening = openingEquity ; ΣClosing = closingEquity ;
        // ΣPeriodResult = netResult ; ΣOtherMovements = otherMovements (ligne EQ2).
        NctEquityComponentDto Component(string code, string label, string prefix)
        {
            var opening = ByPrefix(prev, prefix);
            var closing = ByPrefix(cur, prefix);
            return new NctEquityComponentDto
            {
                Code = code,
                Label = label,
                Opening = opening,
                PeriodResult = 0m,
                OtherMovements = closing - opening,
                Closing = closing
            };
        }

        var cap = Component("CAP", "Capital", "10");
        var res = Component("RES", "Réserves et primes", "11");
        var rep = Component("REP", "Résultats reportés", "12");
        var aut = Component("AUT", "Autres", "14");

        var rsxOpening = Result(prev) + Bal13(prev);
        var rsxClosing = netResult + Bal13(cur);
        var rsx = new NctEquityComponentDto
        {
            Code = "RSX",
            Label = "Résultat de l'exercice",
            Opening = rsxOpening,
            PeriodResult = netResult,
            OtherMovements = rsxClosing - rsxOpening - netResult,
            Closing = rsxClosing
        };

        var components = new List<NctEquityComponentDto> { cap, res, rep, aut, rsx };

        return new NctEquityChangeDto
        {
            Lines = lines,
            OpeningEquity = openingEquity,
            NetResult = netResult,
            ClosingEquity = closingEquity,
            Components = components
        };
    }

    // ── Notes annexes ──────────────────────────────────────────────────────────

    private static IReadOnlyList<NctNoteDto> BuildNotes(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev)
    {
        decimal Gross(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 2 && !a.StartsWith("28") && !a.StartsWith("29"));
        decimal Amort(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => a.StartsWith("28") || a.StartsWith("29"));
        decimal Clients(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 4 && n > 0 && IsClient(a));
        decimal OtherRecv(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 4 && n > 0 && !IsClient(a));
        decimal Suppliers(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a));
        decimal OtherPay(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => (Cls(a) == 4 && n < 0 && !IsSupplier(a)) || (Cls(a) == 5 && n < 0));
        decimal Capital(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => a.StartsWith("10"));
        decimal Reserves(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 1 && (a.StartsWith("11") || a.StartsWith("14")));
        decimal Retained(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => a.StartsWith("12"));
        decimal Cash(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 5 && n > 0);
        decimal Overdraft(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 5 && n < 0);
        decimal Products(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 7);
        decimal Charges(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 6);

        (decimal, decimal) Pair(Func<IReadOnlyDictionary<string, decimal>, decimal> f) => (f(cur), f(prev));

        return new List<NctNoteDto>
        {
            new()
            {
                Title = "Note 0 — Méthodes comptables",
                Description = "États financiers établis conformément au système comptable des entreprises (NCT). " +
                              "Immobilisations à leur coût, amorties selon le mode linéaire. Créances et dettes à leur valeur nominale. " +
                              "Montants exprimés en dinars tunisiens (TND).",
                Lines = Array.Empty<NctLineDto>()
            },
            new()
            {
                Title = "Note 1 — Immobilisations",
                Lines = new List<NctLineDto>
                {
                    Line("N1G", "Valeur brute", Pair(Gross)),
                    Line("N1A", "Amortissements et dépréciations", Pair(Amort)),
                    Line("N1N", "Valeur nette comptable", (Gross(cur) - Amort(cur), Gross(prev) - Amort(prev)), subtotal: true)
                }
            },
            new()
            {
                Title = "Note 2 — Créances et dettes",
                Lines = new List<NctLineDto>
                {
                    Line("N2C", "Clients et comptes rattachés", Pair(Clients)),
                    Line("N2AR", "Autres créances", Pair(OtherRecv)),
                    Line("N2F", "Fournisseurs et comptes rattachés", Pair(Suppliers)),
                    Line("N2AP", "Autres dettes", Pair(OtherPay))
                }
            },
            new()
            {
                Title = "Note 3 — Capitaux propres",
                Lines = new List<NctLineDto>
                {
                    Line("N3CAP", "Capital", Pair(Capital)),
                    Line("N3RES", "Réserves", Pair(Reserves)),
                    Line("N3REP", "Résultats reportés", Pair(Retained)),
                    Line("N3RES2", "Résultat de l'exercice", (Result(cur), Result(prev)), subtotal: true)
                }
            },
            new()
            {
                Title = "Note 4 — Trésorerie",
                Lines = new List<NctLineDto>
                {
                    Line("N4L", "Liquidités et équivalents", Pair(Cash)),
                    Line("N4O", "Concours bancaires courants", Pair(Overdraft))
                }
            },
            new()
            {
                Title = "Note 5 — Produits et charges",
                Lines = new List<NctLineDto>
                {
                    Line("N5P", "Total des produits", Pair(Products)),
                    Line("N5C", "Total des charges", Pair(Charges)),
                    Line("N5R", "Résultat net", (Result(cur), Result(prev)), subtotal: true)
                }
            }
        };
    }

    // ── Utilitaires ────────────────────────────────────────────────────────────

    /// <summary>Résultat = produits (classe 7, crédit − débit) − charges (classe 6, débit − crédit) = −Σnet(6,7).</summary>
    private static decimal Result(IReadOnlyDictionary<string, decimal> net) =>
        -SumWhere(net, (a, n) => Cls(a) == 6 || Cls(a) == 7);

    private static decimal SumWhere(IReadOnlyDictionary<string, decimal> net, Func<string, decimal, bool> predicate)
    {
        decimal sum = 0;
        foreach (var kv in net)
            if (predicate(kv.Key, kv.Value))
                sum += kv.Value;
        return sum;
    }

    private static int Cls(string account) =>
        !string.IsNullOrEmpty(account) && char.IsDigit(account[0]) ? account[0] - '0' : 0;

    /// <summary>Sous-classe NCT à deux chiffres (« 21 » vs « 221 » : StartsWith("21") est faux).</summary>
    private static string Root2(string account) =>
        account.Length >= 2 ? account[..2] : account;

    /// <summary>Racine à trois chiffres, nécessaire pour distinguer p. ex. « 281 » de « 282 » (T1).</summary>
    private static string Root3(string account) =>
        account.Length >= 3 ? account[..3] : account;

    private static bool IsClient(string a) =>
        a.StartsWith("411") || a.StartsWith("413") || a.StartsWith("416") || a.StartsWith("417") || a.StartsWith("418");

    private static bool IsSupplier(string a) =>
        a.StartsWith("401") || a.StartsWith("403") || a.StartsWith("404") || a.StartsWith("405") || a.StartsWith("408");

    private static NctLineDto Line(string code, string label, (decimal Cur, decimal Prev) v, bool subtotal = false, int level = 0) =>
        new() { Code = code, Label = label, Amount = v.Cur, PreviousAmount = v.Prev, IsSubtotal = subtotal, Level = level };
}
