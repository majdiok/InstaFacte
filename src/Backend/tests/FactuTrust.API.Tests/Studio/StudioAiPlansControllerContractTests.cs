using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers.Studio;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests.Studio;

/// <summary>
/// Contrat des endpoints « workbench » des plans Studio IA (B-P0-08) : garde du flag
/// <c>EnableStudioAiWorkbench</c> (404 tant qu'il est coupé), politique d'autorisation, et
/// mappage des erreurs Result → codes HTTP (409 conflit de RowVersion, 401 permission révoquée,
/// 404 plan d'un autre utilisateur, 400 spec invalide).
/// </summary>
/// <remarks>
/// <see cref="FactuTrust.API.Controllers.ApiResponse{T}"/> masque le type Application homonyme
/// dans le namespace des contrôleurs : il est qualifié en entier ici pour lever l'ambiguïté.
/// </remarks>
public sealed class StudioAiPlansControllerContractTests
{
    [Fact]
    public void Controller_stays_routed_under_studio_ai_plans_with_design_policy()
    {
        var route = typeof(StudioAiPlansController).GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>().Single();
        Assert.Equal("api/studio/ai/plans", route.Template);

        var policies = typeof(StudioAiPlansController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Select(a => a.Policy);
        Assert.Contains(PermissionPolicies.StudioDesignEntities, policies);
    }

    [Fact]
    public async Task List_returns_404_and_calls_nothing_when_workbench_disabled()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = CreateController(mediator, workbenchEnabled: false);

        var result = await controller.List(null, null, 1, 20, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Spec_and_creation_endpoints_return_404_when_workbench_disabled()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = CreateController(mediator, workbenchEnabled: false);
        var id = Guid.NewGuid();

        Assert.IsType<NotFoundObjectResult>(await controller.GetSpec(id, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.UpdateSpec(id, new UpdateStudioAiPlanSpecRequest("{}", ""), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.CancelPending(CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.ValidateSpec(new ValidateStudioAiSpecRequest("CreateSystem", "{}"), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.CreateFromSpec(new CreatePlanFromSpecRequest("CreateSystem", "{}"), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.CreateFromTemplate(new CreatePlanFromTemplateRequest("gestion-conges", null, null), CancellationToken.None));
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_success_returns_paged_result_in_envelope()
    {
        var paged = PagedResult<StudioAiPlanListItemDto>.Create(
            new[]
            {
                new StudioAiPlanListItemDto(Guid.NewGuid(), "CreateSystem", "Pending", "Congés", 4,
                    DateTime.UtcNow, DateTime.UtcNow.AddMinutes(60), null, null)
            }, page: 1, pageSize: 20, totalCount: 1);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ListStudioAiPlansQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(paged));
        var controller = CreateController(mediator, workbenchEnabled: true);

        var result = await controller.List("Pending", null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<PagedResult<StudioAiPlanListItemDto>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Single(body.Data!.Items);
    }

    [Fact]
    public async Task Put_spec_returns_409_on_rowversion_conflict()
    {
        var result = await PutSpecFailingWith(Error.Conflict("Le plan a été modifié entre-temps. Rechargez-le."));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<string>>(conflict.Value);
        Assert.False(body.Success);
        Assert.Contains("modifié entre-temps", body.Error!);
    }

    [Fact]
    public async Task Put_spec_returns_400_on_invalid_spec()
    {
        var result = await PutSpecFailingWith(Error.Validation("specJson", "La spec dépasse 256 Ko."));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Put_spec_returns_401_when_permission_revoked()
    {
        var result = await PutSpecFailingWith(Error.Unauthorized("Permission de conception Studio requise."));

        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Put_spec_returns_404_for_another_users_plan()
    {
        var result = await PutSpecFailingWith(Error.NotFound("StudioAiBuildPlan", Guid.NewGuid()));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Put_spec_maps_free_text_not_found_to_404()
    {
        // Overload Error.NotFound(message) (code « NotFound » nu) utilisé par les handlers B-P0-05.
        var result = await PutSpecFailingWith(Error.NotFound("Fonctionnalité non disponible."));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Validate_success_returns_canonical_and_summary()
    {
        var validation = new StudioAiSpecValidationDto(
            Valid: true, Summary: null, CanonicalJson: null, Warnings: Array.Empty<string>());
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ValidateStudioAiSpecCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(validation));
        var controller = CreateController(mediator, workbenchEnabled: true);

        var result = await controller.ValidateSpec(
            new ValidateStudioAiSpecRequest("CreateSystem", "{}"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<StudioAiSpecValidationDto>>(ok.Value);
        Assert.True(body.Success);
        Assert.True(body.Data!.Valid);
    }

    private static async Task<IActionResult> PutSpecFailingWith(Error error)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdateStudioAiPlanSpecCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<UpdateStudioAiPlanSpecResponse>(error));
        var controller = CreateController(mediator, workbenchEnabled: true);

        return await controller.UpdateSpec(
            Guid.NewGuid(), new UpdateStudioAiPlanSpecRequest("{\"system\":{}}", "AAAA"), CancellationToken.None);
    }

    private static StudioAiPlansController CreateController(Mock<IMediator> mediator, bool workbenchEnabled) =>
        new(mediator.Object, Options.Create(new OllamaSettings { EnableStudioAiWorkbench = workbenchEnabled }));
}
