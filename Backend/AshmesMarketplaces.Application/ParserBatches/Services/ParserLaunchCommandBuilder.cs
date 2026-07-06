using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed record ParserLaunchCommandOptions(
    string PythonExecutable,
    string SupervisorScriptPath,
    string ConfigPath,
    string Mode,
    string OutboxRoot,
    string WorkingDirectory);

public sealed record ParserLaunchCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);

public static class ParserLaunchCommandBuilder
{
    public static ParserLaunchCommand Build(ParserLaunchRequest request, ParserLaunchCommandOptions options)
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

        return new ParserLaunchCommand(
            options.PythonExecutable,
            arguments,
            options.WorkingDirectory,
            new Dictionary<string, string>
            {
                ["PARSER_INSTANCE_ID"] = request.ParserInstanceId,
                ["PARSER_CYCLE_ID"] = request.ParserCycleId,
                ["PARSER_SESSION_CACHE_MODE"] = "run",
                ["PARSER_FORCE_REFRESH_TOKEN"] = "1"
            });
    }
}
