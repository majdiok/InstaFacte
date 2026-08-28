using FactuTrust.Application.Common.Logging;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Common;

/// <summary>
/// Phase 5 (hygiène des logs) — <see cref="LogSanitizer"/> couvre CWE-532 (fuite d'informations
/// sensibles dans les logs, via <see cref="LogSanitizer.MaskEmail"/>) et CWE-117 (injection de
/// logs par CR/LF, via <see cref="LogSanitizer.Sanitize"/>).
/// </summary>
public sealed class LogSanitizerTests
{
    [Theory]
    [InlineData("john@x.com", "j***@x.com")]
    [InlineData("a@b.io", "a***@b.io")]
    [InlineData("JOHN.DOE@Example.COM", "J***@Example.COM")]
    public void MaskEmail_masks_local_part_and_keeps_domain(string input, string expected)
    {
        Assert.Equal(expected, LogSanitizer.MaskEmail(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmail_handles_null_or_empty_safely(string? input)
    {
        var result = LogSanitizer.MaskEmail(input);

        Assert.Equal("(empty)", result);
        Assert.DoesNotContain("@", result);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@missing-local-part.com")]
    public void MaskEmail_masks_entirely_when_no_usable_at_sign(string input)
    {
        var result = LogSanitizer.MaskEmail(input);

        Assert.Equal("***", result);
        Assert.DoesNotContain(input, result, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskEmail_never_returns_the_plaintext_local_part()
    {
        const string email = "sensitive.user@tenant.example.com";
        var result = LogSanitizer.MaskEmail(email);

        Assert.DoesNotContain("sensitive.user", result, StringComparison.Ordinal);
        Assert.StartsWith("s***@", result, StringComparison.Ordinal);
        Assert.EndsWith("tenant.example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_strips_carriage_returns_and_newlines()
    {
        var withCrlf = "legit-reason\r\nINFO Fake admin login succeeded\r\nanother line";

        var sanitized = LogSanitizer.Sanitize(withCrlf);

        Assert.DoesNotContain("\r", sanitized);
        Assert.DoesNotContain("\n", sanitized);
        Assert.Equal("legit-reasonINFO Fake admin login succeededanother line", sanitized);
    }

    [Fact]
    public void Sanitize_strips_bare_lf_only_input()
    {
        var sanitized = LogSanitizer.Sanitize("line1\nline2\nline3");

        Assert.Equal("line1line2line3", sanitized);
    }

    [Fact]
    public void Sanitize_truncates_long_values_to_default_max_length()
    {
        var longValue = new string('a', 500);

        var sanitized = LogSanitizer.Sanitize(longValue);

        Assert.True(sanitized.Length <= LogSanitizer.DefaultMaxLength + 1, "truncated value plus ellipsis marker should stay close to the max length");
        Assert.EndsWith("…", sanitized, StringComparison.Ordinal);
        Assert.StartsWith(new string('a', LogSanitizer.DefaultMaxLength), sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_respects_custom_max_length()
    {
        var sanitized = LogSanitizer.Sanitize("abcdefghij", maxLength: 5);

        Assert.Equal("abcde…", sanitized);
    }

    [Fact]
    public void Sanitize_does_not_truncate_values_under_the_limit()
    {
        const string value = "short-value";

        var sanitized = LogSanitizer.Sanitize(value);

        Assert.Equal(value, sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_handles_null_or_empty_input(string? input)
    {
        Assert.Equal(string.Empty, LogSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_combines_crlf_stripping_and_truncation()
    {
        var malicious = "ok\r\n" + new string('x', 300);

        var sanitized = LogSanitizer.Sanitize(malicious);

        Assert.DoesNotContain("\r", sanitized);
        Assert.DoesNotContain("\n", sanitized);
        Assert.EndsWith("…", sanitized, StringComparison.Ordinal);
    }
}
