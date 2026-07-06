using System.Diagnostics;
using System.Text;
using AshmesMarketplaces.Application.ParserBatches.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.AnalyticsWorker.Background;

public sealed class ParserLaunchHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ParserLaunchHostedService> _logger;

    public ParserLaunchHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ParserLaunchHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOneAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Parser launch worker tick failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var launchService = scope.ServiceProvider.GetRequiredService<IParserLaunchRequestService>();
        var launch = await launchService.ClaimNextAsync(cancellationToken);
        if (launch is null)
            return false;

        var command = ParserLaunchCommandBuilder.Build(launch, BuildOptions());
        _logger.LogInformation(
            "Parser launch started. launchId={LaunchId} parserInstanceId={ParserInstanceId} mode={Mode} proxyKey={ProxyKey} batchLimit={BatchLimit}",
            launch.Id,
            launch.ParserInstanceId,
            launch.LaunchMode,
            launch.ProxyKey,
            launch.BatchLimit);

        try
        {
            var result = await RunProcessAsync(command, cancellationToken);
            if (result.ExitCode == 0)
            {
                await launchService.MarkCompletedAsync(launch.Id, cancellationToken);
                _logger.LogInformation(
                    "Parser launch completed. launchId={LaunchId} stdout={Stdout}",
                    launch.Id,
                    TrimForLog(result.Stdout));
            }
            else
            {
                var error = string.IsNullOrWhiteSpace(result.Stderr)
                    ? $"Parser supervisor exited with code {result.ExitCode}."
                    : result.Stderr;
                await launchService.MarkFailedAsync(launch.Id, error, cancellationToken);
                _logger.LogError(
                    "Parser launch failed. launchId={LaunchId} exitCode={ExitCode} stderr={Stderr}",
                    launch.Id,
                    result.ExitCode,
                    TrimForLog(result.Stderr));
            }
        }
        catch (Exception exception)
        {
            await launchService.MarkFailedAsync(launch.Id, exception.Message, cancellationToken);
            _logger.LogError(exception, "Parser launch crashed. launchId={LaunchId}", launch.Id);
        }

        return true;
    }

    private ParserLaunchCommandOptions BuildOptions()
    {
        var workingDirectory = _configuration["ParserLaunch:WorkingDirectory"]
            ?? FindRepositoryRoot();
        return new ParserLaunchCommandOptions(
            ResolvePythonExecutable(_configuration["ParserLaunch:PythonExecutable"], workingDirectory),
            ResolveFromWorkingDirectory(
                _configuration["ParserLaunch:SupervisorScriptPath"],
                workingDirectory,
                Path.Combine("Parser", "app", "proxy_supervisor.py")),
            ResolveFromWorkingDirectory(
                _configuration["ParserLaunch:ConfigPath"],
                workingDirectory,
                Path.Combine("Parser", "presets", "production", "market_refresh_selected_niches_batched.prod.json")),
            _configuration["ParserLaunch:Mode"] ?? "batched_full_enrichment",
            ResolveFromWorkingDirectory(
                _configuration["ParserLaunch:OutboxRoot"],
                workingDirectory,
                Path.Combine("Parser", "output", "outbox")),
            workingDirectory);
    }

    private static string ResolvePythonExecutable(string? configuredExecutable, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredExecutable))
            return ResolveFromWorkingDirectory(configuredExecutable, workingDirectory, configuredExecutable);

        var parserVenvPython = Path.Combine(workingDirectory, "Parser", ".venv", "Scripts", "python.exe");
        return File.Exists(parserVenvPython) ? parserVenvPython : "python";
    }

    private static string ResolveFromWorkingDirectory(string? configuredPath, string workingDirectory, string fallbackRelativePath)
    {
        var path = configuredPath ?? fallbackRelativePath;
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path, workingDirectory);
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Parser", "app", "proxy_supervisor.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "Backend", "AshmesMarketplaces.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static async Task<ParserProcessResult> RunProcessAsync(
        ParserLaunchCommand command,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var item in command.Environment)
        {
            startInfo.Environment[item.Key] = item.Value;
        }
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start parser supervisor process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ParserProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static string TrimForLog(string value)
    {
        const int maxLength = 4000;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record ParserProcessResult(int ExitCode, string Stdout, string Stderr);
}
