using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Templates;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Création DÉTERMINISTE de plans Studio IA (flag <c>EnableStudioAiWorkbench</c>) : validation en
/// direct d'une spec (« Personnaliser » / « Tester »), création depuis une spec éditée
/// (<c>from-spec</c>) et instanciation d'un modèle embarqué (<c>from-template</c>).
/// AUCUN appel LLM, AUCUN exécuteur : le plan créé est <c>Pending</c> et immédiatement confirmable
/// par le chemin existant (<c>ConfirmStudioAiPlanCommand</c>). La création elle-même est DÉLÉGUÉE à
/// <c>CreateStudioAiPlanCommand</c> (même permission <see cref="StudioAiPlanDefaults.RequiredPermission"/>,
/// même audit <c>Studio.AiPlan.Created</c>) — seule la provenance de la spec change.
/// Le flag est vérifié DANS chaque handler (contrôleurs fins) : fonctionnalité coupée = introuvable.
/// </summary>
public static class StudioAiPlanCreation
{
    /// <summary>Borne de <c>DisplayNameOverride</c> (nom affiché du système instancié depuis un modèle).</summary>
    public const int MaxDisplayNameOverrideLength = 128;

    /// <summary>Nature de plan parsée par NOM exact d'enum, insensible à la casse (jamais par valeur numérique).</summary>
    public static bool TryParseKind(string? raw, out StudioAiPlanKind kind)
    {
        kind = default;
        var name = Enum.GetNames<StudioAiPlanKind>()
            .FirstOrDefault(n => string.Equals(n, raw?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null) return false;
        kind = Enum.Parse<StudioAiPlanKind>(name);
        return true;
    }

    /// <summary>
    /// Parse la spec via le <c>TryParse</c> de la nature (même aiguillage que le canonicaliseur et
    /// l'exécuteur), la canonicalise (<see cref="StudioAiSpecCanonical.CanonicalFor"/>) puis recalcule
    /// le résumé côté serveur (<c>ForSystem</c>/<c>ForApp</c>) depuis la forme canonique — aucun résumé
    /// client n'est jamais accepté. Pour les natures dont l'aperçu se résout contre le schéma réel ou
    /// des données vivantes (Amendment/View/Report), le résumé reste <c>null</c> : validation seule,
    /// même restriction que l'édition B-P0-04.
    /// </summary>
    public static bool TryCanonicalize(
        StudioAiPlanKind kind, string specJson, out string? canonical, out string? summary, out string? error)
    {
        canonical = StudioAiSpecCanonical.CanonicalFor(kind, specJson, out error);
        summary = null;
        if (canonical is null) return false;

        if (kind == StudioAiPlanKind.CreateSystem
            && StudioAiSystemSpec.TryParse(canonical, out var system, out _) && system is not null)
            summary = StudioAiPlanSummary.ForSystem(system);
        else if (kind == StudioAiPlanKind.CreateApp
            && StudioAiAppSpec.TryParse(canonical, out var app, out _) && app is not null)
            summary = StudioAiPlanSummary.ForApp(app);
        return true;
    }

    /// <summary>
    /// Spec DTO d'un plan qui vient d'être créé : le jeton de concurrence n'est pas exposé par
    /// <c>StudioAiPlanDto</c> ; le client le récupère via <c>GET {id}/spec</c> avant la première édition.
    /// </summary>
    public static StudioAiPlanSpecDto ToFreshSpecDto(StudioAiPlanDto plan, string canonical) =>
        new(plan.Id, plan.Kind, plan.Status, plan.ExpiresAt,
            RowVersion: string.Empty,
            JsonNode.Parse(canonical)!);
}

// ---- Contrats HTTP (corps des requêtes du contrôleur B-P0-08) ----

public sealed record CreatePlanFromSpecRequest(string Kind, string SpecJson);

public sealed record CreatePlanFromTemplateRequest(string? TemplateKey, Guid? TemplateId, string? DisplayNameOverride);

public sealed record ValidateStudioAiSpecRequest(string Kind, string SpecJson);

/// <summary>
/// Résultat d'une validation en direct. <c>Valid</c> est toujours <c>true</c> dans un succès : une spec
/// invalide est un ÉCHEC <c>Error.Validation("specJson", …)</c> (mappé en 400), jamais un 200
/// <c>{ valid = false }</c>. <c>Summary</c> est <c>null</c> pour les natures sans résumé recalculable
/// hors base (Amendment/View/Report).
/// </summary>
public sealed record StudioAiSpecValidationDto(
    bool Valid, JsonNode? Summary, JsonNode? CanonicalJson, IReadOnlyList<string> Warnings);

/// <summary>Plan créé + spec canonique persistée (même couple que <c>UpdateStudioAiPlanSpecResponse</c>).</summary>
public sealed record StudioAiPlanCreationResponse(StudioAiPlanDto Plan, StudioAiPlanSpecDto Spec);

// ---- Validate (validation en direct, SANS état : rien n'est créé, rien n'est lu en base) ----

public sealed record ValidateStudioAiSpecCommand(string Kind, string SpecJson)
    : IRequest<Result<StudioAiSpecValidationDto>>;

public sealed class ValidateStudioAiSpecCommandHandler
    : IRequestHandler<ValidateStudioAiSpecCommand, Result<StudioAiSpecValidationDto>>
{
    private readonly OllamaSettings _settings;

    public ValidateStudioAiSpecCommandHandler(IOptions<OllamaSettings> settings) => _settings = settings.Value;

    public Task<Result<StudioAiSpecValidationDto>> Handle(
        ValidateStudioAiSpecCommand command, CancellationToken cancellationToken)
    {
        if (!_settings.EnableStudioAiWorkbench)
            return Task.FromResult(Result.Failure<StudioAiSpecValidationDto>(
                Error.NotFound("Fonctionnalité non disponible.")));

        if (!StudioAiPlanCreation.TryParseKind(command.Kind, out var kind))
            return Task.FromResult(Result.Failure<StudioAiSpecValidationDto>(
                Error.Validation("kind", $"Nature de plan inconnue : « {command.Kind} ».")));

        if (string.IsNullOrWhiteSpace(command.SpecJson))
            return Task.FromResult(Result.Failure<StudioAiSpecValidationDto>(
                Error.Validation("specJson", "La spécification du plan est vide.")));
        if (command.SpecJson.Length > StudioAiPlanWorkbench.MaxSpecJsonLength)
            return Task.FromResult(Result.Failure<StudioAiSpecValidationDto>(
                Error.Validation("specJson", "La spec dépasse 256 Ko.")));

        if (!StudioAiPlanCreation.TryCanonicalize(kind, command.SpecJson, out var canonical, out var summary, out var error))
            return Task.FromResult(Result.Failure<StudioAiSpecValidationDto>(
                Error.Validation("specJson", error ?? "La spec est invalide.")));

        return Task.FromResult(Result.Success(new StudioAiSpecValidationDto(
            Valid: true,
            Summary: summary is null ? null : JsonNode.Parse(summary),
            CanonicalJson: JsonNode.Parse(canonical!),
            Warnings: Array.Empty<string>())));
    }
}

// ---- FromSpec (création d'un plan Pending depuis une spec éditée, sans LLM) ----

public sealed record CreateStudioAiPlanFromSpecCommand(string Kind, string SpecJson)
    : IRequest<Result<StudioAiPlanCreationResponse>>;

public sealed class CreateStudioAiPlanFromSpecCommandHandler
    : IRequestHandler<CreateStudioAiPlanFromSpecCommand, Result<StudioAiPlanCreationResponse>>
{
    private readonly IMediator _mediator;
    private readonly OllamaSettings _settings;

    public CreateStudioAiPlanFromSpecCommandHandler(IMediator mediator, IOptions<OllamaSettings> settings)
    {
        _mediator = mediator;
        _settings = settings.Value;
    }

    public async Task<Result<StudioAiPlanCreationResponse>> Handle(
        CreateStudioAiPlanFromSpecCommand command, CancellationToken cancellationToken)
    {
        if (!_settings.EnableStudioAiWorkbench)
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.NotFound("Fonctionnalité non disponible."));

        if (!StudioAiPlanCreation.TryParseKind(command.Kind, out var kind))
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("kind", $"Nature de plan inconnue : « {command.Kind} »."));
        // Seules les créations ont un résumé recalculable sans base (même restriction que l'édition B-P0-04).
        if (kind is not (StudioAiPlanKind.CreateSystem or StudioAiPlanKind.CreateApp))
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("kind", "Seuls les plans de création (système ou table) sont créables depuis une spec."));

        if (string.IsNullOrWhiteSpace(command.SpecJson))
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("specJson", "La spécification du plan est vide."));
        if (command.SpecJson.Length > StudioAiPlanWorkbench.MaxSpecJsonLength)
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("specJson", "La spec dépasse 256 Ko."));

        if (!StudioAiPlanCreation.TryCanonicalize(kind, command.SpecJson, out var canonical, out var summary, out var error))
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("specJson", error ?? "La spec est invalide."));

        // Délégation au handler du chemin LLM : MÊME permission RequiredPermission(kind), MÊME audit
        // « Studio.AiPlan.Created » — ici jamais de LLM, la spec et le résumé viennent du serveur.
        var created = await _mediator.Send(
            new CreateStudioAiPlanCommand(kind, canonical!, summary!), cancellationToken);
        if (created.IsFailure)
            return Result.Failure<StudioAiPlanCreationResponse>(created.Error);

        return Result.Success(new StudioAiPlanCreationResponse(
            created.Value, StudioAiPlanCreation.ToFreshSpecDto(created.Value, canonical!)));
    }
}

// ---- FromTemplate (instanciation d'un modèle embarqué, nature toujours CreateSystem) ----

public sealed record CreateStudioAiPlanFromTemplateCommand(string? TemplateKey, Guid? TemplateId, string? DisplayNameOverride)
    : IRequest<Result<StudioAiPlanCreationResponse>>;

public sealed class CreateStudioAiPlanFromTemplateCommandHandler
    : IRequestHandler<CreateStudioAiPlanFromTemplateCommand, Result<StudioAiPlanCreationResponse>>
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly OllamaSettings _settings;

    public CreateStudioAiPlanFromTemplateCommandHandler(
        IMediator mediator, ICurrentUser currentUser, IOptions<OllamaSettings> settings)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<StudioAiPlanCreationResponse>> Handle(
        CreateStudioAiPlanFromTemplateCommand command, CancellationToken cancellationToken)
    {
        if (!_settings.EnableStudioAiWorkbench)
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.NotFound("Fonctionnalité non disponible."));

        // Tenant requis ; la permission (RequiredPermission(CreateSystem) = StudioDesignEntities) est
        // portée par la politique du contrôleur et revérifiée par CreateStudioAiPlanCommand.
        if (!StudioContext.TryGet(_currentUser, out _, out _, out var contextError))
            return Result.Failure<StudioAiPlanCreationResponse>(contextError);

        // EXACTEMENT une source : clé de modèle embarqué OU identifiant de modèle tenant.
        var hasKey = !string.IsNullOrWhiteSpace(command.TemplateKey);
        var hasId = command.TemplateId is not null;
        if (hasKey == hasId)
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("templateKey", hasKey
                    ? "Indiquez soit une clé de modèle, soit un identifiant de modèle, pas les deux."
                    : "Une clé ou un identifiant de modèle est requis."));

        string specJson;
        if (hasKey)
        {
            var template = StudioTemplateCatalog.TryGet(command.TemplateKey);
            if (template is null)
                return Result.Failure<StudioAiPlanCreationResponse>(Error.NotFound("Modèle introuvable."));
            specJson = template.SpecJson;
        }
        else
        {
            // TODO (P3, B-P3-02) : résoudre le modèle TENANT via le dépôt CustomSystemTemplate ;
            // en P0 seuls les modèles embarqués (TemplateKey) sont instanciables.
            return Result.Failure<StudioAiPlanCreationResponse>(Error.NotFound("Modèle introuvable."));
        }

        // Le catalogue est validé au chargement ; re-parse pour appliquer l'override AVANT canonicalisation.
        if (!StudioAiSystemSpec.TryParse(specJson, out var parsed, out var parseError) || parsed is null)
            return Result.Failure<StudioAiPlanCreationResponse>(
                Error.Validation("specJson", parseError ?? "La spec du modèle est invalide."));

        if (!string.IsNullOrWhiteSpace(command.DisplayNameOverride))
        {
            var displayName = command.DisplayNameOverride.Trim();
            if (displayName.Length > StudioAiPlanCreation.MaxDisplayNameOverrideLength)
                return Result.Failure<StudioAiPlanCreationResponse>(
                    Error.Validation("displayNameOverride", "Le nom affiché dépasse 128 caractères."));
            // La clé système est re-slugifiée à l'exécution depuis displayName (StudioKey.Slugify).
            parsed = parsed with { SystemDisplayName = displayName };
        }

        var canonical = StudioAiSpecCanonical.CanonicalSystem(parsed);
        var summary = StudioAiPlanSummary.ForSystem(parsed); // résumé recalculé APRÈS l'override

        // Même délégation que from-spec : permission et audit « Studio.AiPlan.Created » inclus.
        var created = await _mediator.Send(
            new CreateStudioAiPlanCommand(StudioAiPlanKind.CreateSystem, canonical, summary), cancellationToken);
        if (created.IsFailure)
            return Result.Failure<StudioAiPlanCreationResponse>(created.Error);

        return Result.Success(new StudioAiPlanCreationResponse(
            created.Value, StudioAiPlanCreation.ToFreshSpecDto(created.Value, canonical)));
    }
}
