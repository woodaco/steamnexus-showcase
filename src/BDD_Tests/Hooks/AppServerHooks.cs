using System.Diagnostics;
using TechTalk.SpecFlow;

namespace BDD_Tests.Hooks
{
    [Binding]
    public class AppServerHooks
    {
        private static Process? _appProcess;
        private static readonly string AppPath = ResolveAppPath();

        private static string ResolveAppPath()
        {
            // Primary: walk up from the test assembly's bin directory to find SteamProject.
            // This works both locally and when tests are built in a standard layout (e.g. nightshift).
            var candidate = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "SteamProject"));
            if (Directory.Exists(candidate)) return candidate;

            // Fallback: an explicit override, for build layouts where the walk-up misses.
            var overridePath = Environment.GetEnvironmentVariable("STEAMPROJECT_APP_PATH");
            if (!string.IsNullOrEmpty(overridePath) && Directory.Exists(overridePath)) return overridePath;

            throw new InvalidOperationException(
                $"SteamProject not found. Tried the base-dir walk and STEAMPROJECT_APP_PATH={overridePath}.");
        }

        [BeforeTestRun(Order = 0)]
        public static void StartApp()
        {
            // Skip if running in CI - app should be deployed already
            if (Environment.GetEnvironmentVariable("CI") == "true" ||
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            {
                Console.WriteLine("Running in CI - skipping local app startup");
                return;
            }

            Console.WriteLine($"Starting app from: {AppPath}");

            // Use the dotnet host directly rather than whatever "dotnet" resolves to on PATH,
            // so a wrapper script on PATH cannot redirect the run. DOTNET_HOST_PATH overrides it.
            var dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "/usr/share/dotnet/dotnet";

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = "run --urls=https://localhost:7123",
                WorkingDirectory = AppPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _appProcess = new Process { StartInfo = startInfo };
            _appProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[APP] {e.Data}"); };
            _appProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[APP ERROR] {e.Data}"); };

            _appProcess.Start();
            _appProcess.BeginOutputReadLine();
            _appProcess.BeginErrorReadLine();

            // Wait for the app to start
            WaitForAppToStart("https://localhost:7123", TimeSpan.FromSeconds(60));
        }

        [AfterTestRun(Order = 1000)]
        public static void StopApp()
        {
            if (_appProcess != null && !_appProcess.HasExited)
            {
                Console.WriteLine("Stopping app...");
                // AfterTestRun fires once, after every scenario in the run (SpecFlow.NUnit maps
                // it to the assembly-level teardown), so nothing still needs the server here.
                // Dispose() alone never stopped it: `dotnet run` and the SteamProject child it
                // spawns were orphaned and kept :7123 bound, so the next run's StartApp
                // "found" a stale server built from older code. Kill the whole tree.
                try
                {
                    _appProcess.Kill(entireProcessTree: true);
                    _appProcess.WaitForExit(10_000);
                }
                catch (InvalidOperationException)
                {
                    // Already exited between the HasExited check and Kill.
                }
                _appProcess.Dispose();
                _appProcess = null;
            }
        }

        private static void WaitForAppToStart(string url, TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using var client = new HttpClient(handler);
            // Read this module's deadline from a process-local monotonic clock instead of the
            // global wall clock. The previous arithmetic (deadline = DateTime.Now + timeout;
            // while DateTime.Now < deadline) tracked the REAL clock, so the wait went off
            // whenever the wall clock was step-adjusted under load. A monotonic Stopwatch keeps
            // the "wait up to `timeout`" property off the real clock without loosening the bound.
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine($"Waiting for app at {url}...");

            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect)
                    {
                        Console.WriteLine("App is ready!");
                        return;
                    }
                }
                catch
                {
                    // App not ready yet
                }

                Thread.Sleep(1000);
            }

            throw new Exception($"App failed to start within {timeout.TotalSeconds} seconds");
        }
    }
}
