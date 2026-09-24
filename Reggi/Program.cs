using System.Text;
using Reggi.Registry;
using Reggi.UI;

var useVPro = args.Contains("--vpro", StringComparer.OrdinalIgnoreCase);
var useNetDriver = useVPro || args.Contains("--net-driver", StringComparer.OrdinalIgnoreCase);
var traceStartup = args.Contains("--trace-startup", StringComparer.OrdinalIgnoreCase);
var tracePath = Path.Combine(Path.GetTempPath(), "reggi-startup.log");
var previousInputEncoding = useVPro ? Console.InputEncoding : null;
var previousOutputEncoding = useVPro ? Console.OutputEncoding : null;

void Trace(string message)
{
    if (!traceStartup) return;
    File.AppendAllText(tracePath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
}

if (traceStartup)
{
    File.WriteAllText(tracePath, string.Empty);
    Console.Error.WriteLine($"Startup trace: {tracePath}");
    Trace($"Main entered; OS={Environment.OSVersion}; input redirected={Console.IsInputRedirected}; output redirected={Console.IsOutputRedirected}");
}
else
{
    // Prime the console with a normal write before Terminal.Gui takes over the screen.
    Console.Error.WriteLine();
}
Console.Error.Flush();

try
{
    if (useVPro)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
    }

    new RegistryApplication(new RegistryService()).Run(
        useNetDriver: useNetDriver, startupTrace: Trace, normalizeFunctionKeys: useVPro);
    Trace("Application exited normally");
}
catch (Exception ex)
{
    Trace($"Startup failed: {ex}");
    throw;
}
finally
{
    if (useVPro)
    {
        Console.InputEncoding = previousInputEncoding!;
        Console.OutputEncoding = previousOutputEncoding!;
    }
}
