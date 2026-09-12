using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Systems;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Studio IA — PR 2.1 : les tables de jonction (<see cref="CustomEntityKind.Junction"/>) sont de la
/// plomberie N‑N : jamais listées dans la navigation utilisateur, qu'elles soient rattachées à un
/// système ou autonomes ; les tables standard restent visibles au même endroit qu'avant.
/// </summary>
public sealed class GetStudioNavQueryTests
{
    private static readonly Guid Tid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Uid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ICustomSystemRepository> _systems = new();
    private readonly Mock<ICustomEntityRepository> _entities = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public GetStudioNavQueryTests()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns(Tid);
        _currentUser.SetupGet(u => u.UserId).Returns(Uid);
    }

    [Fact]
    public async Task Junction_entities_are_hidden_inside_systems_and_at_the_root()
    {
        var rh = CustomSystemDefinition.Create(Tid, "rh", "Ressources humaines", "users", null, null, Uid);
        _systems.Setup(s => s.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomSystemDefinition> { rh });

        var employes = CustomEntityDefinition.Create(Tid, "employes", "Employé", "Employés", "user", null, Uid, rh.Id);
        var projets = CustomEntityDefinition.Create(Tid, "projets", "Projet", "Projets", "briefcase", null, Uid, rh.Id);
        var systemJunction = CustomEntityDefinition.Create(Tid, "employes_projets", "Employé – Projet", "Employé – Projet", "link", null, Uid, rh.Id, CustomEntityKind.Junction);
        var contacts = CustomEntityDefinition.Create(Tid, "contacts", "Contact", "Contacts", "id-card", null, Uid);
        var rootJunction = CustomEntityDefinition.Create(Tid, "contacts_projets", "Contact – Projet", "Contact – Projet", "link", null, Uid, null, CustomEntityKind.Junction);
        _entities.Setup(e => e.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomEntityDefinition> { employes, projets, systemJunction, contacts, rootJunction });

        var result = await Handler().Handle(new GetStudioNavQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        var nodes = result.Value;

        var system = Assert.Single(nodes, n => n.Kind == "system");
        Assert.Equal("rh", system.Key);
        Assert.NotNull(system.Children);
        Assert.Equal(new[] { "employes", "projets" }, system.Children!.Select(c => c.Key).ToArray());
        Assert.All(system.Children!, c => Assert.Equal("entity", c.Kind));

        var root = nodes.Where(n => n.Kind == "entity").Select(n => n.Key).ToArray();
        Assert.Equal(new[] { "contacts" }, root);

        // No junction leaks anywhere in the tree.
        var allKeys = nodes.SelectMany(n => (n.Children ?? Array.Empty<StudioNavNodeDto>()).Select(c => c.Key).Append(n.Key)).ToList();
        Assert.DoesNotContain("employes_projets", allKeys);
        Assert.DoesNotContain("contacts_projets", allKeys);
    }

    [Fact]
    public async Task System_whose_only_entity_is_a_junction_is_not_shown()
    {
        var liaison = CustomSystemDefinition.Create(Tid, "liaison", "Liaison", null, null, null, Uid);
        _systems.Setup(s => s.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomSystemDefinition> { liaison });
        _entities.Setup(e => e.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomEntityDefinition>
            {
                CustomEntityDefinition.Create(Tid, "a_b", "A – B", "A – B", "link", null, Uid, liaison.Id, CustomEntityKind.Junction)
            });

        var result = await Handler().Handle(new GetStudioNavQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Standard_entities_keep_their_route_and_plural_label()
    {
        _systems.Setup(s => s.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomSystemDefinition>());
        _entities.Setup(e => e.ListAsync(Tid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomEntityDefinition>
            {
                CustomEntityDefinition.Create(Tid, "contacts", "Contact", "Contacts", "id-card", null, Uid)
            });

        var result = await Handler().Handle(new GetStudioNavQuery(), CancellationToken.None);

        var node = Assert.Single(result.Value);
        Assert.Equal("entity", node.Kind);
        Assert.Equal("contacts", node.Key);
        Assert.Equal("Contacts", node.Label);
        Assert.Equal("id-card", node.Icon);
        Assert.Equal("/studio/d/contacts", node.Route);
        Assert.Null(node.Children);
    }

    private GetStudioNavQueryHandler Handler() => new(_systems.Object, _entities.Object, _currentUser.Object);
}
