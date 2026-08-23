using FactuTrust.Application.Features.SupplierInvoices.Commands;
using FluentValidation;

namespace FactuTrust.Application.Features.SupplierInvoices.Validators;

public sealed class CreateStandaloneSupplierInvoiceCommandValidator
    : AbstractValidator<CreateStandaloneSupplierInvoiceCommand>
{
    public CreateStandaloneSupplierInvoiceCommandValidator()
    {
        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données de la facture fournisseur sont obligatoires.");

        RuleFor(x => x.Dto.SupplierId)
            .NotEmpty()
            .WithMessage("Le fournisseur est obligatoire.");

        RuleFor(x => x.Dto.InvoiceDate)
            .NotEmpty()
            .WithMessage("La date de facture est obligatoire.");

        RuleFor(x => x.Dto.InvoiceNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.Dto.InvoiceNumber))
            .WithMessage("Le numéro de facture ne peut pas dépasser 50 caractères.");

        RuleFor(x => x.Dto.ExternalReference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Dto.ExternalReference))
            .WithMessage("La référence fournisseur ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Dto.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Sélectionnez au moins une ligne à facturer");

        RuleForEach(x => x.Dto.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty()
                .WithMessage("Le produit est obligatoire");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("La quantité doit être positive");

            line.RuleFor(l => l.UnitPriceHt)
                .GreaterThanOrEqualTo(0)
                .When(l => l.UnitPriceHt.HasValue)
                .WithMessage("Le prix unitaire HT ne peut pas être négatif");

            line.RuleFor(l => l.DiscountPercent)
                .InclusiveBetween(0, 100)
                .When(l => l.DiscountPercent.HasValue)
                .WithMessage("La remise doit être comprise entre 0 et 100 %");
        });
    }
}
