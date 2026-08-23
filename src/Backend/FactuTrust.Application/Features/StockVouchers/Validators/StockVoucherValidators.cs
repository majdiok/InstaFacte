using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.StockVouchers.Commands;
using FactuTrust.Domain.Enums;
using FluentValidation;

namespace FactuTrust.Application.Features.StockVouchers.Validators;

public sealed class CreateStockVoucherCommandValidator : AbstractValidator<CreateStockVoucherCommand>
{
    public CreateStockVoucherCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Les données du bon de stock sont obligatoires.");
        RuleFor(x => x.Dto.WarehouseId).NotEmpty().WithMessage("L'entrepôt est obligatoire.");
        RuleFor(x => x.Dto.VoucherDate).NotEmpty().WithMessage("La date est obligatoire.");
        RuleFor(x => x.Dto.Kind).IsInEnum().WithMessage("Le type de bon est obligatoire.");
        RuleFor(x => x.Dto.Reason).IsInEnum().WithMessage("Le motif est obligatoire.");
        RuleFor(x => x.Dto.ExternalReference)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Dto.ExternalReference));
        RuleFor(x => x.Dto.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Dto.Notes));
        RuleFor(x => x.Dto.Lines).NotEmpty().WithMessage("Au moins une ligne est requise.");
        RuleForEach(x => x.Dto.Lines).SetValidator(new CreateStockVoucherLineDtoValidator());
        RuleFor(x => x.Dto)
            .Must(d => d.Kind.IsAllowedReason(d.Reason))
            .WithMessage("Motif incompatible avec le type de bon.");
    }
}

public sealed class CreateStockVoucherLineDtoValidator : AbstractValidator<CreateStockVoucherLineDto>
{
    public CreateStockVoucherLineDtoValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Le produit est obligatoire.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La quantité doit être supérieure à zéro.");
        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitCost.HasValue)
            .WithMessage("Le coût unitaire ne peut pas être négatif.");
    }
}

public sealed class UpdateStockVoucherCommandValidator : AbstractValidator<UpdateStockVoucherCommand>
{
    public UpdateStockVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.WarehouseId).NotEmpty().WithMessage("L'entrepôt est obligatoire.");
        RuleFor(x => x.Dto.VoucherDate).NotEmpty().WithMessage("La date est obligatoire.");
        RuleFor(x => x.Dto.Lines).NotEmpty().WithMessage("Au moins une ligne est requise.");
        RuleForEach(x => x.Dto.Lines).SetValidator(new CreateStockVoucherLineDtoValidator());
    }
}

public sealed class ValidateStockVoucherCommandValidator : AbstractValidator<ValidateStockVoucherCommand>
{
    public ValidateStockVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class CancelStockVoucherCommandValidator : AbstractValidator<CancelStockVoucherCommand>
{
    public CancelStockVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Le motif d'annulation est obligatoire.")
            .MaximumLength(1000);
    }
}

public sealed class DeleteStockVoucherCommandValidator : AbstractValidator<DeleteStockVoucherCommand>
{
    public DeleteStockVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
