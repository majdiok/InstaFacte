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
        bool enabled)
    {
        var balanceSheet = BuildBalanceSheet(currentNet, previousNet);
        var income = BuildIncomeStatement(currentNet, previousNet);
        var cashFlow = BuildCashFlow(currentNet, previousNet, income.NetResult);
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
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev)
    {
        decimal CurA(Func<string, decimal, bool> p) => SumWhere(cur, p);
        decimal PrevA(Func<string, decimal, bool> p) => SumWhere(prev, p);

        // Actif — valeurs présentées en net (débit − crédit).
        var incorp = (CurA((a, n) => Cls(a) == 2 && a.StartsWith("21")), PrevA((a, n) => Cls(a) == 2 && a.StartsWith("21")));
        var corp = (CurA((a, n) => Cls(a) == 2 && (a.StartsWith("22") || a.StartsWith("23") || a.StartsWith("24"))),
                    PrevA((a, n) => Cls(a) == 2 && (a.StartsWith("22") || a.StartsWith("23") || a.StartsWith("24"))));
        var fin = (CurA((a, n) => Cls(a) == 2 && !a.StartsWith("21") && !a.StartsWith("22") && !a.StartsWith("23") && !a.StartsWith("24")),
                   PrevA((a, n) => Cls(a) == 2 && !a.StartsWith("21") && !a.StartsWith("22") && !a.StartsWith("23") && !a.StartsWith("24")));
        var ancTotal = (incorp.Item1 + corp.Item1 + fin.Item1, incorp.Item2 + corp.Item2 + fin.Item2);

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
        var reserves = (CurL((a, n) => Cls(a) == 1 && (a.StartsWith("11") || a.StartsWith("14"))),
                        PrevL((a, n) => Cls(a) == 1 && (a.StartsWith("11") || a.StartsWith("14"))));
        var retained = (CurL((a, n) => a.StartsWith("12")), PrevL((a, n) => a.StartsWith("12")));
        var resultN = Result(cur);
        var resultPrev = Result(prev);
        var equityTotal = (capital.Item1 + reserves.Item1 + retained.Item1 + resultN,
                           capital.Item2 + reserves.Item2 + retained.Item2 + resultPrev);

        // Passifs non courants : provisions (15), emprunts (16,17) et autres classe 1 hors capitaux propres.
        var nonCurrentLiab = (CurL((a, n) => Cls(a) == 1 && !a.StartsWith("10") && !a.StartsWith("11") && !a.StartsWith("12") && !a.StartsWith("14")),
                              PrevL((a, n) => Cls(a) == 1 && !a.StartsWith("10") && !a.StartsWith("11") && !a.StartsWith("12") && !a.StartsWith("14")));

        var suppliers = (CurL((a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a)), PrevL((a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a)));
        var otherCurLiab = (CurL((a, n) => (Cls(a) == 4 && n < 0 && !IsSupplier(a)) || (Cls(a) == 5 && n < 0)),
                            PrevL((a, n) => (Cls(a) == 4 && n < 0 && !IsSupplier(a)) || (Cls(a) == 5 && n < 0)));
        var currentLiab = (suppliers.Item1 + otherCurLiab.Item1, suppliers.Item2 + otherCurLiab.Item2);

        var passif = new List<NctLineDto>
        {
            Line("CP", "CAPITAUX PROPRES", equityTotal, subtotal: true, level: 0),
            Line("CP1", "Capital", capital, level: 1),
            Line("CP2", "Réserves", reserves, level: 1),
            Line("CP3", "Résultats reportés", retained, level: 1),
            Line("CP4", "Résultat de l'exercice", (resultN, resultPrev), level: 1),
            Line("PNC", "PASSIFS NON COURANTS", nonCurrentLiab, subtotal: true, level: 0),
            Line("PC", "PASSIFS COURANTS", currentLiab, subtotal: true, level: 0),
            Line("PC1", "Fournisseurs et comptes rattachés", suppliers, level: 1),
            Line("PC2", "Autres passifs courants", otherCurLiab, level: 1)
        };
        var totalEq = (equityTotal.Item1 + nonCurrentLiab.Item1 + currentLiab.Item1,
                       equityTotal.Item2 + nonCurrentLiab.Item2 + currentLiab.Item2);
        passif.Add(Line("TP", "TOTAL CAPITAUX PROPRES ET PASSIFS", totalEq, subtotal: true, level: 0));

        return new NctBalanceSheetDto
        {
            Assets = assets,
            EquityAndLiabilities = passif,
            TotalAssets = totalAssets.Item1,
            TotalEquityAndLiabilities = totalEq.Item1,
            PreviousTotalAssets = totalAssets.Item2,
            PreviousTotalEquityAndLiabilities = totalEq.Item2,
            IsBalanced = Math.Abs(totalAssets.Item1 - totalEq.Item1) < Tolerance
        };
    }

    // ── Compte de résultat ────────────────────────────────────────────────────

    private static NctIncomeStatementDto BuildIncomeStatement(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev)
    {
        // Produits présentés en crédit − débit (= −net) ; charges en débit − crédit (= net).
        decimal Prod(IReadOnlyDictionary<string, decimal> d, Func<string, bool> p) => -SumWhere(d, (a, n) => Cls(a) == 7 && p(a));
        decimal Charge(IReadOnlyDictionary<string, decimal> d, Func<string, bool> p) => SumWhere(d, (a, n) => Cls(a) == 6 && p(a));

        bool FinancialProd(string a) => a.StartsWith("75") || a.StartsWith("76");
        bool FinancialCharge(string a) => a.StartsWith("65") || a.StartsWith("66");
        bool Tax(string a) => a.StartsWith("69");

        var prodExpl = Prod(cur, a => !FinancialProd(a));
        var prodFin = Prod(cur, FinancialProd);
        var chargeExpl = Charge(cur, a => !FinancialCharge(a) && !Tax(a));
        var chargeFin = Charge(cur, FinancialCharge);
        var tax = Charge(cur, Tax);

        var operating = prodExpl - chargeExpl;
        var financial = prodFin - chargeFin;
        var beforeTax = operating + financial;
        var net = beforeTax - tax;

        var prevNet = Result(prev);

        var lines = new List<NctLineDto>
        {
            Line("PEX", "Produits d'exploitation", (prodExpl, -SumWhere(prev, (a, n) => Cls(a) == 7 && !FinancialProd(a))), level: 0),
            Line("CEX", "Charges d'exploitation", (chargeExpl, SumWhere(prev, (a, n) => Cls(a) == 6 && !FinancialCharge(a) && !Tax(a))), level: 0),
            Line("REX", "Résultat d'exploitation", (operating, 0m), subtotal: true, level: 0),
            Line("PFI", "Produits financiers", (prodFin, -SumWhere(prev, (a, n) => Cls(a) == 7 && FinancialProd(a))), level: 0),
            Line("CFI", "Charges financières", (chargeFin, SumWhere(prev, (a, n) => Cls(a) == 6 && FinancialCharge(a))), level: 0),
            Line("RAI", "Résultat avant impôt", (beforeTax, 0m), subtotal: true, level: 0),
            Line("IMP", "Impôt sur les bénéfices", (tax, SumWhere(prev, (a, n) => Cls(a) == 6 && Tax(a))), level: 0),
            Line("RN", "RÉSULTAT NET", (net, prevNet), subtotal: true, level: 0)
        };

        return new NctIncomeStatementDto
        {
            Lines = lines,
            OperatingResult = operating,
            FinancialResult = financial,
            ResultBeforeTax = beforeTax,
            NetResult = net,
            PreviousNetResult = prevNet
        };
    }

    // ── Tableau de flux de trésorerie (méthode indirecte) ──────────────────────

    private static NctCashFlowDto BuildCashFlow(
        IReadOnlyDictionary<string, decimal> cur, IReadOnlyDictionary<string, decimal> prev, decimal netResult)
    {
        decimal Net(IReadOnlyDictionary<string, decimal> d, Func<string, bool> p) => SumWhere(d, (a, n) => p(a));

        var dotations = Net(cur, a => a.StartsWith("68")) - (-Net(cur, a => a.StartsWith("78")));

        decimal Stocks(IReadOnlyDictionary<string, decimal> d) => Net(d, a => Cls(a) == 3);
        decimal Clients(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 4 && n > 0 && IsClient(a));
        decimal Suppliers(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => Cls(a) == 4 && n < 0 && IsSupplier(a));

        var dStocks = Stocks(cur) - Stocks(prev);
        var dClients = Clients(cur) - Clients(prev);
        var dSuppliers = Suppliers(cur) - Suppliers(prev);
        var operating = netResult + dotations - dStocks - dClients + dSuppliers;

        decimal GrossImmo(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 2 && !a.StartsWith("28") && !a.StartsWith("29"));
        var investing = -(GrossImmo(cur) - GrossImmo(prev));

        decimal Financing(IReadOnlyDictionary<string, decimal> d) => -SumWhere(d, (a, n) => a.StartsWith("10") || a.StartsWith("16") || a.StartsWith("17"));
        var financing = Financing(cur) - Financing(prev);

        var computed = operating + investing + financing;

        decimal Cash(IReadOnlyDictionary<string, decimal> d) => SumWhere(d, (a, n) => Cls(a) == 5);
        var openingCash = Cash(prev);
        var closingCash = Cash(cur);
        var actualChange = closingCash - openingCash;
        var gap = actualChange - computed;

        var lines = new List<NctLineDto>
        {
            Line("CF0", "Résultat net de l'exercice", (netResult, 0m), level: 1),
            Line("CF1", "Dotations aux amortissements et provisions", (dotations, 0m), level: 1),
            Line("CF2", "Variation des stocks", (-dStocks, 0m), level: 1),
            Line("CF3", "Variation des créances clients", (-dClients, 0m), level: 1),
            Line("CF4", "Variation des dettes fournisseurs", (dSuppliers, 0m), level: 1),
            Line("CFOP", "Flux de trésorerie liés à l'exploitation", (operating, 0m), subtotal: true, level: 0),
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
        decimal EquityExResult(IReadOnlyDictionary<string, decimal> d) =>
            -SumWhere(d, (a, n) => Cls(a) == 1 && (a.StartsWith("10") || a.StartsWith("11") || a.StartsWith("12") || a.StartsWith("14")));

        var opening = EquityExResult(prev) + Result(prev);
        var closing = EquityExResult(cur) + netResult;
        var otherMovements = closing - opening - netResult;

        var lines = new List<NctLineDto>
        {
            Line("EQ0", "Capitaux propres à l'ouverture", (opening, 0m), subtotal: true, level: 0),
            Line("EQ1", "Résultat de l'exercice", (netResult, 0m), level: 1),
            Line("EQ2", "Autres variations (affectations, distributions, capital)", (otherMovements, 0m), level: 1),
            Line("EQ3", "Capitaux propres à la clôture", (closing, 0m), subtotal: true, level: 0)
        };

        return new NctEquityChangeDto
        {
            Lines = lines,
            OpeningEquity = opening,
            NetResult = netResult,
            ClosingEquity = closing
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

    private static bool IsClient(string a) =>
        a.StartsWith("411") || a.StartsWith("413") || a.StartsWith("416") || a.StartsWith("417") || a.StartsWith("418");

    private static bool IsSupplier(string a) =>
        a.StartsWith("401") || a.StartsWith("403") || a.StartsWith("404") || a.StartsWith("405") || a.StartsWith("408");

    private static NctLineDto Line(string code, string label, (decimal Cur, decimal Prev) v, bool subtotal = false, int level = 0) =>
        new() { Code = code, Label = label, Amount = v.Cur, PreviousAmount = v.Prev, IsSubtotal = subtotal, Level = level };
}
