using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.PurchaseReceipts.Commands;
using FluentValidation;

namespace FactuTrust.Application.Features.PurchaseReceipts.Validators;

/// <summary>
/// Validator for CreatePurchaseReceiptCommand.
/// </summary>
public sealed class CreatePurchaseReceiptCommandValidator : AbstractValidator<CreatePurchaseReceiptCommand>
{
    public CreatePurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données du bon de réception sont obligatoires.");

        RuleFor(x => x.Dto.SupplierId)
            .NotEmpty()
            .WithMessage("Le fournisseur est obligatoire.");

        RuleFor(x => x.Dto.WarehouseId)
            .NotEmpty()
            .WithMessage("L'entrepôt est obligatoire.");

        RuleFor(x => x.Dto.ReceiptDate)
            .NotEmpty()
            .WithMessage("La date de réception est obligatoire.");

        RuleFor(x => x.Dto.SupplierReference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.SupplierReference))
            .WithMessage("La référence fournisseur ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.TransporterName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Dto.TransporterName))
            .WithMessage("Le nom du transporteur ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Dto.DeliveryNoteNumber)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.DeliveryNoteNumber))
            .WithMessage("Le N° BL ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Dto.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Au moins une ligne de réception est requise.");

        RuleForEach(x => x.Dto.Lines)
            .SetValidator(new CreatePurchaseReceiptLineDtoValidator());
    }
}

/// <summary>
/// Validator for individual line items in purchase receipt creation / update.
/// </summary>
public sealed class CreatePurchaseReceiptLineDtoValidator : AbstractValidator<CreatePurchaseReceiptLineDto>
{
    public CreatePurchaseReceiptLineDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Le produit est obligatoire.");

        RuleFor(x => x.ReceivedQuantity)
            .GreaterThan(0)
            .WithMessage("La quantité reçue doit être supérieure à zéro.");

        RuleFor(x => x.OrderedQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La quantité commandée ne peut pas être négative.");

        RuleFor(x => x.UnitPriceHT)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitPriceHT.HasValue)
            .WithMessage("Le prix unitaire HT ne peut pas être négatif.");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100)
            .When(x => x.DiscountPercent.HasValue)
            .WithMessage("La remise doit être comprise entre 0 et 100 %.");
    }
}

/// <summary>
/// Validator for UpdatePurchaseReceiptCommand.
/// </summary>
public sealed class UpdatePurchaseReceiptCommandValidator : AbstractValidator<UpdatePurchaseReceiptCommand>
{
    public UpdatePurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de réception est obligatoire.");

        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données de mise à jour sont obligatoires.");

        RuleFor(x => x.Dto.WarehouseId)
            .NotEmpty()
            .WithMessage("L'entrepôt est obligatoire.");

        RuleFor(x => x.Dto.ReceiptDate)
            .NotEmpty()
            .WithMessage("La date de réception est obligatoire.");

        RuleFor(x => x.Dto.SupplierReference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.SupplierReference))
            .WithMessage("La référence fournisseur ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.TransporterName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Dto.TransporterName))
            .WithMessage("Le nom du transporteur ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Dto.DeliveryNoteNumber)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.DeliveryNoteNumber))
            .WithMessage("Le N° BL ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Dto.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Au moins une ligne de réception est requise.");

        RuleForEach(x => x.Dto.Lines)
            .SetValidator(new CreatePurchaseReceiptLineDtoValidator());
    }
}

/// <summary>
/// Validator for ValidatePurchaseReceiptCommand.
/// </summary>
public sealed class ValidatePurchaseReceiptCommandValidator : AbstractValidator<ValidatePurchaseReceiptCommand>
{
    public ValidatePurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de réception est obligatoire.");
    }
}

/// <summary>
/// Validator for CancelPurchaseReceiptCommand.
/// </summary>
public sealed class CancelPurchaseReceiptCommandValidator : AbstractValidator<CancelPurchaseReceiptCommand>
{
    public CancelPurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de réception est obligatoire.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Le motif d'annulation est obligatoire.")
            .MaximumLength(1000)
            .WithMessage("Le motif d'annulation ne peut pas dépasser 1000 caractères.");
    }
}

/// <summary>
/// Validator for DeletePurchaseReceiptCommand.
/// </summary>
public sealed class DeletePurchaseReceiptCommandValidator : AbstractValidator<DeletePurchaseReceiptCommand>
{
    public DeletePurchaseReceiptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de réception est obligatoire.");
    }
}
