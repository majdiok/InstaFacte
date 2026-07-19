namespace FactuTrust.Application.DTOs;

/// <summary>
/// En-tête commun des états comptables imprimés en PDF (nom société, matricule fiscal,
/// titre de l'état et libellé de période/exercice). Construit par les handlers d'export à
/// partir de la société par défaut.
/// </summary>
public sealed record AccountingReportHeader(
    string CompanyName,
    string? MatriculeFiscal,
    string Title,
    string PeriodLabel);
