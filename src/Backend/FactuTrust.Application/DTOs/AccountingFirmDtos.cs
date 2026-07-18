using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record RegisterAccountingFirmDto
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string ConfirmPassword { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FirmName { get; init; } = null!;
    public string Nif { get; init; } = null!;
    public string Street { get; init; } = null!;
    public string? StreetLine2 { get; init; }
    public string City { get; init; } = null!;
    public string? PostalCode { get; init; }
    public string Governorate { get; init; } = null!;
    public string FirmEmail { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Website { get; init; }
    public string? Description { get; init; }
    public string? ProfessionalRegistrationNumber { get; init; }
    public bool IsPublicInDirectory { get; init; } = true;
}

public sealed record AccountingFirmDirectoryItemDto
{
    public Guid FirmTenantId { get; init; }
    public string DisplayName { get; init; } = null!;
    public string City { get; init; } = null!;
    public string Governorate { get; init; } = null!;
    public string? Description { get; init; }
    public string? ProfessionalRegistrationNumber { get; init; }
}

public sealed record AccountingFirmProfileDto
{
    public Guid TenantId { get; init; }
    public string DisplayName { get; init; } = null!;
    public string? Description { get; init; }
    public string City { get; init; } = null!;
    public string Governorate { get; init; } = null!;
    public bool IsPublicInDirectory { get; init; }
    public string? ProfessionalRegistrationNumber { get; init; }
    public string ContactEmail { get; init; } = null!;
    public string ContactPhone { get; init; } = null!;
}

public sealed record UpdateAccountingFirmProfileDto
{
    public string DisplayName { get; init; } = null!;
    public string? Description { get; init; }
    public string City { get; init; } = null!;
    public string Governorate { get; init; } = null!;
    public bool IsPublicInDirectory { get; init; }
    public string? ProfessionalRegistrationNumber { get; init; }
    public string ContactEmail { get; init; } = null!;
    public string ContactPhone { get; init; } = null!;
}

public sealed record RequestFirmAssignmentDto
{
    public Guid FirmTenantId { get; init; }
    public string? Notes { get; init; }
}

public sealed record FirmClientAssignmentDto
{
    public Guid Id { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public Guid FirmTenantId { get; init; }
    public string FirmDisplayName { get; init; } = null!;
    public FirmAssignmentStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public DateTime RequestedAt { get; init; }
    public DateTime? RespondedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? Notes { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed record RejectFirmAssignmentDto
{
    public string? Reason { get; init; }
}

public sealed record FirmClientDossierDto
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public DateTime ActiveSince { get; init; }
}

public sealed record SwitchFirmContextDto
{
    public Guid ClientTenantId { get; init; }
}

public sealed record FirmContextDto
{
    public Guid? ClientTenantId { get; init; }
    public string? ClientCompanyName { get; init; }
    public string AccessMode { get; init; } = "native";
}

public sealed record CreateFirmUserDto
{
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string Password { get; init; } = null!;
    public UserRole Role { get; init; }
}

public sealed record FirmUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public UserRole Role { get; init; }
    public string RoleDisplay { get; init; } = null!;
    public bool IsActive { get; init; }
}

public sealed record FirmDashboardClientRowDto
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public DateTime ActiveSince { get; init; }
    public DateTime? LastJournalEntryDate { get; init; }
    public bool IsInactive30Days { get; init; }
}

public sealed record FirmDashboardInvitationRowDto
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = null!;
    public DateTime RequestedAt { get; init; }
    public string? Notes { get; init; }
}

public sealed record FirmDashboardDto
{
    public int ActiveClientsCount { get; init; }
    public int PendingInvitationsCount { get; init; }
    public int InactiveDossiersCount { get; init; }
    public int VatDraftsCount { get; init; }
    public IReadOnlyList<FirmDashboardClientRowDto> Clients { get; init; } = Array.Empty<FirmDashboardClientRowDto>();
    public IReadOnlyList<FirmDashboardInvitationRowDto> PendingInvitations { get; init; } = Array.Empty<FirmDashboardInvitationRowDto>();
}
