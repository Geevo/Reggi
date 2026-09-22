using Reggi.Registry;
using Reggi.UI;

var useNetDriver = args.Contains("--net-driver", StringComparer.OrdinalIgnoreCase);
var traceStartup = args.Contains("--trace-startup", StringComparer.OrdinalIgnoreCase);
var tracePath = Path.Combine(Path.GetTempPath(), "reggi-startup.log");

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

try
{
    new RegistryApplication(new RegistryService()).Run(useNetDriver, Trace);
    Trace("Application exited normally");
}
catch (Exception ex)
{
    Trace($"Startup failed: {ex}");
    throw;
}
