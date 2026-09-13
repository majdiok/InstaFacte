using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Relations;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.1 : la projection « relations d'une entité » (partagée entre
/// <c>GET api/studio/entities/{id}/relations</c> et <c>CustomEntitySchemaDto.Relations</c>) expose les
/// trois natures (<c>many_to_one</c>, <c>one_to_many</c>, <c>many_to_many</c>), ignore les champs/tables
/// inactifs et les références inconnues, et lit les champs table par table, STRICTEMENT en séquence
/// (un DbContext scoped n'est pas thread-safe — jamais de <c>Task.WhenAll</c> sur le même dépôt).
/// </summary>
public sealed class ListEntityRelationsQueryTests
{
    private static readonly Guid Tid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Uid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly CustomEntityDefinition _employes = CustomEntityDefinition.Create(Tid, "employes", "Employé", "Employés", null, null, Uid);
    private readonly CustomEntityDefinition _projets = CustomEntityDefinition.Create(Tid, "projets", "Projet", "Projets", null, null, Uid);
    private readonly CustomEntityDefinition _services = CustomEntityDefinition.Create(Tid, "services", "Service", "Services", null, null, Uid);
    private readonly CustomEntityDefinition _junction = CustomEntityDefinition.Create(Tid, "employes_projets", "Employé – Projet", "Employé – Projet", "link", null, Uid, null, CustomEntityKind.Junction);

    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly SequentialFieldRepo _fields = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public ListEntityRelationsQueryTests()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns(Tid);
        _currentUser.SetupGet(u => u.UserId).Returns(Uid);

        foreach (var e in new[] { _employes, _projets, _services, _junction })
            _entities.Setup(r => r.GetByIdAsync(Tid, e.Id, It.IsAny<CancellationToken>())).ReturnsAsync(e);

        _entities.Setup(r => r.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomEntityDefinition> { _employes, _projets, _services, _junction });

        // employes.service → services (many_to_one from employes; one_to_many from services)
        _fields.Add(_employes.Id, Relation(_employes.Id, "service", "services", sortOrder: 0));
        // junction: employes + projets (many_to_many)
        _fields.Add(_junction.Id, Relation(_junction.Id, "employes", "employes", sortOrder: 0, required: true));
        _fields.Add(_junction.Id, Relation(_junction.Id, "projets", "projets", sortOrder: 1, required: true));
        // noise: a text field and an inactive relation on projets, a relation to an unknown table
        _fields.Add(_projets.Id, CustomFieldDefinition.Create(Tid, _projets.Id, "nom", "Nom", CustomFieldType.Text, true, false, 0, null, null, null, Uid));
        var inactive = Relation(_projets.Id, "ancien_chef", "employes", sortOrder: 1);
        inactive.Update(inactive.Label, inactive.IsRequired, inactive.IsUnique, inactive.ValidationRulesJson, inactive.OptionsJson, inactive.DefaultValueJson, isActive: false, Uid);
        _fields.Add(_projets.Id, inactive);
        _fields.Add(_projets.Id, Relation(_projets.Id, "fantome", "table_inconnue", sortOrder: 2));
    }

    [Fact]
    public async Task Employes_sees_its_many_to_one_and_the_many_to_many_through_the_junction()
    {
        var result = await Handler().Handle(new ListEntityRelationsQuery(_employes.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var relations = result.Value;
        Assert.Equal(2, relations.Count);

        var manyToOne = Assert.Single(relations, r => r.Kind == EntityRelationKinds.ManyToOne);
        Assert.Equal(_employes.Id, manyToOne.SourceEntityId);
        Assert.Equal("employes", manyToOne.SourceEntityKey);
        Assert.Equal(_services.Id, manyToOne.TargetEntityId);
        Assert.Equal("services", manyToOne.TargetEntityKey);
        Assert.Equal("Service", manyToOne.TargetLabel);
        Assert.Equal("service", manyToOne.FieldKey);
        Assert.Null(manyToOne.JunctionEntityId);
        Assert.Null(manyToOne.JunctionEntityKey);
        Assert.Null(manyToOne.JunctionTargetFieldId);

        var manyToMany = Assert.Single(relations, r => r.Kind == EntityRelationKinds.ManyToMany);
        Assert.Equal(_employes.Id, manyToMany.SourceEntityId);
        Assert.Equal(_projets.Id, manyToMany.TargetEntityId);
        Assert.Equal("projets", manyToMany.TargetEntityKey);
        Assert.Equal(_junction.Id, manyToMany.JunctionEntityId);
        Assert.Equal("employes_projets", manyToMany.JunctionEntityKey);
        Assert.Equal("employes", manyToMany.FieldKey); // the junction field pointing at the requested entity
        Assert.True(manyToMany.IsRequired);
        var junctionFields = await _fields.ListByTypeAsync(Tid, CustomFieldType.RelationCustom, includeInactive: false);
        Assert.Equal(junctionFields.Single(f => f.EntityDefinitionId == _junction.Id && f.Key == "employes").Id, manyToMany.FieldId);
        Assert.Equal(junctionFields.Single(f => f.Key == "projets").Id, manyToMany.JunctionTargetFieldId);
        Assert.Equal("projets", manyToMany.JunctionTargetFieldKey);
    }

    [Fact]
    public async Task Projets_sees_the_many_to_many_from_its_own_side_and_no_inactive_or_dangling_links()
    {
        var result = await Handler().Handle(new ListEntityRelationsQuery(_projets.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var relation = Assert.Single(result.Value);
        Assert.Equal(EntityRelationKinds.ManyToMany, relation.Kind);
        Assert.Equal(_projets.Id, relation.SourceEntityId);
        Assert.Equal(_employes.Id, relation.TargetEntityId);
        Assert.Equal("Employé", relation.TargetLabel);
        Assert.Equal("projets", relation.FieldKey);
        Assert.Equal(_junction.Id, relation.JunctionEntityId);
        // the inactive `ancien_chef` and the dangling `fantome` relations are ignored
        Assert.DoesNotContain(result.Value, r => r.FieldKey is "ancien_chef" or "fantome");
    }

    [Fact]
    public async Task Services_sees_the_reverse_one_to_many()
    {
        var result = await Handler().Handle(new ListEntityRelationsQuery(_services.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var relation = Assert.Single(result.Value);
        Assert.Equal(EntityRelationKinds.OneToMany, relation.Kind);
        Assert.Equal(_services.Id, relation.SourceEntityId);
        Assert.Equal(_employes.Id, relation.TargetEntityId);
        Assert.Equal("employes", relation.TargetEntityKey);
        Assert.Equal("service", relation.FieldKey);
        Assert.Null(relation.JunctionEntityId);
    }

    [Fact]
    public async Task Unknown_entity_is_not_found_and_reads_nothing_else()
    {
        var result = await Handler().Handle(new ListEntityRelationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CustomEntity.NotFound", result.Error.Code);
        Assert.Equal(0, _fields.TotalCalls);
    }

    /// <summary>
    /// Revue PR 2.1 (N+1) : le résolveur ne lit plus les champs table par table (jusqu'à 50 requêtes
    /// par ouverture de formulaire) mais UNE requête <c>ListByTypeAsync(RelationCustom)</c>.
    /// </summary>
    [Fact]
    public async Task Field_reads_are_a_single_tenant_wide_relation_query()
    {
        var result = await Handler().Handle(new ListEntityRelationsQuery(_employes.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(1, _fields.TotalCalls);       // one ListByTypeAsync for the whole tenant
        Assert.Equal(1, _fields.MaxConcurrency);
    }

    [Fact]
    public async Task Missing_tenant_is_unauthorized()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns((Guid?)null);

        var result = await Handler().Handle(new ListEntityRelationsQuery(_employes.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    // ---- helpers ----

    private ListEntityRelationsQueryHandler Handler() => new(_entities.Object, _fields, _currentUser.Object);

    private static CustomFieldDefinition Relation(Guid entityId, string key, string targetKey, int sortOrder, bool required = false) =>
        CustomFieldDefinition.Create(Tid, entityId, key, key, CustomFieldType.RelationCustom, required, false, sortOrder,
            null, StudioFieldJson.SerializeRelation(new RelationRefDto("custom", targetKey)), null, Uid);

    /// <summary>
    /// Fake champ-repo qui détecte toute lecture concurrente : <see cref="MaxConcurrency"/> doit rester à 1.
    /// <c>await Task.Yield()</c> laisse une chance réelle à un <c>Task.WhenAll</c> fautif de se chevaucher.
    /// </summary>
    private sealed class SequentialFieldRepo : ICustomFieldRepository
    {
        private readonly Dictionary<Guid, List<CustomFieldDefinition>> _byEntity = new();
        private int _inFlight;
        public int TotalCalls;
        public int MaxConcurrency;

        public void Add(Guid entityId, CustomFieldDefinition field)
        {
            if (!_byEntity.TryGetValue(entityId, out var list))
                _byEntity[entityId] = list = new List<CustomFieldDefinition>();
            list.Add(field);
        }

        public async Task<IReadOnlyList<CustomFieldDefinition>> ListByTypeAsync(Guid tenantId, CustomFieldType fieldType, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _inFlight);
            Interlocked.Increment(ref TotalCalls);
            if (now > MaxConcurrency) MaxConcurrency = now;
            try
            {
                await Task.Yield();
                await Task.Delay(1, cancellationToken);
                return _byEntity
                    .SelectMany(kv => kv.Value)
                    .Where(f => f.FieldType == fieldType && (includeInactive || f.IsActive))
                    .OrderBy(f => f.EntityDefinitionId)
                    .ThenBy(f => f.SortOrder)
                    .ThenBy(f => f.Key, StringComparer.Ordinal)
                    .ToList();
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Task<IReadOnlyList<CustomFieldDefinition>> ListByEntityAsync(Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomFieldDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> KeyExistsAsync(Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> MaxSortOrderAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRangeAsync(IReadOnlyList<CustomFieldDefinition> fields, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
