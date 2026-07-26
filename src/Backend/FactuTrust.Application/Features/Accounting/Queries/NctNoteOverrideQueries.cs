using FactuTrust.Application.Accounting;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Catalogue des notes annexes détaillées. Lecture PURE : projection du catalogue statique, aucun
/// accès à la base. Indispensable à l'écran de personnalisation, qui doit pouvoir lister — et donc
/// ré-afficher — une note masquée, absente par construction de la liasse.
/// </summary>
public sealed record GetNctNoteCatalogQuery : IRequest<Result<IReadOnlyList<NctNoteCatalogEntryDto>>>;

public sealed class GetNctNoteCatalogQueryHandler
    : IRequestHandler<GetNctNoteCatalogQuery, Result<IReadOnlyList<NctNoteCatalogEntryDto>>>
{
    public Task<Result<IReadOnlyList<NctNoteCatalogEntryDto>>> Handle(
        GetNctNoteCatalogQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<NctNoteCatalogEntryDto> entries = NctDetailedNoteCatalog.All
            .OrderBy(d => d.Number)
            .Select(d => new NctNoteCatalogEntryDto
            {
                Number = d.Number,
                DefaultTitle = d.Title,
                Family = d.Family
            })
            .ToList();

        return Task.FromResult(Result.Success(entries));
    }
}

public sealed record GetNctNoteOverridesQuery(int FiscalYear)
    : IRequest<Result<IReadOnlyList<NctNoteOverrideDto>>>;

public sealed class GetNctNoteOverridesQueryHandler
    : IRequestHandler<GetNctNoteOverridesQuery, Result<IReadOnlyList<NctNoteOverrideDto>>>
{
    private readonly INctNoteOverrideRepository _repository;

    public GetNctNoteOverridesQueryHandler(INctNoteOverrideRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<NctNoteOverrideDto>>> Handle(
        GetNctNoteOverridesQuery request, CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<IReadOnlyList<NctNoteOverrideDto>>(Error.Validation("FiscalYear", "Exercice invalide."));

        var rows = await _repository.GetByYearAsync(request.FiscalYear, cancellationToken);
        IReadOnlyList<NctNoteOverrideDto> dtos = rows
            .Select(o => new NctNoteOverrideDto
            {
                FiscalYear = o.FiscalYear,
                NoteNumber = o.NoteNumber,
                CustomTitle = o.CustomTitle,
                CustomDescription = o.CustomDescription,
                IsHidden = o.IsHidden
            })
            .ToList();

        return Result.Success(dtos);
    }
}
