using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.DTOs;

public sealed class FirmUserDtoContractTests
{
    [Fact]
    public void FirmUserDto_keeps_baseline_fields_for_consumers()
    {
        var dto = new FirmUserDto
        {
            Id = Guid.NewGuid(),
            Email = "a@b.co",
            FirstName = "A",
            LastName = "B",
            Role = UserRole.FirmAccountant,
            RoleDisplay = UserRole.FirmAccountant.ToDisplayString(),
            IsActive = true
        };

        Assert.False(string.IsNullOrWhiteSpace(dto.Email));
        Assert.Equal(UserRole.FirmAccountant, dto.Role);
        Assert.True(dto.IsActive);
        Assert.Null(dto.Qualification);
        Assert.Empty(dto.Binomes);
    }

    [Fact]
    public void CreateFirmUserDto_password_optional_for_invite_flow()
    {
        var dto = new CreateFirmUserDto
        {
            Email = "x@y.z",
            FirstName = "X",
            LastName = "Y",
            Role = UserRole.FirmAccountant,
            SendInvite = true
        };

        Assert.Null(dto.Password);
        Assert.True(dto.SendInvite);
    }
}
