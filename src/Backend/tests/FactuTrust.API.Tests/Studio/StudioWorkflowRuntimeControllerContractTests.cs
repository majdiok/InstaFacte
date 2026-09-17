using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers.Studio;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Workflows;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests.Studio;

/// <summary>
/// Contrat du contrôleur runtime des workflows (PR 4.2, tranche 4.2g) : 9 routes sous
/// <c>api/studio</c>, policies lecture <c>custom_records:read</c> (GET) / écriture
/// <c>custom_records:write</c> (POST), garde du drapeau <c>Ollama:EnableStudioWorkflows</c> (404 sans
/// AUCUN appel MediatR tant qu'il est coupé) et mappage Result → HTTP (400 validation, 404 introuvable
/// — approbation non assignée incluse, 409 conflit). <c>run</c> ⇒ 201 vers
/// <see cref="StudioWorkflowsController.GetInstance"/>.
/// </summary>
/// <remarks>
/// <see cref="FactuTrust.API.Controllers.ApiResponse{T}"/> masque le type Application homonyme dans le
/// namespace des contrôleurs : il est qualifié en entier ici pour lever l'ambiguïté.
/// </remarks>
public sealed class StudioWorkflowRuntimeControllerContractTests
{
    private const string EntityKey = "clients";
    private static readonly Guid ApprovalId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid InstanceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid RecordId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static readonly WorkflowInstanceDto InstanceDto = new(
        InstanceId, Guid.NewGuid(), "wf-manuel", "Relance manuelle", 1, Guid.NewGuid(), RecordId,
        "manual", "running", 0, null, null, Guid.NewGuid(), DateTime.UtcNow, null, 0, null, null);

    [Fact]
    public void Controller_is_routed_under_api_studio_with_read_policies_on_GET_and_write_policies_on_POST()
    {
        var type = typeof(StudioWorkflowRuntimeController);
        var route = type.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single();
        Assert.Equal("api/studio", route.Template);
        // La classe exige l'authentification, sans policy de conception (R15).
        Assert.Contains(type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), a => a.Policy is null);

        static string PolicyOf(string method) => typeof(StudioWorkflowRuntimeController).GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single(a => a.Policy is not null).Policy!;
        static string TemplateOf<T>(string method) where T : HttpMethodAttribute =>
            typeof(StudioWorkflowRuntimeController).GetMethod(method)!
                .GetCustomAttributes(typeof(T), true).Cast<T>().Single().Template!;

        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioWorkflowRuntimeController.ListMyApprovals)));
        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioWorkflowRuntimeController.CountMyApprovals)));
        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioWorkflowRuntimeController.ListRecordInstances)));
        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioWorkflowRuntimeController.ListRunnable)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioWorkflowRuntimeController.Approve)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioWorkflowRuntimeController.Reject)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioWorkflowRuntimeController.Run)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioWorkflowRuntimeController.Cancel)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioWorkflowRuntimeController.Remind)));

        Assert.Equal("workflows/approvals/mine", TemplateOf<HttpGetAttribute>(nameof(StudioWorkflowRuntimeController.ListMyApprovals)));
        Assert.Equal("workflows/approvals/mine/count", TemplateOf<HttpGetAttribute>(nameof(StudioWorkflowRuntimeController.CountMyApprovals)));
        Assert.Equal("workflows/approvals/{approvalId:guid}/approve", TemplateOf<HttpPostAttribute>(nameof(StudioWorkflowRuntimeController.Approve)));
        Assert.Equal("workflows/approvals/{approvalId:guid}/reject", TemplateOf<HttpPostAttribute>(nameof(StudioWorkflowRuntimeController.Reject)));
        Assert.Equal("records/{entityKey}/{recordId:guid}/workflow-instances", TemplateOf<HttpGetAttribute>(nameof(StudioWorkflowRuntimeController.ListRecordInstances)));
        Assert.Equal("records/{entityKey}/workflows", TemplateOf<HttpGetAttribute>(nameof(StudioWorkflowRuntimeController.ListRunnable)));
        Assert.Equal("records/{entityKey}/{recordId:guid}/workflows/{workflowKey}/run", TemplateOf<HttpPostAttribute>(nameof(StudioWorkflowRuntimeController.Run)));
        Assert.Equal("workflows/instances/{instanceId:guid}/cancel", TemplateOf<HttpPostAttribute>(nameof(StudioWorkflowRuntimeController.Cancel)));
        Assert.Equal("workflows/instances/{instanceId:guid}/remind", TemplateOf<HttpPostAttribute>(nameof(StudioWorkflowRuntimeController.Remind)));
    }

    [Fact]
    public async Task Every_route_returns_404_and_calls_nothing_when_the_flag_is_off()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = CreateController(mediator, workflowsEnabled: false);

        Assert.IsType<NotFoundObjectResult>(await controller.ListMyApprovals(cancellationToken: CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.CountMyApprovals(CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Approve(ApprovalId, null, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.Reject(ApprovalId, new ApprovalDecisionRequest("Non"), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(
            await controller.ListRecordInstances(EntityKey, RecordId, cancellationToken: CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.ListRunnable(EntityKey, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Run(EntityKey, RecordId, "wf-manuel", CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Cancel(InstanceId, null, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Remind(InstanceId, CancellationToken.None));

        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_returns_201_created_at_GetInstance_of_StudioWorkflowsController()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<RunWorkflowCommand>(c =>
                    c.EntityKey == EntityKey && c.RecordId == RecordId && c.WorkflowKey == "wf-manuel"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(InstanceDto));

        var result = await CreateController(mediator, workflowsEnabled: true)
            .Run(EntityKey, RecordId, "wf-manuel", CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(nameof(StudioWorkflowsController.GetInstance), created.ActionName);
        Assert.Equal("StudioWorkflows", created.ControllerName);
        Assert.Equal(InstanceId, created.RouteValues!["instanceId"]);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<WorkflowInstanceDto>>(created.Value);
        Assert.True(body.Success);
        Assert.Equal(InstanceId, body.Data!.Id);
    }

    [Fact]
    public async Task Reject_without_comment_maps_Validation_to_400()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<DecideApprovalCommand>(c =>
                    c.ApprovalId == ApprovalId && !c.Approve && c.Comment == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowInstanceDto>(
                Error.Validation("comment", "Un commentaire est requis pour refuser.")));

        var result = await CreateController(mediator, workflowsEnabled: true)
            .Reject(ApprovalId, new ApprovalDecisionRequest(null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Unassigned_approval_maps_NotFound_to_404_and_decided_maps_Conflict_to_409()
    {
        var unassigned = new Mock<IMediator>();
        unassigned.Setup(m => m.Send(It.IsAny<DecideApprovalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowInstanceDto>(Error.NotFound("StudioWorkflowApproval", ApprovalId)));
        var decided = new Mock<IMediator>();
        decided.Setup(m => m.Send(It.IsAny<DecideApprovalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowInstanceDto>(Error.Conflict("Cette approbation a déjà été traitée.")));

        var notFound = await CreateController(unassigned, workflowsEnabled: true)
            .Approve(ApprovalId, null, CancellationToken.None);
        var conflict = await CreateController(decided, workflowsEnabled: true)
            .Approve(ApprovalId, null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(notFound);
        Assert.IsType<ConflictObjectResult>(conflict);
    }

    [Fact]
    public async Task Remind_within_24_hours_maps_Conflict_to_409()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<RemindInstanceCommand>(c => c.InstanceId == InstanceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkflowInstanceDto>(
                Error.Conflict("Les approbateurs ont déjà été relancés il y a moins de 24 h.")));

        var result = await CreateController(mediator, workflowsEnabled: true)
            .Remind(InstanceId, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    private static StudioWorkflowRuntimeController CreateController(Mock<IMediator> mediator, bool workflowsEnabled) =>
        new(mediator.Object, Options.Create(new OllamaSettings { EnableStudioWorkflows = workflowsEnabled }));
}
