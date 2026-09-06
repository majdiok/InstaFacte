using System.Text.Json;
using FactuTrust.Application.DTOs;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Contrat des avertissements d'inscription (`AuthResponseDto.Warnings` / `WarningDetails`).
///
/// Contexte : le front préfixait TOUS les avertissements par « Certains modules n'ont pas pu être
/// activés : », y compris un avis de cohérence NIF/segment — alors que les modules étaient bien
/// activés. `WarningDetails` porte désormais un code stable pour lever cette ambiguïté, sans
/// retirer `Warnings` (string[]) sur lequel des clients existants s'appuient.
/// </summary>
public sealed class RegistrationWarningContractTests
{
    /// <summary>Mirrors Program.cs's <c>AddJsonOptions</c> (camelCase property names).</summary>
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AuthResponse_defaults_to_empty_warning_collections()
    {
        var response = new AuthResponseDto();

        Assert.Empty(response.Warnings);
        Assert.Empty(response.WarningDetails);
    }

    [Fact]
    public void WarningDetails_serialize_with_camelCase_code_message_severity()
    {
        var response = new AuthResponseDto
        {
            WarningDetails = new[]
            {
                RegistrationWarningDto.Warning(RegistrationWarningCodes.NifSegmentMismatch, "Message NIF")
            },
            Warnings = new[] { "Message NIF" }
        };

        var json = JsonSerializer.Serialize(response, ApiJsonOptions);

        Assert.Contains("\"warningDetails\"", json);
        Assert.Contains("\"code\":\"NIF_SEGMENT_MISMATCH\"", json);
        Assert.Contains("\"severity\":\"warning\"", json);
        Assert.Contains("\"message\":\"Message NIF\"", json);
    }

    [Fact]
    public void Warning_codes_are_stable_strings()
    {
        Assert.Equal("SECTOR_SELECTION_IGNORED", RegistrationWarningCodes.SectorSelectionIgnored);
        Assert.Equal("NIF_SEGMENT_MISMATCH", RegistrationWarningCodes.NifSegmentMismatch);
        Assert.Equal("MODULES_SELECTION_IGNORED", RegistrationWarningCodes.ModulesSelectionIgnored);
        Assert.Equal("MODULES_DENIED_BY_PLAN", RegistrationWarningCodes.ModulesDeniedByPlan);
        Assert.Equal("MODULES_ACTIVATION_FAILED", RegistrationWarningCodes.ModulesActivationFailed);
        Assert.Equal("MODULES_IGNORED_AT_REGISTRATION", RegistrationWarningCodes.ModulesIgnoredAtRegistration);
    }

    [Fact]
    public void Factories_apply_the_documented_severities()
    {
        Assert.Equal("info", RegistrationWarningDto.Info("X", "m").Severity);
        Assert.Equal("warning", RegistrationWarningDto.Warning("X", "m").Severity);
        Assert.Equal(RegistrationWarningSeverities.Info, RegistrationWarningDto.Info("X", "m").Severity);
        Assert.Equal(RegistrationWarningSeverities.Warning, RegistrationWarningDto.Warning("X", "m").Severity);
    }

    /// <summary>
    /// `Warnings` est dérivé de `WarningDetails` côté contrôleur : la vue « messages seuls » doit
    /// rester exhaustive pour un client qui ignore le champ typé.
    /// </summary>
    [Fact]
    public void Message_projection_covers_every_typed_warning()
    {
        var details = new[]
        {
            RegistrationWarningDto.Info(RegistrationWarningCodes.ModulesDeniedByPlan, "Offre supérieure requise."),
            RegistrationWarningDto.Warning(RegistrationWarningCodes.NifSegmentMismatch, "Vérifiez votre NIF.")
        };

        var messages = details.Select(w => w.Message).ToList();

        Assert.Equal(details.Length, messages.Count);
        Assert.Equal(details.Select(d => d.Message), messages);
    }
}
