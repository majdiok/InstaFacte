using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

public class WithholdingTaxCalculationService : IWithholdingTaxService
{
    /// <summary>Seuil TTC (TND) pour l’application de la retenue RS7 (achats).</summary>
    public const decimal Rs7TtcThresholdTnd = 1000m;
    private const decimal VAT_WITHHOLDING_RATE_STANDARD = 0.25m;
    private const decimal VAT_WITHHOLDING_RATE_FULL = 1.0m;

    public WithholdingCalculationResultDto CalculateWithholding(WithholdingCalculationRequest request)
    {
        var statutoryRate = DetermineRate(request.OperationCode, request.IsResident, request.HasCNPC);
        var rate = request.WithholdingRateOverride ?? statutoryRate;
        if (rate < 0 || rate > 100)
            rate = statutoryRate;

        var vatRate = request.VatRate ?? 0m;
        var vatAmount = Math.Round(request.AmountHT * vatRate / 100m, 3);
        var amountTTC = request.AmountHT + vatAmount;

        var rs7Threshold = request.Rs7TtcThresholdTnd ?? Rs7TtcThresholdTnd;
        if (IsRS7Code(request.OperationCode) && amountTTC < rs7Threshold)
        {
            return new WithholdingCalculationResultDto(
                request.AmountHT, vatAmount, amountTTC, 0m, 0m, amountTTC, null);
        }

        decimal withholdingBase = request.AmountHT;
        decimal withholdingAmount;

        if (request.HasPriseEnCharge)
        {
            var effectiveRate = rate / (100m - rate) * 100m;
            withholdingAmount = Math.Round(withholdingBase * effectiveRate / 100m, 3);
        }
        else
        {
            withholdingAmount = Math.Round(withholdingBase * rate / 100m, 3);
        }

        var netAmountPaid = amountTTC - withholdingAmount;

        decimal? vatWithholdingAmount = null;
        if (vatAmount > 0 && ShouldApplyVatWithholding(request.OperationCode))
        {
            var vatWhRate = GetVatWithholdingRate(request.OperationCode, request.IsResident);
            vatWithholdingAmount = Math.Round(vatAmount * vatWhRate, 3);
            netAmountPaid -= vatWithholdingAmount.Value;
        }

        return new WithholdingCalculationResultDto(
            request.AmountHT,
            vatAmount,
            amountTTC,
            rate,
            withholdingAmount,
            netAmountPaid,
            vatWithholdingAmount);
    }

    private static decimal DetermineRate(string operationCode, bool isResident, bool hasCNPC)
    {
        if (string.IsNullOrWhiteSpace(operationCode))
            return 0m;

        var code = operationCode.ToUpperInvariant();
        var prefix = code.Split('_')[0];

        var baseRate = prefix switch
        {
            "RS1" => code switch
            {
                "RS1_000005" => 5m,
                _ => 15m
            },

            "RS2" => code switch
            {
                "RS2_000001" => 3m,
                "RS2_000004" => 20m,
                _ => 10m
            },

            "RS3" => 20m,
            "RS4" => 10m,

            "RS5" => code switch
            {
                "RS5_000001" => 10m,
                _ => 15m
            },

            "RS6" => 2.5m,

            "RS7" => code switch
            {
                "RS7_000002" => 1m,
                "RS7_000003" => 0.5m,
                _ => 1.5m
            },

            "RS8" => 25m,

            "RS9" when !isResident => hasCNPC ? GetCnpcReducedRate(code) : 15m,
            "RS9" => 15m,

            "RS10" => 0m,

            "RS11" => code switch
            {
                "RS11_000001" => 1.5m,
                _ => 15m
            },

            _ => 0m
        };

        if (hasCNPC && prefix == "RS9")
            return baseRate;

        return baseRate;
    }

    /// <summary>
    /// CNPC convention rate reductions for non-residents.
    /// In practice, rates depend on the specific bilateral treaty.
    /// These are common reduced rates.
    /// </summary>
    private static decimal GetCnpcReducedRate(string operationCode) => operationCode switch
    {
        "RS9_000001" => 10m,
        "RS9_000002" => 5m,
        "RS9_000003" => 10m,
        "RS9_000004" => 10m,
        "RS9_000005" => 10m,
        _ => 10m
    };

    private static bool IsRS7Code(string operationCode)
    {
        if (string.IsNullOrWhiteSpace(operationCode)) return false;
        return operationCode.Split('_')[0].Equals("RS7", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldApplyVatWithholding(string operationCode)
    {
        if (string.IsNullOrWhiteSpace(operationCode))
            return false;

        var prefix = operationCode.Split('_')[0].ToUpperInvariant();
        return prefix is "RS1" or "RS2" or "RS9";
    }

    /// <summary>
    /// Art. 19bis: 25% standard VAT withholding for residents, 100% for non-residents.
    /// </summary>
    private static decimal GetVatWithholdingRate(string operationCode, bool isResident)
    {
        if (string.IsNullOrWhiteSpace(operationCode)) return VAT_WITHHOLDING_RATE_STANDARD;

        var prefix = operationCode.Split('_')[0].ToUpperInvariant();
        if (prefix == "RS9")
            return VAT_WITHHOLDING_RATE_FULL;

        return isResident ? VAT_WITHHOLDING_RATE_STANDARD : VAT_WITHHOLDING_RATE_FULL;
    }
}
