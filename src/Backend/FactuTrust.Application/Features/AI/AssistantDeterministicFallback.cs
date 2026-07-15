using System.Text.RegularExpressions;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI;

public static class AssistantDeterministicFallback
{
    private static readonly Regex IdentityQuestionRegex = new(
        @"\b(qui\s+(es[- ]?tu|(?:êtes|etes)[- ]?vous)|vous\s+(?:êtes|etes)\s+qui|tu\s+es\s+qui|présente[- ]?toi|presente[- ]?toi|c['']est\s+quoi\s+instafact|what\s+are\s+you)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string TryBuild(
        Conversation conversation,
        string? userMessage,
        int minMeaningfulChars,
        bool enabled,
        bool toolsExecutedThisRequest,
        AiToolIntentRouter.AiToolIntent toolIntent)
    {
        if (!enabled)
            return string.Empty;

        if (LooksLikeIdentityQuestion(userMessage))
            return BuildIdentityResponse();

        if (toolIntent == AiToolIntentRouter.AiToolIntent.Greeting
            || AiToolIntentRouter.ShouldUseConversationalFastPath(toolIntent, userMessage))
        {
            return BuildGreetingResponse();
        }

        if (!toolsExecutedThisRequest)
        {
            return "Je n'ai pas pu formuler une réponse complète. Reformulez ou précisez votre question.";
        }

        var fromTools = BuildFromRecentToolResults(conversation, minMeaningfulChars, toolsExecutedThisRequest);
        if (!string.IsNullOrWhiteSpace(fromTools)
            && AssistantVisibleContentFormatter.HasMeaningfulAssistantText(fromTools, minMeaningfulChars))
        {
            return fromTools;
        }

        var enhanced = TryEnhanceLatestToolJson(conversation, minMeaningfulChars, toolsExecutedThisRequest);
        if (!string.IsNullOrWhiteSpace(enhanced)
            && AssistantVisibleContentFormatter.HasMeaningfulAssistantText(enhanced, minMeaningfulChars))
        {
            return enhanced;
        }

        return "Je n'ai pas pu formuler une réponse complète à partir des données récupérées. Reformulez ou précisez votre question.";
    }

    public static bool LooksLikeIdentityQuestion(string? userMessage) =>
        AiToolIntentRouter.LooksLikeIdentityQuestion(userMessage)
        || (!string.IsNullOrWhiteSpace(userMessage) && IdentityQuestionRegex.IsMatch(userMessage));

    public static string BuildIdentityResponse() =>
        $"Je suis l'assistant IA de **{BrandConstants.Name}**, intégré à votre application de gestion commerciale. "
        + "Je peux analyser vos ventes, stocks, marges et comptabilité à partir de vos données réelles, "
        + "produire des tableaux de bord et répondre à vos questions métier en français.";

    public static string BuildGreetingResponse() =>
        $"Bonjour ! Je suis l'assistant IA de **{BrandConstants.Name}**. "
        + "Posez-moi une question sur vos ventes, stocks, marges ou comptabilité — par exemple : "
        + "« Quel est mon chiffre d'affaires aujourd'hui ? »";

    private static string? BuildFromRecentToolResults(
        Conversation conversation,
        int minMeaningfulChars,
        bool toolsExecutedThisRequest)
    {
        if (!toolsExecutedThisRequest)
            return null;

        foreach (var msg in conversation.Messages.Reverse())
        {
            if (msg.Role != MessageRole.Tool || string.IsNullOrWhiteSpace(msg.Content))
                continue;

            var wrapped = $"```json\n{msg.Content.Trim()}\n```";
            var enhanced = AssistantVisibleContentFormatter.EnhanceForDisplay(wrapped, minMeaningfulChars);
            if (AssistantVisibleContentFormatter.HasMeaningfulAssistantText(enhanced, minMeaningfulChars))
                return enhanced;
        }

        return null;
    }

    private static string? TryEnhanceLatestToolJson(
        Conversation conversation,
        int minMeaningfulChars,
        bool toolsExecutedThisRequest)
    {
        if (!toolsExecutedThisRequest)
            return null;

        foreach (var msg in conversation.Messages.Reverse())
        {
            if (msg.Role != MessageRole.Tool || string.IsNullOrWhiteSpace(msg.Content))
                continue;

            var wrapped = $"```json\n{msg.Content.Trim()}\n```";
            return AssistantVisibleContentFormatter.EnhanceForDisplay(wrapped, minMeaningfulChars);
        }

        return null;
    }
}
