using System.Globalization;
using System.IO.Compression;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Export d'archive de dossier (lecture seule) : réunit dans un ZIP le plan comptable, le journal
/// général, la balance, le plan tiers, le FEC et un manifeste, pour un exercice. N'écrit rien,
/// ne persiste rien — assemblage des états existants (mêmes octets que les exports unitaires).
/// </summary>
public sealed record ExportDossierArchiveQuery(int FiscalYear) : IRequest<Result<byte[]>>;

public sealed class ExportDossierArchiveQueryHandler : IRequestHandler<ExportDossierArchiveQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IFecExportService _fec;
    private readonly IClientRepository _clients;
    private readonly ISupplierRepository _suppliers;
    private readonly ICompanyRepository _companies;

    public ExportDossierArchiveQueryHandler(
        IMediator mediator,
        IAccountingExportService export,
        IFecExportService fec,
        IClientRepository clients,
        ISupplierRepository suppliers,
        ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _fec = fec;
        _clients = clients;
        _suppliers = suppliers;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportDossierArchiveQuery request, CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<byte[]>(Error.Validation("FiscalYear", "Exercice invalide."));

        var from = new DateTime(request.FiscalYear, 1, 1);
        var to = new DateTime(request.FiscalYear, 12, 31);

        var chart = await _mediator.Send(new GetChartOfAccountsQuery(), cancellationToken);
        if (chart.IsFailure) return Result.Failure<byte[]>(chart.Error);

        var journal = await _mediator.Send(new GetJournalEntriesQuery(null, from, to), cancellationToken);
        if (journal.IsFailure) return Result.Failure<byte[]>(journal.Error);

        var balance = await _mediator.Send(new GetBalanceQuery(from, to), cancellationToken);
        if (balance.IsFailure) return Result.Failure<byte[]>(balance.Error);

        var clients = await _clients.GetActiveClientsAsync(cancellationToken);
        var suppliers = await _suppliers.GetActiveSuppliersAsync(cancellationToken);
        var company = await _companies.GetDefaultAsync(cancellationToken);

        var files = new List<string>();

        await using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] content)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(content, 0, content.Length);
                files.Add(name);
            }

            Add("plan_comptable.csv", BuildChartCsv(chart.Value));
            Add("journal_general.csv", _export.ExportJournalToCsv(journal.Value));
            Add("balance.csv", _export.ExportBalanceToCsv(balance.Value));
            Add("tiers.csv", BuildThirdPartiesCsv(clients, suppliers));

            // FEC best-effort : un échec n'annule pas l'archive (l'exercice peut être vide).
            var fec = await _fec.ExportFecAsync(request.FiscalYear, cancellationToken);
            if (fec.IsSuccess)
                Add("fec.txt", fec.Value);

            Add("manifest.txt", BuildManifest(request.FiscalYear, company?.Name, files));
        }

        return Result.Success(ms.ToArray());
    }

    private static byte[] BuildChartCsv(IReadOnlyList<DTOs.ChartOfAccountDto> accounts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("compte;libelle;classe;nature;actif");
        foreach (var a in accounts)
        {
            sb.Append(Escape(a.AccountNumber)).Append(';');
            sb.Append(Escape(a.Label)).Append(';');
            sb.Append(a.AccountClass).Append(';');
            sb.Append(a.NatureType == 1 ? "credit" : "debit").Append(';');
            sb.AppendLine(a.IsActive ? "oui" : "non");
        }
        return WithBom(sb);
    }

    private static byte[] BuildThirdPartiesCsv(
        IReadOnlyList<Domain.Entities.Client> clients, IReadOnlyList<Domain.Entities.Supplier> suppliers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("type;nom;email;adresse;nif");
        foreach (var c in clients)
        {
            sb.Append("client;");
            sb.Append(Escape(c.Name)).Append(';');
            sb.Append(Escape(c.Email?.Value ?? string.Empty)).Append(';');
            sb.Append(Escape(c.Address?.ToSingleLine() ?? string.Empty)).Append(';');
            sb.AppendLine(Escape(c.NIF?.Value ?? string.Empty));
        }
        foreach (var s in suppliers)
        {
            sb.Append("fournisseur;");
            sb.Append(Escape(s.Name)).Append(';');
            sb.Append(Escape(s.Email?.Value ?? string.Empty)).Append(';');
            sb.Append(Escape(s.Address?.ToSingleLine() ?? string.Empty)).Append(';');
            sb.AppendLine(Escape(s.NIF?.Value ?? string.Empty));
        }
        return WithBom(sb);
    }

    private static byte[] BuildManifest(int fiscalYear, string? companyName, IReadOnlyList<string> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Archive de dossier comptable");
        sb.AppendLine($"Société : {companyName ?? "Société"}");
        sb.AppendLine($"Exercice : {fiscalYear}");
        sb.AppendLine($"Généré le : {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine("Fichiers inclus :");
        foreach (var f in files)
            sb.AppendLine($" - {f}");
        return WithBom(sb);
    }

    /// <summary>Échappement CSV minimal (guillemets si le champ contient ; " ou saut de ligne).</summary>
    private static string Escape(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }

    private static byte[] WithBom(StringBuilder sb)
    {
        // BOM UTF-8 : cohérent avec les autres exports CSV (ouverture correcte dans Excel FR).
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }
}
