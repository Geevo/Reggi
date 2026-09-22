using Reggi.Core;
using Reggi.Registry;
using Reggi.UI;
using Xunit;

namespace Reggi.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreHasNoUiOrWindowsRegistryDependency()
    {
        var references = ReferenceNames(typeof(IRegistryService).Assembly);

        Assert.DoesNotContain("Reggi.UI", references);
        Assert.DoesNotContain("Reggi.Registry", references);
        Assert.DoesNotContain("Terminal.Gui", references);
        Assert.DoesNotContain("Microsoft.Win32.Registry", references);
    }

    [Fact]
    public void UiDependsOnCoreButNotTheWindowsRegistryAdapter()
    {
        var references = ReferenceNames(typeof(RegistryApplication).Assembly);

        Assert.Contains("Reggi.Core", references);
        Assert.Contains("Terminal.Gui", references);
        Assert.DoesNotContain("Reggi.Registry", references);
        Assert.DoesNotContain("Microsoft.Win32.Registry", references);
    }

    [Fact]
    public void WindowsRegistryAdapterDoesNotDependOnUi()
    {
        var references = ReferenceNames(typeof(RegistryService).Assembly);

        Assert.Contains("Reggi.Core", references);
        Assert.DoesNotContain("Reggi.UI", references);
        Assert.DoesNotContain("Terminal.Gui", references);
    }

    private static HashSet<string> ReferenceNames(System.Reflection.Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToHashSet(StringComparer.Ordinal);
}
