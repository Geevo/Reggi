using Reggi.Core;
using Reggi.Registry;
using Xunit;

namespace Reggi.Tests;

/// <summary>
/// Integration tests using a temporary key under HKEY_CURRENT_USER\Software.
/// </summary>
public sealed class RegistryNameRuleTests : IDisposable
{
    private readonly RegistryService _registry = new();
    private readonly RegistryPath _parent = new("HKEY_CURRENT_USER", "Software");
    private readonly string _keyName = "ReggiTests_" + Guid.NewGuid().ToString("N");
    private readonly RegistryPath _key;

    public RegistryNameRuleTests()
    {
        _key = _parent.Child(_keyName);
        _registry.CreateSubKey(_parent, _keyName);
    }

    public void Dispose()
    {
        try { _registry.DeleteSubKeyTree(_parent, _keyName); }
        catch (Exception) { /* best-effort cleanup */ }
    }

    [Fact]
    public void ValueNameMayContainABackslash()
    {
        _registry.CreateValue(_key, @"has\backslash", "data", RegistryValueType.String);

        Assert.Contains(_registry.GetValues(_key).Values, v => v.Name == @"has\backslash");
    }

    [Fact]
    public void ValueCreatedWithABackslashCanAlsoBeRenamedToOne()
    {
        _registry.CreateValue(_key, @"first\name", "data", RegistryValueType.String);

        _registry.RenameValue(_key, @"first\name", @"second\name");

        var values = _registry.GetValues(_key).Values;
        Assert.Contains(values, v => v.Name == @"second\name");
        Assert.DoesNotContain(values, v => v.Name == @"first\name");
    }

    [Fact]
    public void KeyNameMayNotContainABackslash()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _registry.CreateSubKey(_key, @"a\b"));

        Assert.Contains("backslash", error.Message);
    }

    [Fact]
    public void RenamingAKeyToANameWithABackslashIsRefused()
    {
        _registry.CreateSubKey(_key, "Child");

        var error = Assert.Throws<InvalidOperationException>(() =>
            _registry.RenameSubKey(_key.Child("Child"), @"a\b"));

        Assert.Contains("backslash", error.Message);
        Assert.Contains(_registry.GetSubKeyNames(_key), n => n == "Child");
    }

    [Fact]
    public void RenamingAValueToNothingIsRefused()
    {
        _registry.CreateValue(_key, "Named", "data", RegistryValueType.String);

        Assert.Throws<InvalidOperationException>(() => _registry.RenameValue(_key, "Named", "   "));
    }

    [Fact]
    public void CreatingADuplicateValueIsRefusedAndLeavesTheOriginal()
    {
        _registry.CreateValue(_key, "Dupe", "original", RegistryValueType.String);

        Assert.Throws<InvalidOperationException>(() =>
            _registry.CreateValue(_key, "Dupe", "replacement", RegistryValueType.String));

        var value = _registry.GetValues(_key).Values.Single(v => v.Name == "Dupe");
        Assert.Equal("original", value.RawValue);
    }

    [Fact]
    public void CreatingADuplicateKeyIsRefused()
    {
        _registry.CreateSubKey(_key, "Once");

        Assert.Throws<InvalidOperationException>(() => _registry.CreateSubKey(_key, "Once"));
    }

    [Fact]
    public void AValueLiterallyNamedDefaultCoexistsWithTheRealDefault()
    {
        _registry.SetValue(_key, string.Empty, "the real default", RegistryValueType.String);
        _registry.CreateValue(_key, "(Default)", "an ordinary value", RegistryValueType.String);

        var values = _registry.GetValues(_key).Values;
        var real = values.Single(v => v.IsDefault);
        var impostor = values.Single(v => v.Name == "(Default)");

        Assert.Equal("the real default", real.RawValue);
        Assert.Equal("an ordinary value", impostor.RawValue);
        Assert.NotEqual(real.DisplayName, impostor.DisplayName);
    }
}
