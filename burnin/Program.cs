// CLI entry point for the KubeMQ C# burn-in test.
// Supports --config, --validate-config, --cleanup-only.
// Exit codes: 0=PASSED, 1=FAILED, 2=config error.

using KubeMQ.Burnin;

// Global unobserved task exception handler (log, don't terminate)
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"unobserved task exception: {e.Exception.Message}");
    e.SetObserved(); // prevent process termination
};

// Global unhandled exception handler
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"unhandled exception: {e.ExceptionObject}");
    if (e.IsTerminating)
    {
        Environment.Exit(2);
    }
};

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    // Parse CLI arguments
    string configPath = "";
    bool validateConfig = false;
    bool cleanupOnly = false;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config" or "-c":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            case "--validate-config":
                validateConfig = true;
                break;
            case "--cleanup-only":
                cleanupOnly = true;
                break;
        }
    }

    // Load config
    var loadResult = Config.LoadConfig(configPath);
    var cfg = loadResult.Config;
    foreach (string w in loadResult.Warnings)
        Console.Error.WriteLine($"WARNING: {w}");

    // Validate config
    var errors = Config.ValidateConfig(cfg);
    bool hasErrors = false;
    foreach (string e in errors)
    {
        if (e.StartsWith("WARNING", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(e);
        }
        else
        {
            Console.Error.WriteLine($"config error: {e}");
            hasErrors = true;
        }
    }

    if (hasErrors)
        return 2;

    // --validate-config mode
    if (validateConfig)
    {
        Console.WriteLine("config validation passed");
        Console.WriteLine(
            $"mode={cfg.Mode} duration={cfg.Duration} broker={cfg.Broker.Address} run_id={cfg.RunId}");
        return 0;
    }

    // --cleanup-only mode
    if (cleanupOnly)
    {
        Console.WriteLine("running cleanup-only mode");
        try
        {
            await CleanupRunner.RunAsync(cfg);
            Console.WriteLine("cleanup complete");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"cleanup failed: {ex}");
            return 1;
        }
        return 0;
    }

    // Signal handling: ManualResetEventSlim signaled from Console.CancelKeyPress and AppDomain.ProcessExit
    using var shutdownCts = new CancellationTokenSource();
    var shutdownSignal = new ManualResetEventSlim(false);

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // prevent immediate termination
        Console.WriteLine("received SIGINT, shutting down");
        shutdownCts.Cancel();
        shutdownSignal.Set();
    };

    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        Console.WriteLine("received SIGTERM, shutting down");
        shutdownCts.Cancel();
        shutdownSignal.Set();
    };

    // Run engine
    using var engine = new Engine(cfg);
    bool passed;

    try
    {
        // Run the engine (blocks until duration expires or signal received)
        await engine.RunAsync(shutdownCts.Token);
    }
    catch (OperationCanceledException)
    {
        // normal shutdown via signal
    }
    catch (Exception ex)
    {
        if (!shutdownCts.IsCancellationRequested)
        {
            Console.Error.WriteLine($"engine run failed: {ex}");
        }
    }

    // Execute 2-phase shutdown sequence
    passed = await engine.ShutdownAsync();
    Console.WriteLine("burn-in test complete");

    return passed ? 0 : 1;
}
