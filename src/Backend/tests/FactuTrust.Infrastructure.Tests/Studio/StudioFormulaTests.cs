using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioFormulaTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    private static CustomFieldDefinition Plain(string key) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Decimal, false, false, 0, null, null, null, null);

    private static CustomFieldDefinition Formula(string key, string expr) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Formula, false, false, 0, null, StudioFormula.Serialize(expr), null, null);

    [Fact]
    public void Valid_formula_over_existing_fields_passes()
    {
        var siblings = new[] { Plain("ht") };
        var error = StudioFormula.Validate("ttc", "ht * 1.19", siblings);
        Assert.Null(error);
    }

    [Fact]
    public void Unknown_reference_is_rejected()
    {
        var siblings = new[] { Plain("ht") };
        var error = StudioFormula.Validate("ttc", "ht * tva", siblings);
        Assert.NotNull(error);
    }

    [Fact]
    public void Self_reference_is_rejected()
    {
        var siblings = new[] { Plain("ht") };
        var error = StudioFormula.Validate("ttc", "ttc + 1", siblings);
        Assert.NotNull(error);
    }

    [Fact]
    public void Direct_cycle_between_formulas_is_rejected()
    {
        // Existing: a = b + 1 (formula). Now defining b = a + 1 → a→b→a cycle.
        var siblings = new[] { Formula("a", "b + 1"), Plain("b") };
        var error = StudioFormula.Validate("b", "a + 1", siblings);
        Assert.NotNull(error);
    }

    [Fact]
    public void Acyclic_formula_chain_passes()
    {
        // tva = ht * 0.19 ; ttc = ht + tva  → no cycle.
        var siblings = new[] { Plain("ht"), Formula("tva", "ht * 0.19") };
        var error = StudioFormula.Validate("ttc", "ht + tva", siblings);
        Assert.Null(error);
    }

    [Fact]
    public void Syntax_error_is_rejected()
    {
        var error = StudioFormula.Validate("ttc", "ht *", new[] { Plain("ht") });
        Assert.NotNull(error);
    }
}
