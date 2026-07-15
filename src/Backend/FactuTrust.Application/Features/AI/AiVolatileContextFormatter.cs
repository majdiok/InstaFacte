using System.Text;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI;

/// <summary>Formats optional UI context for the LLM (not persisted with the conversation).</summary>
public sealed class AiVolatileContextFormatter
{
    public const int DefaultMaxAnalysisSummaryChars = 16384;

    private readonly ScreenAnalysisOptions _options;

    public AiVolatileContextFormatter(IOptions<ScreenAnalysisOptions> options)
    {
        _options = options.Value;
    }

    public static AiVolatileContextFormatter CreateDefault() =>
        new(Options.Create(new ScreenAnalysisOptions()));

    public string? Format(ChatUiContextDto? ctx, string? serverEnrichment = null)
    {
        if (ctx is null)
            return null;

        var hasAny = !string.IsNullOrWhiteSpace(ctx.Route)
                     || !string.IsNullOrWhiteSpace(ctx.ScreenId)
                     || ctx.Entity is { Type: not null, Id: not null }
                     || ctx.PathParams is { Count: > 0 }
                     || ctx.QueryParams is { Count: > 0 }
                     || !string.IsNullOrWhiteSpace(ctx.AnalysisSummary);

        if (!hasAny)
            return null;

        var hasScreenAnalysis = !string.IsNullOrWhiteSpace(ctx.AnalysisSummary);
        var maxChars = Math.Clamp(_options.MaxAnalysisSummaryChars, 4096, 65536);

        var sb = new StringBuilder();
        sb.AppendLine("CONTEXTE ÉCRAN (volatile — aide à interpréter la question ; ne pas stocker comme fait) :");

        if (hasScreenAnalysis)
        {
            sb.AppendLine("MODE ANALYSE ÉCRAN (prioritaire pour cette requête) :");
            if (!string.IsNullOrWhiteSpace(ctx.ScreenId))
                sb.AppendLine($"- L'utilisateur a cliqué « Analyser avec l'assistant IA » depuis l'écran {ctx.ScreenId}.");
            else
                sb.AppendLine("- L'utilisateur a cliqué « Analyser avec l'assistant IA » depuis un écran métier.");
            sb.AppendLine("- Le JSON ci-dessous est la source PRIMAIRE et AUTHORISÉE pour cette analyse.");
            sb.AppendLine("- Analyse directement ces données : ne demande PAS à l'utilisateur de copier/coller des données.");
            sb.AppendLine("- N'appelle des outils QUE pour compléter ou vérifier un point précis absent du snapshot.");
            sb.AppendLine("- Les montants officiels ultérieurs peuvent être confirmés via outils si nécessaire.");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(ctx.Route))
            sb.AppendLine($"- Route : {ctx.Route}");
        if (!string.IsNullOrWhiteSpace(ctx.ScreenId))
            sb.AppendLine($"- Écran : {ctx.ScreenId}");
        if (ctx.Entity is { } e && !string.IsNullOrWhiteSpace(e.Type) && !string.IsNullOrWhiteSpace(e.Id))
            sb.AppendLine($"- Entité focalisée : {e.Type} / id={e.Id}");
        if (ctx.PathParams is { Count: > 0 })
        {
            sb.Append("- Paramètres chemin : ");
            sb.AppendLine(string.Join(", ", ctx.PathParams.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        if (ctx.QueryParams is { Count: > 0 })
        {
            sb.Append("- Query : ");
            sb.AppendLine(string.Join(", ", ctx.QueryParams.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        if (!string.IsNullOrWhiteSpace(serverEnrichment))
        {
            sb.AppendLine("- Données complémentaires serveur (non contractuel) :");
            sb.AppendLine(serverEnrichment);
            sb.AppendLine();
        }

        if (hasScreenAnalysis)
        {
            var summary = ctx.AnalysisSummary!;
            if (summary.Length > maxChars)
                summary = summary[..maxChars] + "\n... [tronqué — aperçu non contractuel]";
            sb.AppendLine("- Aperçu données écran (non contractuel, JSON ou texte) :");
            sb.AppendLine(summary);
        }

        if (hasScreenAnalysis)
            sb.AppendLine("Utiliser le snapshot ci-dessus comme base de l'analyse demandée.");
        else
            sb.AppendLine("Utiliser ce contexte pour des réponses pertinentes ; les données chiffrées viennent toujours des outils.");

        return sb.ToString();
    }
}
