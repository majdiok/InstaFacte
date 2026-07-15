namespace FactuTrust.Application.Features.Studio.Ai;

public sealed record StudioBuildStep(
    string Phase,
    string Label,
    string Status,
    string? EntityRef = null,
    string? Detail = null);

public interface IStudioBuildProgress
{
    void Report(StudioBuildStep step);
}