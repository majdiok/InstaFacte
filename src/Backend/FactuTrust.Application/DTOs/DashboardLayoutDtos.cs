namespace FactuTrust.Application.DTOs;

/// <summary>Disposition (ordre des blocs) du tableau de bord d'un utilisateur.</summary>
public sealed record DashboardLayoutDto(IReadOnlyList<string> BlockOrder);

/// <summary>Requête d'enregistrement de l'ordre des blocs du tableau de bord.</summary>
public sealed class SaveDashboardLayoutRequest
{
    public List<string> BlockOrder { get; set; } = new();
}
