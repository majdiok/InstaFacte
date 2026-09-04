using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Services.Accounting;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Validators;

public sealed class CreateSubAccountCommandValidator : AbstractValidator<CreateSubAccountCommand>
{
    public CreateSubAccountCommandValidator()
    {
        // Forme et plafond délégués à AccountNumberRules : même règle que ChartOfAccount.Create, donc
        // impossible de refuser ici ce que le domaine accepterait, ou l'inverse.
        RuleFor(x => x.Request.AccountNumber)
            .NotEmpty().WithMessage("Le numéro de compte est obligatoire.")
            .Matches(AccountNumberRules.Pattern)
            .WithMessage("Le numéro de compte doit être un numéro SCE (chiffres, points autorisés).")
            .Must(n => AccountNumberRules.DigitCount(n) <= AccountNumberRules.MaxDigits)
            .WithMessage(n =>
                $"Le compte « {n.Request.AccountNumber?.Trim()} » comporte "
                + $"{AccountNumberRules.DigitCount(n.Request.AccountNumber)} chiffres : un numéro de "
                + $"compte ne peut pas en dépasser {AccountNumberRules.MaxDigits}.");

        RuleFor(x => x.Request.Label)
            .NotEmpty().WithMessage("Le libellé est obligatoire.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Request.AccountClass)
            .InclusiveBetween(1, 7).WithMessage("La classe de compte doit être entre 1 et 7.");

        RuleFor(x => x.Request.NatureType)
            .InclusiveBetween(0, 2).WithMessage("Le type de nature doit être entre 0 et 2.");

        RuleFor(x => x.Request.AccountType)
            .InclusiveBetween(0, 3).WithMessage("Le type de compte doit être entre 0 et 3.");
    }
}

public sealed class CreateManualJournalEntryCommandValidator : AbstractValidator<CreateManualJournalEntryCommand>
{
    private static readonly string[] ValidJournalCodes = { "JV", "JA", "JC", "JB", "JOD", "JIM", "JAN" };

    public CreateManualJournalEntryCommandValidator(IJournalRepository journals, IOptions<AccountingSettings> settings)
    {
        RuleFor(x => x.Request.JournalCode)
            .NotEmpty().WithMessage("Le code journal est obligatoire.")
            // Quand le catalogue de journaux est activé, valider contre le catalogue (superset des 7 seedés) ;
            // sinon, conserver la liste figée historique (aucune régression).
            .MustAsync(async (code, ct) =>
            {
                var c = (code ?? string.Empty).Trim().ToUpperInvariant();
                if (settings.Value.JournalCatalogEnabled)
                    return await journals.ExistsActiveJournalCodeAsync(c, ct);
                return ValidJournalCodes.Contains(c);
            })
            .WithMessage("Le code journal est inconnu ou inactif.");

        RuleFor(x => x.Request.EntryDate)
            .NotEmpty().WithMessage("La date est obligatoire.")
            .LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(1))
            .WithMessage("La date ne peut pas être dans le futur.");

        RuleFor(x => x.Request.Label)
            .NotEmpty().WithMessage("Le libellé est obligatoire.")
            .MaximumLength(500).WithMessage("Le libellé ne peut pas dépasser 500 caractères.");

        RuleFor(x => x.Request.PieceRef)
            .MaximumLength(50).WithMessage("La référence de pièce ne peut pas dépasser 50 caractères.");

        RuleFor(x => x.Request.Lines)
            .NotEmpty().WithMessage("Les lignes sont obligatoires.")
            .Must(lines => lines.Count >= 2)
            .WithMessage("Au moins deux lignes sont requises.");

        RuleFor(x => x.Request.Lines)
            .Must(lines =>
            {
                var totalDebit = lines.Sum(l => l.Debit);
                var totalCredit = lines.Sum(l => l.Credit);
                return Math.Abs(totalDebit - totalCredit) < 0.001m;
            })
            .WithMessage("L'écriture doit être équilibrée (total débit = total crédit).")
            .When(x => x.Request.Lines.Count >= 2);

        RuleForEach(x => x.Request.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountNumber)
                .NotEmpty().WithMessage("Le numéro de compte est obligatoire pour chaque ligne.");

            line.RuleFor(l => l.LineLabel)
                .NotEmpty().WithMessage("Le libellé est obligatoire pour chaque ligne.");

            line.RuleFor(l => l)
                .Must(l => !(l.Debit > 0 && l.Credit > 0))
                .WithMessage("Une ligne ne peut pas avoir simultanément un débit et un crédit.");
        });
    }
}

public sealed class SaveVatDeclarationCommandValidator : AbstractValidator<SaveVatDeclarationCommand>
{
    public SaveVatDeclarationCommandValidator()
    {
        RuleFor(x => x.Request.Year)
            .InclusiveBetween(2000, 2100).WithMessage("L'année doit être entre 2000 et 2100.");

        RuleFor(x => x.Request.Month)
            .InclusiveBetween(1, 12).WithMessage("Le mois doit être entre 1 et 12.");
    }
}

public sealed class UpdateAccountLabelCommandValidator : AbstractValidator<UpdateAccountLabelCommand>
{
    public UpdateAccountLabelCommandValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Le libellé est obligatoire.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");
    }
}

public sealed class LetterAccountEntriesCommandValidator : AbstractValidator<LetterAccountEntriesCommand>
{
    public LetterAccountEntriesCommandValidator()
    {
        RuleFor(x => x.JournalEntryLineIds)
            .NotEmpty().WithMessage("Au moins une ligne est requise.")
            .Must(ids => ids.Count >= 2)
            .WithMessage("Au moins deux lignes sont requises pour le lettrage.");
    }
}

public sealed class ReverseJournalEntryCommandValidator : AbstractValidator<ReverseJournalEntryCommand>
{
    public ReverseJournalEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("L'écriture à extourner est obligatoire.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Le motif de l'extourne est obligatoire.")
            .MaximumLength(400).WithMessage("Le motif ne peut pas dépasser 400 caractères.");
    }
}

public sealed class CloseAnnualPeriodCommandValidator : AbstractValidator<CloseAnnualPeriodCommand>
{
    public CloseAnnualPeriodCommandValidator()
    {
        RuleFor(x => x.FiscalYear)
            .InclusiveBetween(2000, 2100).WithMessage("L'exercice doit être entre 2000 et 2100.");
    }
}
