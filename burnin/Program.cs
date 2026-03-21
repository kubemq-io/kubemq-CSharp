// CLI entry point for the KubeMQ C# burn-in test.
// Boot sequence per spec §2: HTTP server first, app starts in Idle state.
// Supports --config, --validate-config, --cleanup-only.
// Exit codes: 0=PASSED/idle, 1=FAILED, 2=config error.

using KubeMQ.Burnin;

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"unobserved task exception: {e.Exception.Message}");
    e.SetObserved();
};

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"unhandled exception: {e.ExceptionObject}");
    if (e.IsTerminating)
        Environment.Exit(2);
};

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    // P1: Ensure enough threadpool threads for high-concurrency burn-in workloads
    System.Threading.ThreadPool.SetMinThreads(64, 64);
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

    // Load startup config (YAML/env)
    var loadResult = Config.LoadConfig(configPath);
    var cfg = loadResult.Config;
    foreach (string w in loadResult.Warnings)
        Console.Error.WriteLine($"WARNING: {w}");

    // Validate config
    var (validationErrors, validationWarnings) = Config.ValidateConfig(cfg);
    foreach (string w in validationWarnings)
        Console.Error.WriteLine($"WARNING: {w}");
    if (validationErrors.Count > 0)
    {
        foreach (string e in validationErrors)
            Console.Error.WriteLine($"config error: {e}");
        return 2;
    }

    // --validate-config mode (no HTTP server)
    if (validateConfig)
    {
        Console.WriteLine("config validation passed");
        Console.WriteLine($"mode={cfg.Mode} duration={cfg.Duration} broker={cfg.Broker.Address}");
        return 0;
    }

    // --cleanup-only mode (no HTTP server)
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

    // === API-controlled lifecycle (spec §2) ===

    // Step 1: Create Engine (enters Idle state)
    using var engine = new Engine(cfg);

    // Step 2: Pre-initialize Prometheus metrics (§8.3)
    Metrics.PreInitialize();

    // Step 3: Start HTTP server FIRST (§2 step 2)
    string corsOrigins = cfg.Cors.Origins;
    var httpServer = new BurninHttpServer(cfg.Metrics.Port, engine, corsOrigins);
    await httpServer.StartAsync();
    Console.WriteLine($"HTTP server started on port {cfg.Metrics.Port} (state=idle)");
    Console.WriteLine($"broker address: {cfg.Broker.Address}");
    Console.WriteLine($"CORS origins: {corsOrigins}");

    // Step 4: SIGTERM/SIGINT handling
    using var shutdownCts = new CancellationTokenSource();
    var shutdownTcs = new TaskCompletionSource();

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Console.WriteLine("received SIGINT, shutting down");
        shutdownCts.Cancel();
        shutdownTcs.TrySetResult();
    };

    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        Console.WriteLine("received SIGTERM, shutting down");
        shutdownCts.Cancel();
        shutdownTcs.TrySetResult();
    };

    // Step 5: Wait for signal (app is idle, waiting for API commands)
    Console.WriteLine("ready — waiting for API commands (POST /run/start)");
    await shutdownTcs.Task;

    // Step 6: Graceful shutdown
    int exitCode = await engine.GracefulShutdownAsync();

    // Step 7: Stop HTTP server
    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await httpServer.StopAsync(stopCts.Token);
    await httpServer.DisposeAsync();

    Console.WriteLine("burn-in app exiting");
    return exitCode;
}
