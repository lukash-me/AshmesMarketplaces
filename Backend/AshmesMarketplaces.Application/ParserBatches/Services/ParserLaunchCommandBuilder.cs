using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed record ParserLaunchCommandOptions(
    string PythonExecutable,
    string SupervisorScriptPath,
    string ConfigPath,
    string Mode,
    string OutboxRoot,
    string WorkingDirectory,
    string BatchQueueUrl,
    string OutputBaseDir,
    string RateLimitStateDir);

public sealed record ParserLaunchCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);

public static class ParserLaunchCommandBuilder
{
    public static ParserLaunchCommand Build(
        ParserLaunchRequest request,
        ParserLaunchCommandOptions options,
        string? launchContextPath = null)
    {
        var arguments = new List<string>
        {
            options.SupervisorScriptPath,
            "--config",
            options.ConfigPath,
            "--mode",
            options.Mode,
            "--parser-instance-id",
            request.ParserInstanceId,
            "--outbox-root",
            options.OutboxRoot
        };

        if (!string.IsNullOrWhiteSpace(launchContextPath))
        {
            arguments.Add("--launch-context");
            arguments.Add(launchContextPath);
        }

        if (request.LaunchMode is ParserLaunchModes.LimitedAll or ParserLaunchModes.CheckProxy &&
            request.BatchLimit is > 0)
        {
            arguments.Add("--smoke-max-batches");
            arguments.Add(request.BatchLimit.Value.ToString());
        }

        if (request.LaunchMode == ParserLaunchModes.CheckProxy && !string.IsNullOrWhiteSpace(request.ProxyKey))
        {
            arguments.Add("--only-proxy");
            arguments.Add(request.ProxyKey);
        }

        var environment = new Dictionary<string, string>
        {
            ["PARSER_INSTANCE_ID"] = request.ParserInstanceId,
            ["PARSER_CYCLE_ID"] = request.ParserCycleId,
            ["PARSER_SESSION_CACHE_MODE"] = "run",
            ["PARSER_FORCE_REFRESH_TOKEN"] = "1",
            ["PARSER_BATCH_QUEUE_URL"] = options.BatchQueueUrl,
            ["PARSER_OUTPUT_BASE_DIR"] = options.OutputBaseDir,
            ["PARSER_RATE_LIMIT_STATE_DIR"] = options.RateLimitStateDir
        };
        if (!string.IsNullOrWhiteSpace(launchContextPath))
            environment["PARSER_LAUNCH_CONTEXT_FILE"] = launchContextPath;

        return new ParserLaunchCommand(
            options.PythonExecutable,
            arguments,
            options.WorkingDirectory,
            environment);
    }
}
