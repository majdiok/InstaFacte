using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Application.Features.PurchaseOrders.Queries;
using FluentValidation;

namespace FactuTrust.Application.Features.PurchaseOrders.Validators;

/// <summary>
/// Validator for CreatePurchaseOrderCommand.
/// Validates the DTO payload before the command handler executes.
/// </summary>
public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données de commande sont obligatoires.");

        RuleFor(x => x.Dto.SupplierId)
            .NotEmpty()
            .WithMessage("Le fournisseur est obligatoire.");

        RuleFor(x => x.Dto.OrderDate)
            .NotEmpty()
            .WithMessage("La date de commande est obligatoire.");

        RuleFor(x => x.Dto.ExpectedDeliveryDate)
            .GreaterThanOrEqualTo(x => x.Dto.OrderDate)
            .When(x => x.Dto.ExpectedDeliveryDate.HasValue)
            .WithMessage("La date de livraison prévue doit être postérieure ou égale à la date de commande.");

        RuleFor(x => x.Dto.Reference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.Reference))
            .WithMessage("La référence ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Dto.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Au moins une ligne de commande est requise.");

        RuleForEach(x => x.Dto.Lines)
            .SetValidator(new CreatePurchaseOrderLineDtoValidator());
    }
}

/// <summary>
/// Validator for individual line items in purchase order creation.
/// </summary>
public sealed class CreatePurchaseOrderLineDtoValidator : AbstractValidator<CreatePurchaseOrderLineDto>
{
    public CreatePurchaseOrderLineDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Le produit est obligatoire.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("La quantité doit être supérieure à zéro.");

        RuleFor(x => x.UnitPriceHT)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitPriceHT.HasValue)
            .WithMessage("Le prix unitaire HT ne peut pas être négatif.");
    }
}

/// <summary>
/// Validator for UpdatePurchaseOrderCommand.
/// </summary>
public sealed class UpdatePurchaseOrderCommandValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");

        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données de mise à jour sont obligatoires.");

        RuleFor(x => x.Dto.Reference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.Reference))
            .WithMessage("La référence ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Dto.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");

        When(x => x.Dto.Lines is not null, () =>
        {
            RuleFor(x => x.Dto.Lines!)
                .NotEmpty()
                .WithMessage("Au moins une ligne est requise si des lignes sont fournies.");

            RuleForEach(x => x.Dto.Lines!)
                .SetValidator(new UpdatePurchaseOrderLineDtoValidator());
        });
    }
}

/// <summary>
/// Validator for individual line items in purchase order update.
/// </summary>
public sealed class UpdatePurchaseOrderLineDtoValidator : AbstractValidator<UpdatePurchaseOrderLineDto>
{
    public UpdatePurchaseOrderLineDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Le produit est obligatoire.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("La quantité doit être supérieure à zéro.");

        RuleFor(x => x.UnitPriceHT)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitPriceHT.HasValue)
            .WithMessage("Le prix unitaire HT ne peut pas être négatif.");
    }
}

/// <summary>
/// Validator for ConfirmPurchaseOrderCommand.
/// </summary>
public sealed class ConfirmPurchaseOrderCommandValidator : AbstractValidator<ConfirmPurchaseOrderCommand>
{
    public ConfirmPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");
    }
}

/// <summary>
/// Validator for CancelPurchaseOrderCommand.
/// </summary>
public sealed class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Le motif d'annulation est obligatoire.")
            .MaximumLength(1000)
            .WithMessage("Le motif d'annulation ne peut pas dépasser 1000 caractères.");
    }
}

/// <summary>
/// Validator for DeletePurchaseOrderCommand.
/// </summary>
public sealed class DeletePurchaseOrderCommandValidator : AbstractValidator<DeletePurchaseOrderCommand>
{
    public DeletePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");
    }
}

/// <summary>
/// Validator for ReceiveGoodsCommand.
/// </summary>
public sealed class ReceiveGoodsCommandValidator : AbstractValidator<ReceiveGoodsCommand>
{
    public ReceiveGoodsCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");

        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("Les données de réception sont obligatoires.");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Au moins une ligne de réception est requise.");

        RuleForEach(x => x.Dto.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.LineId)
                    .NotEmpty()
                    .WithMessage("L'identifiant de la ligne est obligatoire.");

                line.RuleFor(l => l.ReceivedQuantity)
                    .GreaterThan(0)
                    .WithMessage("La quantité reçue doit être supérieure à zéro.");
            });
    }
}

/// <summary>
/// Validator for SendPurchaseOrderEmailCommand.
/// </summary>
public sealed class SendPurchaseOrderEmailCommandValidator : AbstractValidator<SendPurchaseOrderEmailCommand>
{
    public SendPurchaseOrderEmailCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");
    }
}

/// <summary>
/// Validator for CreateSupplierInvoiceFromPOCommand.
/// </summary>
public sealed class CreateSupplierInvoiceFromPOCommandValidator : AbstractValidator<CreateSupplierInvoiceFromPOCommand>
{
    public CreateSupplierInvoiceFromPOCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty()
            .WithMessage("L'identifiant de la commande est obligatoire.");

        RuleFor(x => x.InvoiceNumber)
            .NotEmpty()
            .WithMessage("Le numéro de facture est obligatoire.")
            .MaximumLength(50)
            .WithMessage("Le numéro de facture ne peut pas dépasser 50 caractères.");

        RuleFor(x => x.InvoiceDate)
            .NotEmpty()
            .WithMessage("La date de facture est obligatoire.");

        RuleFor(x => x.PaymentTermDays)
            .InclusiveBetween(0, 365)
            .WithMessage("Le délai de paiement doit être compris entre 0 et 365 jours.");

        RuleFor(x => x.ExternalReference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.ExternalReference))
            .WithMessage("La référence externe ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Les notes ne peuvent pas dépasser 2000 caractères.");
    }
}

/// <summary>
/// Validator for ExportPurchaseOrderPdfQuery.
/// </summary>
public sealed class ExportPurchaseOrderPdfQueryValidator : AbstractValidator<ExportPurchaseOrderPdfQuery>
{
    public ExportPurchaseOrderPdfQueryValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de commande est obligatoire pour l'export PDF.");
    }
}
