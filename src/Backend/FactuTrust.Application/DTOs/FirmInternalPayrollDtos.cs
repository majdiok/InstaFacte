namespace FactuTrust.Application.DTOs;

public sealed record FirmPayrollCollaboratorProvisioningRowDto
{
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public string? Email { get; init; }
    public bool HasPayrollEmployee { get; init; }
    public Guid? PayrollEmployeeId { get; init; }
    public string? PayrollEmployeeName { get; init; }
    public int PayrollLinkSource { get; init; }
    public string? BlockingReason { get; init; }

    /// <summary>
    /// Mentions obligatoires manquantes sur le salarié lié (N° CNSS, CIN).
    /// </summary>
    /// <remarks>
    /// Purement informatif : la paie reste calculable sans elles, mais la déclaration sociale ne
    /// passera pas. Un blocage ici remonterait dans le module paie client et serait une régression.
    /// </remarks>
    public IReadOnlyList<string> MissingPayrollIdentifiers { get; init; } = Array.Empty<string>();
}

public sealed record FirmPayrollProvisioningStatusDto
{
    public bool InternalPayrollEnabled { get; init; }
    public bool AutoProvisionOnCollaboratorCreate { get; init; }
    public int ActivePayrollEmployees { get; init; }
    public IReadOnlyList<FirmPayrollCollaboratorProvisioningRowDto> Collaborators { get; init; }
        = Array.Empty<FirmPayrollCollaboratorProvisioningRowDto>();

    /// <summary>Salariés liés à qui il manque une mention obligatoire pour la déclaration sociale.</summary>
    public int CollaboratorsWithIncompleteIdentity { get; init; }
}

public sealed record FirmPayrollProvisionResultDto
{
    public int Created { get; init; }
    public int Linked { get; init; }
    public int Skipped { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

/// <summary>Dossier paie saisi à la création d'un collaborateur cabinet (auto-provision).</summary>
public sealed record FirmCollaboratorPayrollOnboardingDto
{
    public string EmployeeNumber { get; init; } = null!;
    public DateTime HireDate { get; init; }
    public CreateContractDto Contract { get; init; } = null!;
}
