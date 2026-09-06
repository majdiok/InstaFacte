using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Forms;

// ---- Get the default form for an entity (falls back to a generated default) ----

public sealed record GetCustomFormQuery(Guid EntityId) : IRequest<Result<CustomFormDto>>;

public sealed class GetCustomFormQueryHandler : IRequestHandler<GetCustomFormQuery, Result<CustomFormDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomFormRepository _forms;
    private readonly ICurrentUser _currentUser;

    public GetCustomFormQueryHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomFormRepository forms, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _forms = forms;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomFormDto>> Handle(GetCustomFormQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<CustomFormDto>(err);

        var entity = await _entities.GetByIdAsync(tenantId, request.EntityId, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomFormDto>(Error.NotFound("CustomEntity", request.EntityId));

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var form = await _forms.GetDefaultByEntityAsync(tenantId, entity.Id, cancellationToken);

        var layout = form is null
            ? FormLayoutJson.BuildDefault(fields)
            : FormLayoutJson.SanitizeAgainstFields(FormLayoutJson.Parse(form.LayoutJson), fields);

        return Result.Success(new CustomFormDto(
            form?.Id ?? Guid.Empty,
            form?.Key ?? "default",
            form?.DisplayName ?? entity.DisplayName,
            true,
            layout));
    }
}

// ---- Upsert the default form layout ----

public sealed record UpsertDefaultFormCommand(Guid EntityId, SaveFormLayoutRequest Request) : IRequest<Result<CustomFormDto>>;

public sealed class UpsertDefaultFormCommandHandler : IRequestHandler<UpsertDefaultFormCommand, Result<CustomFormDto>>
{
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomFormRepository _forms;
    private readonly ICurrentUser _currentUser;

    public UpsertDefaultFormCommandHandler(
        ICustomEntityRepository entities, ICustomFieldRepository fields, ICustomFormRepository forms, ICurrentUser currentUser)
    {
        _entities = entities;
        _fields = fields;
        _forms = forms;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomFormDto>> Handle(UpsertDefaultFormCommand command, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out var userId, out var err))
            return Result.Failure<CustomFormDto>(err);

        // La commande est aussi invoquée hors contrôleur (plans IA : système, modification `set_form`).
        // Le droit de concevoir les formulaires est donc revérifié ici, au niveau réellement exécuté :
        // détenir `design_entities` n'autorise pas à lui seul à réécrire la mise en page d'un formulaire.
        if (!_currentUser.HasPermission(Permissions.Studio.DesignForms))
            return Result.Failure<CustomFormDto>(Error.Unauthorized("Permission de conception des formulaires requise."));

        var entity = await _entities.GetByIdAsync(tenantId, command.EntityId, cancellationToken);
        if (entity is null)
            return Result.Failure<CustomFormDto>(Error.NotFound("CustomEntity", command.EntityId));

        var fields = await _fields.ListByEntityAsync(tenantId, entity.Id, includeInactive: false, cancellationToken);
        var sanitized = FormLayoutJson.SanitizeAgainstFields(command.Request.Layout ?? new FormLayout(), fields);
        var layoutJson = FormLayoutJson.Serialize(sanitized);
        var displayName = string.IsNullOrWhiteSpace(command.Request.DisplayName) ? entity.DisplayName : command.Request.DisplayName.Trim();

        var form = await _forms.GetDefaultByEntityAsync(tenantId, entity.Id, cancellationToken);
        if (form is null)
        {
            form = CustomFormDefinition.Create(tenantId, entity.Id, $"{entity.Key}_default", displayName, layoutJson, isDefault: true, userId);
            await _forms.AddAsync(form, cancellationToken);
        }
        else
        {
            form.Update(displayName, layoutJson, isActive: true, userId);
            await _forms.UpdateAsync(form, cancellationToken);
        }

        return Result.Success(new CustomFormDto(form.Id, form.Key, form.DisplayName, true, sanitized));
    }
}
