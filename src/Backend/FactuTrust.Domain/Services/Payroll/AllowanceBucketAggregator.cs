using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Ligne de prime/indemnité (contrat récurrent ou variable mensuelle) pour agrégation et affichage bulletin.
/// </summary>
public sealed record AllowanceLineInput(string Label, decimal Amount, bool Taxable, bool SubjectToCnss, EarningKind? Kind = null);

/// <summary>
/// Résultat de l'agrégation des primes en buckets de calcul (matrice 4 quadrants ou modèle simplifié).
/// </summary>
public sealed record AllowanceBucketResult(
    decimal TaxableCnssable,
    decimal TaxableOnly,
    decimal CnssOnly,
    decimal NonTaxable,
    IReadOnlyList<AllowanceLineInput> Lines);

/// <summary>
/// Agrège les primes/indemnités selon la matrice imposable × CNSS configurée pour l'exercice.
/// </summary>
public static class AllowanceBucketAggregator
{
    public static AllowanceBucketResult Aggregate(IEnumerable<AllowanceLineInput> lines, bool enableQuadrantMatrix)
    {
        var lineList = lines.ToList();
        decimal taxableCnssable = 0m;
        decimal taxableOnly = 0m;
        decimal cnssOnly = 0m;
        decimal nonTaxable = 0m;

        if (enableQuadrantMatrix)
        {
            foreach (var allowance in lineList)
            {
                switch (allowance.Taxable, allowance.SubjectToCnss)
                {
                    case (true, true):
                        taxableCnssable += allowance.Amount;
                        break;
                    case (true, false):
                        taxableOnly += allowance.Amount;
                        break;
                    case (false, true):
                        cnssOnly += allowance.Amount;
                        break;
                    default:
                        nonTaxable += allowance.Amount;
                        break;
                }
            }
        }
        else
        {
            // R-25 : mode legacy (matrice quadrant désactivée). Historiquement tout ce qui n'était
            // pas (imposable + CNSS) tombait en nonTaxable, ce qui sous-taxait silencieusement les
            // indemnités imposables mais non soumises à la CNSS (Taxable=true, Cnss=false). On
            // corrige ce cas en l'orientant vers taxableOnly ; les indemnités non imposables
            // (Taxable=false, Cnss=true ou false) restent en nonTaxable — seul le cas imposable
            // est corrigé pour ne pas changer la base CNSS des exercices déjà paramétrés en legacy.
            foreach (var allowance in lineList)
            {
                if (allowance.Taxable && allowance.SubjectToCnss)
                    taxableCnssable += allowance.Amount;
                else if (allowance.Taxable)
                    taxableOnly += allowance.Amount;
                else
                    nonTaxable += allowance.Amount;
            }
        }

        return new AllowanceBucketResult(
            taxableCnssable,
            taxableOnly,
            cnssOnly,
            nonTaxable,
            lineList);
    }
}
