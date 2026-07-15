using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class CustomRecordValidatorTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    private static CustomFieldDefinition Field(
        string key, CustomFieldType type, bool required = false,
        string? rulesJson = null, string? optionsJson = null, int sort = 0) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, type, required, false, sort, rulesJson, optionsJson, null, null);

    private static Dictionary<string, JsonNode?> Data(params (string Key, JsonNode? Value)[] pairs)
    {
        var dict = new Dictionary<string, JsonNode?>();
        foreach (var (k, v) in pairs) dict[k] = v;
        return dict;
    }

    [Fact]
    public void Rejects_unknown_keys()
    {
        var fields = new[] { Field("name", CustomFieldType.Text) };
        var result = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("bogus", JsonValue.Create("x"))));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Required_missing_fails()
    {
        var fields = new[] { Field("name", CustomFieldType.Text, required: true) };
        var result = CustomRecordValidator.ValidateAndCanonicalize(fields, Data());
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Valid_text_record_is_canonicalized()
    {
        var fields = new[] { Field("name", CustomFieldType.Text, required: true) };
        var result = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("name", JsonValue.Create("Acme"))));
        Assert.True(result.IsSuccess);
        Assert.Contains("\"name\"", result.Value);
        Assert.Contains("Acme", result.Value);
    }

    [Fact]
    public void Text_maxlength_enforced()
    {
        var rules = StudioFieldJson.SerializeRules(new FieldValidationRules { MaxLength = 3 });
        var fields = new[] { Field("code", CustomFieldType.Text, rulesJson: rules) };
        var ok = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("code", JsonValue.Create("abc"))));
        var tooLong = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("code", JsonValue.Create("abcd"))));
        Assert.True(ok.IsSuccess);
        Assert.True(tooLong.IsFailure);
    }

    [Fact]
    public void Number_must_be_integer()
    {
        var fields = new[] { Field("qty", CustomFieldType.Number) };
        var ok = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("qty", JsonValue.Create(5))));
        var notInt = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("qty", JsonValue.Create(5.5))));
        Assert.True(ok.IsSuccess);
        Assert.True(notInt.IsFailure);
    }

    [Fact]
    public void Number_range_enforced()
    {
        var rules = StudioFieldJson.SerializeRules(new FieldValidationRules { Min = 1, Max = 10 });
        var fields = new[] { Field("qty", CustomFieldType.Number, rulesJson: rules) };
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("qty", JsonValue.Create(5)))).IsSuccess);
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("qty", JsonValue.Create(0)))).IsFailure);
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("qty", JsonValue.Create(11)))).IsFailure);
    }

    [Fact]
    public void Select_membership_enforced()
    {
        var options = StudioFieldJson.SerializeOptions(new[] { new SelectOptionDto("open", "Open"), new SelectOptionDto("closed", "Closed") });
        var fields = new[] { Field("status", CustomFieldType.Select, optionsJson: options) };
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("status", JsonValue.Create("open")))).IsSuccess);
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("status", JsonValue.Create("invalid")))).IsFailure);
    }

    [Fact]
    public void Boolean_type_checked()
    {
        var fields = new[] { Field("flag", CustomFieldType.Boolean) };
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("flag", JsonValue.Create(true)))).IsSuccess);
        Assert.True(CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("flag", JsonValue.Create("nope")))).IsFailure);
    }

    [Fact]
    public void Optional_missing_fields_are_skipped()
    {
        var fields = new[]
        {
            Field("name", CustomFieldType.Text, required: true, sort: 0),
            Field("note", CustomFieldType.Text, required: false, sort: 1)
        };
        var result = CustomRecordValidator.ValidateAndCanonicalize(fields, Data(("name", JsonValue.Create("X"))));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("note", result.Value);
    }

    [Fact]
    public void AutoNumber_user_input_is_ignored()
    {
        // The compute-on-write step owns AutoNumber; any value the client sends must be dropped.
        var fields = new[]
        {
            Field("name", CustomFieldType.Text, sort: 0),
            Field("ref", CustomFieldType.AutoNumber, sort: 1)
        };
        var result = CustomRecordValidator.ValidateAndCanonicalize(
            fields, Data(("name", JsonValue.Create("X")), ("ref", JsonValue.Create("HACK-999"))));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("HACK-999", result.Value);
        Assert.DoesNotContain("\"ref\"", result.Value);
    }

    [Fact]
    public void AutoNumber_required_does_not_block_when_absent()
    {
        // AutoNumber is always filled server-side, so "required" must never fail validation.
        var fields = new[] { Field("ref", CustomFieldType.AutoNumber, required: true) };
        var result = CustomRecordValidator.ValidateAndCanonicalize(fields, Data());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void IsComputed_flags_all_server_derived_types()
    {
        Assert.True(CustomRecordValidator.IsComputed(CustomFieldType.AutoNumber));
        Assert.True(CustomRecordValidator.IsComputed(CustomFieldType.Formula));
        Assert.True(CustomRecordValidator.IsComputed(CustomFieldType.Lookup));
        Assert.True(CustomRecordValidator.IsComputed(CustomFieldType.Rollup));
        Assert.False(CustomRecordValidator.IsComputed(CustomFieldType.Text));
        Assert.False(CustomRecordValidator.IsComputed(CustomFieldType.Money));
    }

    [Fact]
    public void Formula_user_input_is_ignored()
    {
        var fields = new[]
        {
            Field("ht", CustomFieldType.Decimal, sort: 0),
            Field("ttc", CustomFieldType.Formula, sort: 1)
        };
        var result = CustomRecordValidator.ValidateAndCanonicalize(
            fields, Data(("ht", JsonValue.Create(100)), ("ttc", JsonValue.Create(999))));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("\"ttc\"", result.Value);
    }

    [Fact]
    public void Attachment_accepts_own_storage_url_and_rejects_foreign()
    {
        var tid = Guid.NewGuid();
        var fields = new[] { Field("doc", CustomFieldType.Attachment) };
        var ok = CustomRecordValidator.ValidateAndCanonicalize(
            fields, Data(("doc", JsonValue.Create($"/uploads/tenants/{tid}/studio/contacts/abc.png"))));
        var foreign = CustomRecordValidator.ValidateAndCanonicalize(
            fields, Data(("doc", JsonValue.Create("https://evil.example/x.png"))));
        Assert.True(ok.IsSuccess);
        Assert.True(foreign.IsFailure);
    }

    [Fact]
    public void IsStudioFileUrl_requires_tenant_studio_path()
    {
        Assert.True(CustomRecordValidator.IsStudioFileUrl("/uploads/tenants/abc/studio/contacts/x.png"));
        Assert.False(CustomRecordValidator.IsStudioFileUrl("/uploads/tenants/abc/products/x.png"));
        Assert.False(CustomRecordValidator.IsStudioFileUrl("http://evil/y"));
    }
}
