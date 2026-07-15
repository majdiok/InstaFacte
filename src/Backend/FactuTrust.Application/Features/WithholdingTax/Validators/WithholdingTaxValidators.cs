using FactuTrust.Application.Features.WithholdingTax.Commands;
using FluentValidation;

namespace FactuTrust.Application.Features.WithholdingTax.Validators;

public class ExportTejXmlCommandValidator : AbstractValidator<ExportTejXmlCommand>
{
    public ExportTejXmlCommandValidator()
    {
        RuleFor(x => x.Request.Year)
            .InclusiveBetween(2020, 2100).WithMessage("L'année doit être valide");

        RuleFor(x => x.Request.Month)
            .InclusiveBetween(1, 12).WithMessage("Le mois doit être compris entre 1 et 12");
    }
}
