using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Services;

/// <summary>Heuristic completion / quality scoring for permanent files (not persisted).</summary>
public static class PermanentFileQualityCalculator
{
    public sealed record QualityResult(
        bool IsBusinessComplete,
        IReadOnlyList<string> MissingItems,
        int CompletionPercent,
        int? NextRecommendedStep,
        string? NextActionLabel);

    public static QualityResult Compute(PermanentFile file, int activeRepresentativeCount)
    {
        var missing = new List<string>();
        var score = 0;
        const int totalWeight = 8;

        if (!string.IsNullOrWhiteSpace(file.CompanyName)) score++;
        else missing.Add("Raison sociale");

        if (!string.IsNullOrWhiteSpace(file.Nif)) score++;
        else missing.Add("NIF");

        if (file.LegalForm.HasValue) score++;
        else missing.Add("Forme juridique");

        if (activeRepresentativeCount >= 1) score++;
        else missing.Add("Dirigeant");

        if (file.LabCompleted) score++;
        else missing.Add("Questionnaire LAB");

        if (file.MissionAccepted) score++;
        else missing.Add("Lettre de mission");

        if (!string.IsNullOrWhiteSpace(file.Street) || !string.IsNullOrWhiteSpace(file.City)) score++;
        else missing.Add("Siège social");

        if (file.AnnualFeeAmount.HasValue) score++;
        else missing.Add("Honoraires");

        var percent = (int)Math.Round(score * 100.0 / totalWeight);
        var nextStep = RecommendNextStep(file, activeRepresentativeCount);
        var nextLabel = BuildNextActionLabel(file, missing, nextStep);

        var requiredMissing = missing.Where(m => m is not "Siège social" and not "Honoraires").ToList();
        var isComplete = file.Status == PermanentFileStatus.Complete
            && requiredMissing.Count == 0;

        return new QualityResult(isComplete, missing, percent, nextStep, nextLabel);
    }

    private static int? RecommendNextStep(PermanentFile file, int activeRepresentativeCount)
    {
        if (string.IsNullOrWhiteSpace(file.CompanyName) || string.IsNullOrWhiteSpace(file.Nif) || !file.LegalForm.HasValue)
            return 1;
        if (activeRepresentativeCount < 1)
            return 2;
        if (string.IsNullOrWhiteSpace(file.Street) && string.IsNullOrWhiteSpace(file.City))
            return 3;
        if (!file.LabCompleted || !file.MissionAccepted)
            return 4;
        if (!file.AnnualFeeAmount.HasValue)
            return 5;
        if (file.Status != PermanentFileStatus.Complete)
            return 6;
        return null;
    }

    private static string? BuildNextActionLabel(PermanentFile file, List<string> missing, int? nextStep)
    {
        if (file.Status == PermanentFileStatus.Archived)
            return null;
        if (file.Status == PermanentFileStatus.Complete && file.SyncedToTenantAt is null)
            return "Synchroniser";
        if (file.Status == PermanentFileStatus.Complete)
            return null;
        if (nextStep == 6)
            return "Finaliser";
        if (missing.Count > 0)
            return missing[0];
        return null;
    }
}
