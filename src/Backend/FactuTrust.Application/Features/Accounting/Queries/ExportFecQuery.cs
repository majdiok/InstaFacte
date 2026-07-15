using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record ExportFecQuery(int FiscalYear) : IRequest<Result<byte[]>>;

public sealed class ExportFecQueryHandler : IRequestHandler<ExportFecQuery, Result<byte[]>>
{
    private readonly IFecExportService _fecService;
    private readonly IJournalEntryRepository _journalEntries;

    public ExportFecQueryHandler(IFecExportService fecService, IJournalEntryRepository journalEntries)
    {
        _fecService = fecService;
        _journalEntries = journalEntries;
    }

    public async Task<Result<byte[]>> Handle(ExportFecQuery request, CancellationToken ct)
    {
        // Garde-fou légal : le FEC ne reflète que les écritures validées. Bloquer tant que des
        // écritures en brouillard subsistent sur l'exercice (no-op quand le brouillard est désactivé).
        var drafts = await _journalEntries.CountDraftsByFiscalYearAsync(request.FiscalYear, ct);
        if (drafts > 0)
            return Result.Failure<byte[]>(Error.Validation("Brouillard",
                $"{drafts} écriture(s) en brouillard sur l'exercice {request.FiscalYear}. Validez-les avant l'export FEC."));

        return await _fecService.ExportFecAsync(request.FiscalYear, ct);
    }
}
