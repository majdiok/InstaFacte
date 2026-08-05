namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Ligne de prime/indemnité (contrat récurrent ou variable mensuelle) pour agrégation et affichage bulletin.
/// </summary>
public sealed record AllowanceLineInput(string Label, decimal Amount, bool Taxable, bool SubjectToCnss);

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
            foreach (var allowance in lineList)
            {
                if (allowance.Taxable && allowance.SubjectToCnss)
                    taxableCnssable += allowance.Amount;
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
