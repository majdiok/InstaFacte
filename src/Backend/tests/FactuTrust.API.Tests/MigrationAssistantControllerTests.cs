using System.Reflection;
using System.Text;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Migration assistée (N1) — garde-fous des endpoints d'assistance, testés en instanciation directe
/// (déterministe, sans base) : le feature flag <c>MigrationAi:Enabled</c> OFF (défaut) renvoie 404,
/// un fichier absent renvoie 400, et chaque action porte la politique <c>AccountingImport</c>
/// (vérifiée par réflexion — contrat d'autorisation figé).
/// </summary>
public sealed class MigrationAssistantControllerTests
{
    private static MigrationAssistantController BuildController(IMediator mediator, bool enabled)
        => new(mediator, Options.Create(new MigrationAiSettings { Enabled = enabled }));

    private static IFormFile CsvFile(string content)
        => new FormFile(new MemoryStream(Encoding.UTF8.GetBytes(content)), 0, Encoding.UTF8.GetByteCount(content), "file", "plan.csv");

    [Fact]
    public async Task AllActions_FlagDisabled_Return404()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = BuildController(mediator.Object, enabled: false);

        Assert.IsType<NotFoundResult>(await controller.Analyze(
            new MigrationAssistantController.MigrationFileFormRequest { File = CsvFile("compte\n6132\n") }, default));
        Assert.IsType<NotFoundResult>(await controller.SuggestColumnMapping(
            new MigrationAssistantController.MigrationTargetedFileFormRequest { File = CsvFile("compte\n6132\n"), Target = ReferenceImportTarget.ChartOfAccounts }, default));
        Assert.IsType<NotFoundResult>(await controller.SuggestAccountMapping(
            new MigrationAssistantController.MigrationAccountMappingFormRequest { File = CsvFile("compte\n6132\n") }, default));
        Assert.IsType<NotFoundResult>(await controller.DetectDuplicates(
            new MigrationAssistantController.MigrationFileFormRequest { File = CsvFile("nom\nAlpha\n") }, default));

        // Le flag OFF garantit l'absence totale d'appels métier : aucune commande n'est émise.
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Analyze_MissingFile_Returns400()
    {
        var controller = BuildController(new Mock<IMediator>().Object, enabled: true);

        var result = await controller.Analyze(
            new MigrationAssistantController.MigrationFileFormRequest { File = null! }, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Analyze_FlagEnabled_DelegatesToMediator()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<Result<MigrationAnalysisDto>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new MigrationAnalysisDto
            {
                FileName = "plan.csv",
                DetectedSource = MigrationSourceSystem.Inconnu,
                Format = JournalImportFormat.Csv,
                SuggestedTarget = ReferenceImportTarget.ChartOfAccounts
            }));
        var controller = BuildController(mediator.Object, enabled: true);

        var result = await controller.Analyze(
            new MigrationAssistantController.MigrationFileFormRequest { File = CsvFile("compte;libelle;classe\n6132;Loyer;6\n") }, default);

        Assert.IsType<OkObjectResult>(result);
        mediator.Verify(m => m.Send(It.IsAny<IRequest<Result<MigrationAnalysisDto>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Contrat d'autorisation : chaque action publique exige la politique AccountingImport.</summary>
    [Fact]
    public void AllActions_RequireAccountingImportPolicy()
    {
        var actions = typeof(MigrationAssistantController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(Task<IActionResult>));

        foreach (var action in actions)
        {
            var authorize = action.GetCustomAttributes<AuthorizeAttribute>()
                .FirstOrDefault(a => a.Policy == PermissionPolicies.AccountingImport);
            Assert.True(authorize is not null, $"{action.Name} doit porter [Authorize(Policy = AccountingImport)]");
        }
    }
}
