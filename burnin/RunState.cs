namespace KubeMQ.Burnin;

public enum RunState
{
    Idle = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Error = 5
}

public enum PatternState
{
    Starting = 0,
    Running = 1,
    Recovering = 2,
    Error = 3,
    Stopped = 4
}

public static class StateExtensions
{
    public static string ToApi(this RunState s) => s switch
    {
        RunState.Idle => "idle",
        RunState.Starting => "starting",
        RunState.Running => "running",
        RunState.Stopping => "stopping",
        RunState.Stopped => "stopped",
        RunState.Error => "error",
        _ => "unknown"
    };

    public static string ToApi(this PatternState s) => s switch
    {
        PatternState.Starting => "starting",
        PatternState.Running => "running",
        PatternState.Recovering => "recovering",
        PatternState.Error => "error",
        PatternState.Stopped => "stopped",
        _ => "unknown"
    };

    public static bool CanStart(this RunState s) =>
        s is RunState.Idle or RunState.Stopped or RunState.Error;

    public static bool CanStop(this RunState s) =>
        s is RunState.Starting or RunState.Running;
}
