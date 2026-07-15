using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioAutoNumberTests
{
    [Fact]
    public void Default_config_pads_to_four()
    {
        Assert.Equal("0001", StudioAutoNumber.Format(1, null));
        Assert.Equal("0042", StudioAutoNumber.Format(42, "{}"));
    }

    [Fact]
    public void Prefix_padding_suffix_applied()
    {
        var json = """{"number":{"prefix":"FACT-","padding":5,"suffix":"/2026"}}""";
        Assert.Equal("FACT-00007/2026", StudioAutoNumber.Format(7, json));
    }

    [Fact]
    public void Zero_padding_leaves_number_bare()
    {
        var json = """{"number":{"prefix":"N","padding":0,"suffix":""}}""";
        Assert.Equal("N123", StudioAutoNumber.Format(123, json));
    }

    [Fact]
    public void Padding_is_clamped_and_value_longer_than_padding_is_untruncated()
    {
        var json = """{"number":{"prefix":"","padding":2,"suffix":""}}""";
        Assert.Equal("123456", StudioAutoNumber.Format(123456, json)); // no truncation
    }

    [Fact]
    public void Corrupt_config_falls_back_to_defaults()
    {
        Assert.Equal("0005", StudioAutoNumber.Format(5, "not json at all"));
    }

    [Fact]
    public void ParseConfig_clamps_padding_and_affixes()
    {
        var json = """{"number":{"prefix":"01234567890123456789","padding":99,"suffix":"ABCDEFGHIJKLMNOPQRST"}}""";
        var (prefix, padding, suffix) = StudioAutoNumber.ParseConfig(json);
        Assert.Equal(StudioAutoNumber.MaxAffixLength, prefix.Length);
        Assert.Equal(StudioAutoNumber.MaxAffixLength, suffix.Length);
        Assert.Equal(StudioAutoNumber.MaxPadding, padding);
    }
}
