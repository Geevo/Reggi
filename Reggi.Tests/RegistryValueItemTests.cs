using Reggi.Core;
using Xunit;

namespace Reggi.Tests;

public sealed class RegistryValueItemTests
{
    private static RegistryValueItem Named(string name) => new()
    {
        Name = name,
        Kind = RegistryValueType.String,
        RawValue = "data"
    };

    [Fact]
    public void DefaultValueIsShownUnderItsLabel()
    {
        var item = Named(string.Empty);

        Assert.True(item.IsDefault);
        Assert.Equal("(Default)", item.DisplayName);
    }

    [Fact]
    public void ValueLiterallyNamedDefaultIsDistinguishableFromTheRealDefault()
    {
        var real = Named(string.Empty);
        var impostor = Named("(Default)");

        Assert.False(impostor.IsDefault);
        Assert.NotEqual(real.DisplayName, impostor.DisplayName);
    }

    [Theory]
    [InlineData("Path")]
    [InlineData(@"has\backslash")]
    [InlineData("(default)")]      // different case, so no collision to disambiguate
    [InlineData(" spaced ")]
    public void OrdinaryNamesAreShownVerbatim(string name) =>
        Assert.Equal(name, Named(name).DisplayName);

    [Fact]
    public void UnsetDefaultReportsNoData()
    {
        var item = RegistryValueItem.UnsetDefault();

        Assert.True(item.IsDefault);
        Assert.True(item.IsUnset);
        Assert.Equal("(value not set)", item.DataText);
    }
}
