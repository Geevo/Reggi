using RegTerm.Core;
using RegTerm.Registry;
using Xunit;

namespace RegTerm.Tests;

/// <summary>
/// Integration tests using a temporary key under HKEY_CURRENT_USER\Software.
/// </summary>
public sealed class RegistryNameRuleTests : IDisposable
{
    private readonly RegistryService registry = new();
    private readonly RegistryPath parent = new("HKEY_CURRENT_USER", "Software");
    private readonly string keyName = "RegTermTests_" + Guid.NewGuid().ToString("N");
    private readonly RegistryPath key;

    public RegistryNameRuleTests()
    {
        key = parent.Child(keyName);
        registry.CreateSubKey(parent, keyName);
    }

    public void Dispose()
    {
        try { registry.DeleteSubKeyTree(parent, keyName); }
        catch (Exception) { /* best-effort cleanup */ }
    }

    [Fact]
    public void ValueNameMayContainABackslash()
    {
        registry.CreateValue(key, @"has\backslash", "data", RegistryValueType.String);

        Assert.Contains(registry.GetValues(key).Values, v => v.Name == @"has\backslash");
    }

    [Fact]
    public void ValueCreatedWithABackslashCanAlsoBeRenamedToOne()
    {
        registry.CreateValue(key, @"first\name", "data", RegistryValueType.String);

        registry.RenameValue(key, @"first\name", @"second\name");

        var values = registry.GetValues(key).Values;
        Assert.Contains(values, v => v.Name == @"second\name");
        Assert.DoesNotContain(values, v => v.Name == @"first\name");
    }

    [Fact]
    public void KeyNameMayNotContainABackslash()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => registry.CreateSubKey(key, @"a\b"));

        Assert.Contains("backslash", error.Message);
    }

    [Fact]
    public void RenamingAKeyToANameWithABackslashIsRefused()
    {
        registry.CreateSubKey(key, "Child");

        var error = Assert.Throws<InvalidOperationException>(
            () => registry.RenameSubKey(key.Child("Child"), @"a\b"));

        Assert.Contains("backslash", error.Message);
        Assert.Contains(registry.GetSubKeyNames(key), n => n == "Child");
    }

    [Fact]
    public void RenamingAValueToNothingIsRefused()
    {
        registry.CreateValue(key, "Named", "data", RegistryValueType.String);

        Assert.Throws<InvalidOperationException>(
            () => registry.RenameValue(key, "Named", "   "));
    }

    [Fact]
    public void CreatingADuplicateValueIsRefusedAndLeavesTheOriginal()
    {
        registry.CreateValue(key, "Dupe", "original", RegistryValueType.String);

        Assert.Throws<InvalidOperationException>(
            () => registry.CreateValue(key, "Dupe", "replacement", RegistryValueType.String));

        var value = registry.GetValues(key).Values.Single(v => v.Name == "Dupe");
        Assert.Equal("original", value.RawValue);
    }

    [Fact]
    public void CreatingADuplicateKeyIsRefused()
    {
        registry.CreateSubKey(key, "Once");

        Assert.Throws<InvalidOperationException>(() => registry.CreateSubKey(key, "Once"));
    }

    [Fact]
    public void AValueLiterallyNamedDefaultCoexistsWithTheRealDefault()
    {
        registry.SetValue(key, string.Empty, "the real default", RegistryValueType.String);
        registry.CreateValue(key, "(Default)", "an ordinary value", RegistryValueType.String);

        var values = registry.GetValues(key).Values;
        var real = values.Single(v => v.IsDefault);
        var impostor = values.Single(v => v.Name == "(Default)");

        Assert.Equal("the real default", real.RawValue);
        Assert.Equal("an ordinary value", impostor.RawValue);
        Assert.NotEqual(real.DisplayName, impostor.DisplayName);
    }
}
