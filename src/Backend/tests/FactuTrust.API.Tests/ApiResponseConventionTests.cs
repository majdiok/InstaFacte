using System.Reflection;
using FactuTrust.Application.DTOs;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Empêche la réintroduction d'un second <c>ApiResponse&lt;T&gt;</c> dans FactuTrust.API
/// ou d'une collision de nom avec FactuTrust.Application.DTOs.
/// </summary>
public sealed class ApiResponseConventionTests
{
    /// <summary>
    /// Types déclarés dans Controllers qui masquent volontairement un homonyme dans Application.DTOs.
    /// Documenter toute exemption ici — ne pas laisser la liste grossir sans revue explicite.
    /// </summary>
    private static readonly HashSet<string> AllowedControllerDtoNameCollisions =
        new(StringComparer.Ordinal)
        {
            "ApiResponse`1",
        };

    [Fact]
    public void FactuTrust_API_declares_exactly_one_ApiResponse_generic_type()
    {
        var apiAssembly = typeof(FactuTrust.API.Controllers.InventoryController).Assembly;

        var apiResponseTypes = apiAssembly
            .GetTypes()
            .Where(t => t.IsGenericTypeDefinition && t.Name.StartsWith("ApiResponse", StringComparison.Ordinal))
            .ToList();

        Assert.Single(apiResponseTypes);
        Assert.Equal(typeof(FactuTrust.API.Controllers.ApiResponse<>), apiResponseTypes[0]);
    }

    [Fact]
    public void Controllers_namespace_does_not_shadow_Application_DTOs_except_documented_exemptions()
    {
        var apiAssembly = typeof(FactuTrust.API.Controllers.InventoryController).Assembly;
        var dtoAssembly = typeof(ApiResponse<object>).Assembly;

        var controllerPublicTypes = apiAssembly
            .GetTypes()
            .Where(t => t.IsPublic && t.Namespace?.StartsWith("FactuTrust.API.Controllers", StringComparison.Ordinal) == true)
            .Select(t => t.IsGenericTypeDefinition ? t.Name : t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var dtoPublicTypeNames = dtoAssembly
            .GetTypes()
            .Where(t => t.IsPublic && t.Namespace?.StartsWith("FactuTrust.Application.DTOs", StringComparison.Ordinal) == true)
            .Select(t => t.IsGenericTypeDefinition ? t.Name : t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var collisions = controllerPublicTypes
            .Intersect(dtoPublicTypeNames, StringComparer.Ordinal)
            .Except(AllowedControllerDtoNameCollisions)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "Unexpected public type name collisions between FactuTrust.API.Controllers and FactuTrust.Application.DTOs: "
            + string.Join(", ", collisions));
    }
}
