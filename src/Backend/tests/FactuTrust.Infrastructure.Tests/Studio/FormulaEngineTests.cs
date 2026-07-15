using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class FormulaEngineTests
{
    private static object? Eval(string expr, params (string Key, object? Value)[] values)
    {
        var dict = values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Assert.True(FormulaEngine.TryEvaluate(expr, dict, out var result), $"evaluation failed for: {expr}");
        return result;
    }

    [Fact]
    public void Arithmetic_precedence()
    {
        Assert.Equal(14m, Eval("2 + 3 * 4"));
        Assert.Equal(20m, Eval("(2 + 3) * 4"));
        Assert.Equal(2m, Eval("10 % 4"));
    }

    [Fact]
    public void Parameters_are_substituted()
    {
        Assert.Equal(119m, Eval("montant_ht * 1.19", ("montant_ht", 100m)));
    }

    [Fact]
    public void Round_uses_away_from_zero()
    {
        Assert.Equal(2.56m, Eval("ROUND(2.555, 2)"));
        Assert.Equal(3m, Eval("ROUND(2.5)"));
    }

    [Fact]
    public void If_branches()
    {
        Assert.Equal("pos", Eval("IF(x > 0, 'pos', 'neg')", ("x", 5m)));
        Assert.Equal("neg", Eval("IF(x > 0, 'pos', 'neg')", ("x", -5m)));
    }

    [Fact]
    public void String_concat_via_plus_and_concat()
    {
        Assert.Equal("ab", Eval("'a' + 'b'"));
        Assert.Equal("a1", Eval("CONCAT('a', 1)"));
    }

    [Fact]
    public void Coalesce_returns_first_non_null()
    {
        Assert.Equal(5m, Eval("COALESCE(missing, 5)", ("missing", null)));
    }

    [Fact]
    public void Logical_and_comparisons()
    {
        Assert.Equal(true, Eval("x > 0 && y < 10", ("x", 1m), ("y", 5m)));
        Assert.Equal(false, Eval("x > 0 AND y < 10", ("x", 1m), ("y", 50m)));
        Assert.Equal(true, Eval("NOT (x == y)", ("x", 1m), ("y", 2m)));
    }

    [Fact]
    public void Min_max_abs()
    {
        Assert.Equal(3m, Eval("MIN(3, 7, 5)"));
        Assert.Equal(7m, Eval("MAX(3, 7, 5)"));
        Assert.Equal(4m, Eval("ABS(0 - 4)"));
    }

    [Fact]
    public void Division_by_zero_fails_gracefully()
    {
        Assert.False(FormulaEngine.TryEvaluate("1 / 0", new Dictionary<string, object?>(), out _));
    }

    [Fact]
    public void Null_parameter_in_arithmetic_fails_gracefully()
    {
        Assert.False(FormulaEngine.TryEvaluate("a + 1", new Dictionary<string, object?> { ["a"] = null }, out _));
    }

    [Fact]
    public void Unknown_function_is_rejected()
    {
        var (ok, error, _) = FormulaEngine.Validate("FOO(1)");
        Assert.False(ok);
        Assert.Contains("FOO", error);
    }

    [Fact]
    public void Whitelist_blocks_arbitrary_calls()
    {
        Assert.False(FormulaEngine.Validate("SYSTEM('rm')").Ok);
        Assert.False(FormulaEngine.Validate("EVAL('x')").Ok);
    }

    [Fact]
    public void Validate_collects_distinct_references()
    {
        var (ok, _, refs) = FormulaEngine.Validate("a + b * a + ROUND(c, 2)");
        Assert.True(ok);
        Assert.Equal(new[] { "a", "b", "c" }, refs.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Empty_or_overlong_expression_is_invalid()
    {
        Assert.False(FormulaEngine.Validate("").Ok);
        Assert.False(FormulaEngine.Validate(new string('1', FormulaEngine.MaxLength + 1)).Ok);
    }

    [Fact]
    public void Malformed_expression_is_invalid()
    {
        Assert.False(FormulaEngine.Validate("1 +").Ok);
        Assert.False(FormulaEngine.Validate("(1 + 2").Ok);
        Assert.False(FormulaEngine.Validate("1 2").Ok);
    }
}
