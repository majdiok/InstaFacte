using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class ParentDeductionEligibilityResolverTests
{
    private static EmployeeDependentParent Claim(Guid employeeId, string cin, DependentParentKinship kinship = DependentParentKinship.Father)
    {
        var result = EmployeeDependentParent.Create(employeeId, cin, kinship, new DateTime(2026, 1, 1));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public void Resolve_CompleteClaim_ReturnsEffectiveCount()
    {
        var employeeId = Guid.NewGuid();
        var claims = new[] { Claim(employeeId, "12345678") };
        var index = new Dictionary<string, Guid> { ["12345678"] = employeeId };

        var result = ParentDeductionEligibilityResolver.ResolveForEmployee(1, claims, index, employeeId);

        Assert.Equal(ParentClaimsStatus.Complete, result.Status);
        Assert.Equal(1, result.EffectiveDependentParents);
        Assert.Null(result.WarningCode);
    }

    [Fact]
    public void Resolve_ConflictWithOtherEmployee_BlocksDeduction()
    {
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();
        var claims = new[] { Claim(employeeA, "87654321") };
        var index = new Dictionary<string, Guid> { ["87654321"] = employeeB };

        var result = ParentDeductionEligibilityResolver.ResolveForEmployee(1, claims, index, employeeA);

        Assert.Equal(ParentClaimsStatus.Conflict, result.Status);
        Assert.Equal(0, result.EffectiveDependentParents);
        Assert.Equal(ParentDeductionEligibilityResolver.ConflictErrorCode, result.WarningCode);
    }

    [Fact]
    public void Resolve_LegacyCountWithoutClaims_IsIncompleteWithZeroEffective()
    {
        var employeeId = Guid.NewGuid();

        var result = ParentDeductionEligibilityResolver.ResolveForEmployee(
            legacyDependentParentsCount: 2,
            activeClaimsForEmployee: Array.Empty<EmployeeDependentParent>(),
            activeCinOwners: new Dictionary<string, Guid>(),
            employeeId);

        Assert.Equal(ParentClaimsStatus.Incomplete, result.Status);
        Assert.Equal(0, result.EffectiveDependentParents);
        Assert.Equal(ParentDeductionEligibilityResolver.IncompleteWarningCode, result.WarningCode);
    }

    [Fact]
    public void ValidateNoConflicts_DetectsDuplicateCinAcrossEmployees()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var claims = new[]
        {
            Claim(a, "11223344"),
            Claim(b, "11223344")
        };

        var result = ParentDeductionEligibilityResolver.ValidateNoConflicts(claims);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public void ValidateNoConflicts_AllowsSameEmployeeTwoDifferentParents()
    {
        var employeeId = Guid.NewGuid();
        var claims = new[]
        {
            Claim(employeeId, "11111111", DependentParentKinship.Father),
            Claim(employeeId, "22222222", DependentParentKinship.Mother)
        };

        var result = ParentDeductionEligibilityResolver.ValidateNoConflicts(claims);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EmployeeDependentParent_RejectsSameCinAsEmployee()
    {
        var result = EmployeeDependentParent.Create(
            Guid.NewGuid(), "12345678", DependentParentKinship.Father, DateTime.UtcNow.Date,
            employeeCin: "12345678");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EmployeeDependentParent_ValidateSet_RejectsDuplicateKinship()
    {
        var employeeId = Guid.NewGuid();
        var claims = new[]
        {
            Claim(employeeId, "11111111", DependentParentKinship.Father),
            Claim(employeeId, "22222222", DependentParentKinship.Father)
        };

        var result = EmployeeDependentParent.ValidateSet(claims);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EndClaim_ReleasesParentCin()
    {
        var claim = Claim(Guid.NewGuid(), "33445566");
        Assert.True(claim.IsActive);

        var end = claim.End(new DateTime(2026, 6, 1));

        Assert.True(end.IsSuccess);
        Assert.False(claim.IsActive);
        Assert.Equal(new DateTime(2026, 6, 1), claim.EndDate);
    }

    [Fact]
    public void SyncDependentParentsCount_UpdatesDerivedField()
    {
        var employee = Employee.Create("EMP-P1", "Ali", "Ben", new DateTime(2020, 1, 1), dependentParents: 0).Value;
        var sync = employee.SyncDependentParentsCount(2);

        Assert.True(sync.IsSuccess);
        Assert.Equal(2, employee.DependentParents);
    }
}
