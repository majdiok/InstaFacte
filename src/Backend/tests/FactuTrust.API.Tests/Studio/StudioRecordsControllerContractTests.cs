using System.Text.Json.Nodes;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers.Studio;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests.Studio;

/// <summary>
/// Contrat de <see cref="StudioRecordsController"/> après la PR 2.1 : <c>List</c> transmet le filtre serveur
/// <c>filterField/filterValue</c> et borne <c>pageSize</c> à 1..200 AVANT MediatR ; <c>Create</c>/<c>Update</c>
/// mappent <c>record.duplicate_link</c> → 409 et conservent le 400 historique pour tous les autres codes.
/// </summary>
/// <remarks>
/// <see cref="FactuTrust.API.Controllers.ApiResponse{T}"/> masque le type Application homonyme dans le
/// namespace des contrôleurs : il est qualifié en entier ici pour lever l'ambiguïté.
/// </remarks>
public sealed class StudioRecordsControllerContractTests
{
    private const string EntityKey = "employes_projets";

    [Fact]
    public void Routes_and_policies_are_unchanged()
    {
        var route = typeof(StudioRecordsController).GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>().Single();
        Assert.Equal("api/studio/records/{entityKey}", route.Template);

        static string? PolicyOf(string method) => typeof(StudioRecordsController).GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Select(a => a.Policy)
            .Single();

        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioRecordsController.List)));
        Assert.Equal(PermissionPolicies.CustomRecordsRead, PolicyOf(nameof(StudioRecordsController.Schema)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioRecordsController.Create)));
        Assert.Equal(PermissionPolicies.CustomRecordsWrite, PolicyOf(nameof(StudioRecordsController.Update)));
    }

    [Fact]
    public async Task List_forwards_the_server_side_filter_to_the_query()
    {
        ListCustomRecordsQuery? captured = null;
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(m => m.Send(It.IsAny<ListCustomRecordsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<PagedResult<CustomRecordDto>>> q, CancellationToken _) => captured = (ListCustomRecordsQuery)q)
            .ReturnsAsync(Result.Success(PagedResult<CustomRecordDto>.Create(Array.Empty<CustomRecordDto>(), 1, 50, 0)));
        var controller = new StudioRecordsController(mediator.Object);

        var result = await controller.List(EntityKey, "dupont", 2, 50, "employes", "0f8fad5b-d9cb-469f-a165-70867728950e", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(EntityKey, captured!.EntityKey);
        Assert.Equal("dupont", captured.Search);
        Assert.Equal(2, captured.Page);
        Assert.Equal(50, captured.PageSize);
        Assert.Equal("employes", captured.FilterField);
        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", captured.FilterValue);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task List_without_filter_keeps_the_legacy_shape()
    {
        ListCustomRecordsQuery? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ListCustomRecordsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<PagedResult<CustomRecordDto>>> q, CancellationToken _) => captured = (ListCustomRecordsQuery)q)
            .ReturnsAsync(Result.Success(PagedResult<CustomRecordDto>.Create(Array.Empty<CustomRecordDto>(), 1, 25, 0)));

        await new StudioRecordsController(mediator.Object).List(EntityKey, null, 1, 25, null, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured!.FilterField);
        Assert.Null(captured.FilterValue);
        Assert.Equal(25, captured.PageSize);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(10_000, 200)]
    [InlineData(25, 25)]
    public async Task List_clamps_page_size_to_1_200_before_reaching_the_handler(int requested, int expected)
    {
        ListCustomRecordsQuery? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ListCustomRecordsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Result<PagedResult<CustomRecordDto>>> q, CancellationToken _) => captured = (ListCustomRecordsQuery)q)
            .ReturnsAsync(Result.Success(PagedResult<CustomRecordDto>.Create(Array.Empty<CustomRecordDto>(), 1, expected, 0)));

        await new StudioRecordsController(mediator.Object).List(EntityKey, null, 1, requested, null, null, CancellationToken.None);

        Assert.Equal(expected, captured!.PageSize);
    }

    [Fact]
    public async Task List_returns_400_for_an_invalid_filter_field()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ListCustomRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagedResult<CustomRecordDto>>(Error.Validation("filterField", "Champ de filtre inconnu : « nope ».")));

        var result = await new StudioRecordsController(mediator.Object)
            .List(EntityKey, null, 1, 25, "nope", "x", CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<object>>(bad.Value);
        Assert.False(body.Success);
        Assert.Contains("nope", body.Error);
    }

    [Fact]
    public async Task Create_maps_duplicate_link_to_409()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomRecordDto>(new Error(StudioErrorCodes.RecordDuplicateLink, "Ce lien existe déjà entre ces deux enregistrements.")));

        var result = await new StudioRecordsController(mediator.Object).Create(EntityKey, Payload(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<string>>(conflict.Value);
        Assert.False(body.Success);
        Assert.Equal("Ce lien existe déjà entre ces deux enregistrements.", body.Error);
    }

    [Fact]
    public async Task Update_maps_duplicate_link_to_409()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomRecordDto>(new Error(StudioErrorCodes.RecordDuplicateLink, "Ce lien existe déjà entre ces deux enregistrements.")));

        var result = await new StudioRecordsController(mediator.Object).Update(EntityKey, Guid.NewGuid(), Payload(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Theory]
    [InlineData("Validation.employes", "« Employé » est obligatoire.")]
    [InlineData("Validation.entityKey", "Table « employes_projets » introuvable ou inactive.")]
    [InlineData("Validation.quota", "Quota d'enregistrements atteint.")]
    [InlineData("CustomRecord.NotFound", "CustomRecord with ID '…' was not found.")]
    [InlineData("Conflict", "L'enregistrement a été modifié entre-temps.")]
    public async Task Create_and_update_keep_400_for_every_other_error_code(string code, string message)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomRecordDto>(new Error(code, message)));
        mediator.Setup(m => m.Send(It.IsAny<UpdateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomRecordDto>(new Error(code, message)));
        var controller = new StudioRecordsController(mediator.Object);

        var created = Assert.IsType<BadRequestObjectResult>(await controller.Create(EntityKey, Payload(), CancellationToken.None));
        var updated = Assert.IsType<BadRequestObjectResult>(await controller.Update(EntityKey, Guid.NewGuid(), Payload(), CancellationToken.None));

        foreach (var bad in new[] { created, updated })
        {
            var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<object>>(bad.Value);
            Assert.False(body.Success);
            Assert.Equal(message, body.Error);
        }
    }

    [Fact]
    public async Task Create_success_returns_the_record_in_the_envelope()
    {
        var dto = new CustomRecordDto(Guid.NewGuid(), JsonNode.Parse("""{"employes":"a","projets":"b"}"""), DateTime.UtcNow, DateTime.UtcNow, null);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateCustomRecordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var result = await new StudioRecordsController(mediator.Object).Create(EntityKey, Payload(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<FactuTrust.API.Controllers.ApiResponse<CustomRecordDto>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(dto.Id, body.Data!.Id);
    }

    private static SaveCustomRecordRequest Payload() => new(new Dictionary<string, JsonNode?>
    {
        ["employes"] = JsonValue.Create("0f8fad5b-d9cb-469f-a165-70867728950e"),
        ["projets"] = JsonValue.Create("7c9e6679-7425-40de-944b-e07fc1f90ae7")
    });
}
