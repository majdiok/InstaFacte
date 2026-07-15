using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FluentValidation;

namespace FactuTrust.Application.Features.InvoiceWizard.Validators;

/// <summary>
/// Validates invoice calculations with millime precision (Tunisian currency).
/// Ensures all amounts are correctly calculated server-side.
/// </summary>
public sealed class CalculationValidator : AbstractValidator<CalculationValidationRequest>
{
    public CalculationValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Au moins une ligne est requise pour la validation des calculs")
            .WithErrorCode(ValidationErrorCodes.Required);

        RuleFor(x => x)
            .Must(VerifyLineCalculations)
            .WithMessage("Les calculs de ligne sont incorrects")
            .WithErrorCode(ValidationErrorCodes.CalculationMismatch);

        RuleFor(x => x)
            .Must(VerifyTotalCalculations)
            .WithMessage("Les totaux calculés ne correspondent pas aux valeurs soumises")
            .WithErrorCode(ValidationErrorCodes.CalculationMismatch);

        RuleFor(x => x)
            .Must(VerifyVatBreakdown)
            .WithMessage("La ventilation TVA est incorrecte")
            .WithErrorCode(ValidationErrorCodes.CalculationMismatch);
    }

    private static bool VerifyLineCalculations(CalculationValidationRequest request)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return true;

        var fodecRate = request.FodecRatePercent ?? 1.0m;

        foreach (var line in request.Lines)
        {
            var expected = TunisianInvoiceLineCalculation.CalculateLine(
                line.Quantity,
                line.UnitPriceHT,
                line.DiscountType,
                line.DiscountValue,
                line.VatRate,
                line.FodecApplicable,
                fodecRate);

            if (line.CalculatedTotalHT.HasValue &&
                !TunisianValidationRules.AmountsEqual(expected.TotalHT, line.CalculatedTotalHT.Value))
            {
                return false;
            }

            if (line.CalculatedFodecAmount.HasValue &&
                !TunisianValidationRules.AmountsEqual(expected.FodecAmount, line.CalculatedFodecAmount.Value))
            {
                return false;
            }

            if (line.CalculatedVatAmount.HasValue &&
                !TunisianValidationRules.AmountsEqual(expected.VatAmount, line.CalculatedVatAmount.Value))
            {
                return false;
            }

            if (line.CalculatedTotalTTC.HasValue &&
                !TunisianValidationRules.AmountsEqual(expected.TotalTTC, line.CalculatedTotalTTC.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyTotalCalculations(CalculationValidationRequest request)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return true;

        if (request.SubmittedTotals == null)
            return true;

        var fodecRate = request.FodecRatePercent ?? 1.0m;
        decimal serverTotalHT = 0;
        decimal serverTotalFodec = 0;
        decimal serverTotalVat = 0;
        decimal serverTotalDiscount = 0;

        foreach (var line in request.Lines)
        {
            var subtotal = line.Quantity * line.UnitPriceHT;
            var discount = TunisianInvoiceLineCalculation.CalculateDiscount(subtotal, line.DiscountType, line.DiscountValue);
            var amounts = TunisianInvoiceLineCalculation.CalculateLine(
                line.Quantity,
                line.UnitPriceHT,
                line.DiscountType,
                line.DiscountValue,
                line.VatRate,
                line.FodecApplicable,
                fodecRate);

            serverTotalHT += amounts.TotalHT;
            serverTotalFodec += amounts.FodecAmount;
            serverTotalVat += amounts.VatAmount;
            serverTotalDiscount += discount;
        }

        serverTotalHT = TunisianValidationRules.RoundToMillimes(serverTotalHT);
        serverTotalFodec = TunisianValidationRules.RoundToMillimes(serverTotalFodec);
        serverTotalVat = TunisianValidationRules.RoundToMillimes(serverTotalVat);
        serverTotalDiscount = TunisianValidationRules.RoundToMillimes(serverTotalDiscount);
        var fiscalStamp = request.SubmittedTotals.FiscalStampAmount ?? 0;
        var serverTotalTTC = TunisianValidationRules.RoundToMillimes(
            serverTotalHT + serverTotalFodec + serverTotalVat + fiscalStamp);

        if (request.SubmittedTotals.TotalHT.HasValue &&
            !TunisianValidationRules.AmountsEqual(serverTotalHT, request.SubmittedTotals.TotalHT.Value))
        {
            return false;
        }

        if (request.SubmittedTotals.TotalFodec.HasValue &&
            !TunisianValidationRules.AmountsEqual(serverTotalFodec, request.SubmittedTotals.TotalFodec.Value))
        {
            return false;
        }

        if (request.SubmittedTotals.TotalVat.HasValue &&
            !TunisianValidationRules.AmountsEqual(serverTotalVat, request.SubmittedTotals.TotalVat.Value))
        {
            return false;
        }

        if (request.SubmittedTotals.TotalTTC.HasValue &&
            !TunisianValidationRules.AmountsEqual(serverTotalTTC, request.SubmittedTotals.TotalTTC.Value))
        {
            return false;
        }

        if (request.SubmittedTotals.TotalDiscount.HasValue &&
            !TunisianValidationRules.AmountsEqual(serverTotalDiscount, request.SubmittedTotals.TotalDiscount.Value))
        {
            return false;
        }

        return true;
    }

    private static bool VerifyVatBreakdown(CalculationValidationRequest request)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return true;

        if (request.SubmittedTotals?.VatBreakdown == null || request.SubmittedTotals.VatBreakdown.Count == 0)
            return true;

        var fodecRate = request.FodecRatePercent ?? 1.0m;
        var serverBreakdown = new Dictionary<int, (decimal BaseAmount, decimal VatAmount)>();

        foreach (var line in request.Lines)
        {
            var amounts = TunisianInvoiceLineCalculation.CalculateLine(
                line.Quantity,
                line.UnitPriceHT,
                line.DiscountType,
                line.DiscountValue,
                line.VatRate,
                line.FodecApplicable,
                fodecRate);

            if (!serverBreakdown.ContainsKey(line.VatRate))
                serverBreakdown[line.VatRate] = (0, 0);

            var current = serverBreakdown[line.VatRate];
            serverBreakdown[line.VatRate] = (
                current.BaseAmount + amounts.TotalHT + amounts.FodecAmount,
                current.VatAmount + amounts.VatAmount);
        }

        foreach (var submitted in request.SubmittedTotals.VatBreakdown)
        {
            if (!serverBreakdown.TryGetValue(submitted.Rate, out var serverValues))
                return false;

            var serverBase = TunisianValidationRules.RoundToMillimes(serverValues.BaseAmount);
            var serverVat = TunisianValidationRules.RoundToMillimes(serverValues.VatAmount);

            if (!TunisianValidationRules.AmountsEqual(serverBase, submitted.BaseAmount) ||
                !TunisianValidationRules.AmountsEqual(serverVat, submitted.VatAmount))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record CalculationValidationRequest
{
    public List<CalculationLineDto> Lines { get; init; } = new();
    public CalculationTotalsDto? SubmittedTotals { get; init; }
    public decimal? FodecRatePercent { get; init; }
}

public sealed record CalculationLineDto
{
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public int VatRate { get; init; }
    public bool FodecApplicable { get; init; }
    public string? DiscountType { get; init; }
    public decimal? DiscountValue { get; init; }
    public decimal? CalculatedTotalHT { get; init; }
    public decimal? CalculatedFodecAmount { get; init; }
    public decimal? CalculatedVatAmount { get; init; }
    public decimal? CalculatedTotalTTC { get; init; }
}

public sealed record CalculationTotalsDto
{
    public decimal? TotalHT { get; init; }
    public decimal? TotalFodec { get; init; }
    public decimal? TotalVat { get; init; }
    public decimal? TotalTTC { get; init; }
    public decimal? TotalDiscount { get; init; }
    public decimal? FiscalStampAmount { get; init; }
    public List<VatBreakdownValidationDto>? VatBreakdown { get; init; }
}

public sealed record VatBreakdownValidationDto
{
    public int Rate { get; init; }
    public decimal BaseAmount { get; init; }
    public decimal VatAmount { get; init; }
}

public static class InvoiceCalculationService
{
    public static WizardTotalsDto CalculateTotals(
        IEnumerable<WizardStepLineDto> lines,
        string currency = "TND",
        decimal fiscalStampSigned = 0,
        decimal fodecRatePercent = 1.0m)
    {
        var linesList = lines.ToList();
        if (linesList.Count == 0)
        {
            return new WizardTotalsDto
            {
                SubTotalHT = 0,
                TotalDiscount = 0,
                TotalHT = 0,
                TotalFodec = 0,
                TotalVat = 0,
                FiscalStampAmount = fiscalStampSigned,
                TotalTTC = TunisianValidationRules.RoundToMillimes(fiscalStampSigned),
                Currency = currency,
                FodecRatePercent = fodecRatePercent,
                VatBreakdown = new List<WizardVatBreakdownDto>()
            };
        }

        decimal subTotalHT = 0;
        decimal totalDiscount = 0;
        decimal totalHT = 0;
        decimal totalFodec = 0;
        decimal totalVat = 0;
        var vatGroups = new Dictionary<int, (decimal Base, decimal Vat)>();

        foreach (var line in linesList)
        {
            var subtotal = line.Quantity * line.UnitPriceHT;
            var discount = TunisianInvoiceLineCalculation.CalculateDiscount(
                subtotal, line.DiscountType, line.DiscountValue);
            var amounts = TunisianInvoiceLineCalculation.CalculateLine(
                line.Quantity,
                line.UnitPriceHT,
                line.DiscountType,
                line.DiscountValue,
                line.VatRate,
                line.FodecApplicable,
                fodecRatePercent);

            subTotalHT += subtotal;
            totalDiscount += discount;
            totalHT += amounts.TotalHT;
            totalFodec += amounts.FodecAmount;
            totalVat += amounts.VatAmount;

            if (!vatGroups.ContainsKey(line.VatRate))
                vatGroups[line.VatRate] = (0, 0);

            var current = vatGroups[line.VatRate];
            vatGroups[line.VatRate] = (
                current.Base + amounts.TotalHT + amounts.FodecAmount,
                current.Vat + amounts.VatAmount);
        }

        subTotalHT = TunisianValidationRules.RoundToMillimes(subTotalHT);
        totalDiscount = TunisianValidationRules.RoundToMillimes(totalDiscount);
        totalHT = TunisianValidationRules.RoundToMillimes(totalHT);
        totalFodec = TunisianValidationRules.RoundToMillimes(totalFodec);
        totalVat = TunisianValidationRules.RoundToMillimes(totalVat);
        var totalTTC = TunisianValidationRules.RoundToMillimes(totalHT + totalFodec + totalVat + fiscalStampSigned);

        var vatBreakdown = vatGroups
            .Select(g => new WizardVatBreakdownDto
            {
                Rate = g.Key,
                RateDisplay = $"{g.Key}%",
                BaseAmount = TunisianValidationRules.RoundToMillimes(g.Value.Base),
                VatAmount = TunisianValidationRules.RoundToMillimes(g.Value.Vat)
            })
            .OrderByDescending(v => v.Rate)
            .ToList();

        return new WizardTotalsDto
        {
            SubTotalHT = subTotalHT,
            TotalDiscount = totalDiscount,
            TotalHT = totalHT,
            TotalFodec = totalFodec,
            TotalVat = totalVat,
            FiscalStampAmount = fiscalStampSigned,
            TotalTTC = totalTTC,
            Currency = currency,
            FodecRatePercent = fodecRatePercent,
            VatBreakdown = vatBreakdown
        };
    }

    public static WizardLineResponseDto CalculateLine(
        WizardStepLineDto line,
        int lineNumber,
        decimal fodecRatePercent = 1.0m)
    {
        var subtotal = line.Quantity * line.UnitPriceHT;
        var discountAmount = TunisianInvoiceLineCalculation.CalculateDiscount(
            subtotal, line.DiscountType, line.DiscountValue);
        var amounts = TunisianInvoiceLineCalculation.CalculateLine(
            line.Quantity,
            line.UnitPriceHT,
            line.DiscountType,
            line.DiscountValue,
            line.VatRate,
            line.FodecApplicable,
            fodecRatePercent);

        return new WizardLineResponseDto
        {
            LineNumber = lineNumber,
            ProductId = line.ProductId,
            Designation = line.Designation,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            UnitPriceHT = line.UnitPriceHT,
            DiscountType = line.DiscountType,
            DiscountValue = line.DiscountValue,
            DiscountAmount = discountAmount,
            VatRate = line.VatRate,
            FodecApplicable = line.FodecApplicable,
            TotalHT = amounts.TotalHT,
            FodecAmount = amounts.FodecAmount,
            VatAmount = amounts.VatAmount,
            TotalTTC = amounts.TotalTTC
        };
    }
}
