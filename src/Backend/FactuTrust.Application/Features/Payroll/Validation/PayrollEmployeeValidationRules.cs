using FactuTrust.Application.Common.Validation;
using FluentValidation;

namespace FactuTrust.Application.Features.Payroll.Validation;

/// <summary>Shared FluentValidation rules for payroll employee DTOs.</summary>
public static class PayrollEmployeeValidationRules
{
    public static IRuleBuilderOptions<T, TProperty> ValidCin<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
        where TProperty : class? =>
        rule.Must(v => TunisianValidationRules.IsValidCin(v as string))
            .WithMessage("Le CIN doit comporter 8 chiffres.");

    public static IRuleBuilderOptions<T, TProperty> ValidCnss<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
        where TProperty : class? =>
        rule.Must(v => TunisianValidationRules.IsValidCnssNumber(v as string))
            .WithMessage("Le numéro CNSS doit comporter 10 chiffres.");

    public static IRuleBuilderOptions<T, TProperty> ValidRib<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
        where TProperty : class? =>
        rule.Must(v => TunisianValidationRules.IsValidRib(v as string))
            .WithMessage("Le RIB doit comporter 20 chiffres.");

    public static IRuleBuilderOptions<T, TProperty> ValidGovernorate<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
        where TProperty : class? =>
        rule.Must(g =>
        {
            var value = g as string;
            return string.IsNullOrWhiteSpace(value) || TunisianValidationRules.IsValidGovernorate(value);
        })
            .WithMessage("Gouvernorat tunisien invalide.");
}
