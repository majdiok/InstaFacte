using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Concurrency coverage for the last-active-administrator protection (plan §6 Phase 2.3, §7.1.G).
/// Two administrators on the same tenant PATCH each other's role to a non-admin role at the same
/// time: the transactional tenant-row lock (<c>UPDLOCK, HOLDLOCK</c>) plus in-transaction admin
/// count must let exactly one of the two mutations through and reject the other with the French
/// "last administrator" message, leaving at least one active administrator on the tenant.
/// Requires a real SQL Server (company registration provisions a real tenant DB).
/// </summary>
public sealed class TenantUsersLastAdminTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private const string LastAdminMessage =
        "Impossible de rétrograder ou désactiver le dernier administrateur actif de l'entreprise.";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public TenantUsersLastAdminTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Concurrent_cross_demotion_of_the_two_last_admins_lets_exactly_one_succeed()
    {
        // Arrange: register a company (admin1) then add a second Administrator (admin2) to the
        // same tenant — these are the tenant's ONLY two active admins.
        var registerClient = NewClient();
        var company = await TenantUsersTestSupport.RegisterCompanyAsync(registerClient);
        var admin1Client = NewClient().WithBearer(company.AccessToken);

        var admin2Email = await TenantUsersTestSupport.CreateAdditionalAdminAsync(admin1Client);
        var admin2Token = await TenantUsersTestSupport.LoginAsync(NewClient(), admin2Email);
        var admin2Client = NewClient().WithBearer(admin2Token);

        var users = await TenantUsersTestSupport.GetUsersAsync(admin1Client);
        var admin1Id = company.AdminUserId;
        var admin2Id = users.Single(u => u.Email == admin2Email).Id;

        Assert.Equal(UserRole.Administrator, users.Single(u => u.Id == admin1Id).Role);
        Assert.Equal(UserRole.Administrator, users.Single(u => u.Id == admin2Id).Role);

        // Act: admin1 demotes admin2 WHILE admin2 demotes admin1, at the same time. Neither PATCH
        // is a self-demotion (that path is rejected earlier, unconditionally, and is not what this
        // test is about) — each admin targets the OTHER admin, which is the actual race the
        // transactional tenant-row lock has to serialize.
        var demoteAdmin2ByAdmin1 = admin1Client.PatchAsJsonAsync(
            $"/api/tenant-users/{admin2Id}",
            new UpdateTenantUserRequest { Role = UserRole.SalesRep },
            TenantUsersTestSupport.ApiJsonOptions);
        var demoteAdmin1ByAdmin2 = admin2Client.PatchAsJsonAsync(
            $"/api/tenant-users/{admin1Id}",
            new UpdateTenantUserRequest { Role = UserRole.SalesRep },
            TenantUsersTestSupport.ApiJsonOptions);

        var responses = await Task.WhenAll(demoteAdmin2ByAdmin1, demoteAdmin1ByAdmin2);

        // Assert: exactly one 200 and one 400 (order between the two concurrent requests is not
        // deterministic — only the outcome invariant matters).
        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }, statusCodes);

        var rejected = responses.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
        var rejectedRaw = await rejected.Content.ReadAsStringAsync();
        Assert.Contains(LastAdminMessage, rejectedRaw);

        // Final state: at least one of the two remains an active Administrator (never a fully
        // orphaned tenant), using a fresh admin session guaranteed to still be valid (the successful
        // demotion's own token might have just been revoked by the OTHER admin's successful call, so
        // re-fetch via whichever admin is still actually an administrator).
        var finalUsers = await GetUsersAsResilientAdminAsync(admin1Client, admin2Client);
        var activeAdminCount = finalUsers.Count(u => u.Role == UserRole.Administrator && u.IsActive);
        Assert.True(activeAdminCount >= 1, "Tenant must never end up with zero active administrators");
    }

    /// <summary>
    /// Reads the tenant's user list using whichever of the two admin sessions is still valid — one of
    /// them may have just had its own role changed (and its token revoked, plan §6 Phase 2.5) by the
    /// concurrent PATCH that succeeded against it.
    /// </summary>
    private static async Task<IReadOnlyList<TenantUserListItemDto>> GetUsersAsResilientAdminAsync(
        HttpClient client1, HttpClient client2)
    {
        try
        {
            return await TenantUsersTestSupport.GetUsersAsync(client1);
        }
        catch (InvalidOperationException)
        {
            return await TenantUsersTestSupport.GetUsersAsync(client2);
        }
    }
}
