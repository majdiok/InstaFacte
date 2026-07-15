namespace FactuTrust.Domain.Enums;

public enum OpportunityStage
{
    Prospection = 0,
    Qualification = 1,
    Proposition = 2,
    Negotiation = 3,
    Won = 4,
    Lost = 5
}

public static class OpportunityStageExtensions
{
    public static string ToDisplayString(this OpportunityStage stage) => stage switch
    {
        OpportunityStage.Prospection => "Prospection",
        OpportunityStage.Qualification => "Qualification",
        OpportunityStage.Proposition => "Proposition",
        OpportunityStage.Negotiation => "Négociation",
        OpportunityStage.Won => "Gagnée",
        OpportunityStage.Lost => "Perdue",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    public static string ToCssClass(this OpportunityStage stage) => stage switch
    {
        OpportunityStage.Prospection => "info",
        OpportunityStage.Qualification => "primary",
        OpportunityStage.Proposition => "warning",
        OpportunityStage.Negotiation => "help",
        OpportunityStage.Won => "success",
        OpportunityStage.Lost => "danger",
        _ => "secondary"
    };

    public static bool IsOpen(this OpportunityStage stage) =>
        stage is not (OpportunityStage.Won or OpportunityStage.Lost);
}
