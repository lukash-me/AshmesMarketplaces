using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.AnalyticsWorker.Background;

public sealed class ParserLaunchHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions LaunchContextJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

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

        _logger.LogInformation(
            "Parser launch started. launchId={LaunchId} parserInstanceId={ParserInstanceId} mode={Mode} proxyKey={ProxyKey} batchLimit={BatchLimit}",
            launch.Id,
            launch.ParserInstanceId,
            launch.LaunchMode,
            launch.ProxyKey,
            launch.BatchLimit);

        try
        {
            var options = BuildOptions();
            var launchContextPath = await WriteLaunchContextAsync(scope.ServiceProvider, launch, options, cancellationToken);
            var command = ParserLaunchCommandBuilder.Build(launch, options, launchContextPath);
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
        var configPath = ResolveFromWorkingDirectory(
            _configuration["ParserLaunch:ConfigPath"],
            workingDirectory,
            Path.Combine("Parser", "presets", "production", "market_refresh_selected_niches_batched.prod.json"));
        var mode = _configuration["ParserLaunch:Mode"] ?? "batched_full_enrichment";
        if (_configuration.GetValue("ParserLaunch:RequireServiceRuntime", false))
            ValidateServiceRuntimeConfig(configPath, mode);

        var outboxRoot = ResolveFromWorkingDirectory(
            _configuration["ParserLaunch:OutboxRoot"],
            workingDirectory,
            Path.Combine("Parser", "output", "outbox"));
        var outputBaseDir = ResolveFromWorkingDirectory(
            _configuration["ParserLaunch:OutputBaseDir"],
            workingDirectory,
            Directory.GetParent(outboxRoot)?.FullName ?? Path.Combine("Parser", "output"));
        var rateLimitStateDir = ResolveFromWorkingDirectory(
            _configuration["ParserLaunch:RateLimitStateDir"],
            workingDirectory,
            Path.Combine(outputBaseDir, "rate_limits"));
        var batchQueueUrl = _configuration["ParserLaunch:BatchQueueUrl"]
            ?? _configuration["PARSER_BATCH_QUEUE_URL"]
            ?? "http://localhost:5019/api/v1/parser";

        return new ParserLaunchCommandOptions(
            ResolvePythonExecutable(_configuration["ParserLaunch:PythonExecutable"], workingDirectory),
            ResolveFromWorkingDirectory(
                _configuration["ParserLaunch:SupervisorScriptPath"],
                workingDirectory,
                Path.Combine("Parser", "app", "proxy_supervisor.py")),
            configPath,
            mode,
            outboxRoot,
            workingDirectory,
            batchQueueUrl,
            outputBaseDir,
            rateLimitStateDir);
    }

    private static async Task<string> WriteLaunchContextAsync(
        IServiceProvider serviceProvider,
        ParserLaunchRequest launch,
        ParserLaunchCommandOptions options,
        CancellationToken cancellationToken)
    {
        var proxyManagementService = serviceProvider.GetRequiredService<IParserProxyManagementService>();
        var assignmentsResult = await proxyManagementService.GetRuntimeAssignmentsAsync(
            launch.ParserInstanceId,
            cancellationToken);
        if (!assignmentsResult.IsSuccess)
            throw new InvalidOperationException(assignmentsResult.Error?.Message ?? "Parser runtime assignments were not available.");

        var assignments = assignmentsResult.Value!;
        var selectedNiches = SelectLaunchNiches(launch, assignments.Niches);
        var selectedProxyKeys = selectedNiches.Select(x => x.ProxyKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedProxies = assignments.Proxies
            .Where(x => selectedProxyKeys.Contains(x.Key))
            .ToList();
        var missingProxyKeys = selectedProxyKeys
            .Except(selectedProxies.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingProxyKeys.Count > 0)
            throw new InvalidOperationException($"Launch context is missing proxy definitions: {string.Join(", ", missingProxyKeys)}.");

        var payload = new
        {
            schemaVersion = 1,
            launchId = launch.Id,
            parserCycleId = launch.ParserCycleId,
            parserInstanceId = launch.ParserInstanceId,
            launchMode = launch.LaunchMode,
            batchLimit = launch.BatchLimit,
            createdAtUtc = DateTime.UtcNow,
            defaultProxy = assignments.DefaultProxy,
            proxies = selectedProxies,
            niches = selectedNiches,
            rank = new
            {
                topN = 1000,
                pageSize = 100,
                sort = "popular"
            },
            queue = new
            {
                baseUrl = options.BatchQueueUrl
            },
            paths = new
            {
                outboxRoot = options.OutboxRoot,
                outputBaseDir = options.OutputBaseDir,
                rateLimitStateDir = options.RateLimitStateDir
            }
        };

        var path = Path.Combine(
            options.OutboxRoot,
            "_runtime",
            "launch_contexts",
            $"{SafeFileName(launch.ParserCycleId)}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(payload, LaunchContextJsonOptions),
            Utf8NoBom,
            cancellationToken);
        return path;
    }

    private static IReadOnlyList<ParserRuntimeNicheAssignmentDto> SelectLaunchNiches(
        ParserLaunchRequest launch,
        IReadOnlyList<ParserRuntimeNicheAssignmentDto> niches)
    {
        var launchable = niches
            .Where(x => x.Enabled)
            .ToList();
        if (launch.LaunchMode == ParserLaunchModes.CheckProxy)
        {
            launchable = launchable
                .Where(x => string.Equals(x.ProxyKey, launch.ProxyKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (launchable.Count == 0)
            throw new InvalidOperationException("Launch context has no launchable proxy/niche assignments.");

        return launchable;
    }

    private static void ValidateServiceRuntimeConfig(string configPath, string mode)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("modes", out var modes) ||
            !modes.TryGetProperty(mode, out var modeConfig))
        {
            throw new InvalidOperationException($"Parser service runtime mode '{mode}' was not found in config '{configPath}'.");
        }

        if (modeConfig.TryGetProperty("proxy_mapping_file", out _))
            throw new InvalidOperationException("Parser service runtime config must not contain proxy_mapping_file.");

        if (modeConfig.TryGetProperty("product", out var product) &&
            product.TryGetProperty("env", out var productEnv))
        {
            foreach (var forbiddenKey in new[]
                     {
                         "PARSER_PROXY_MAPPING_FILE",
                         "PARSER_EXPLICIT_NICHES_FILE"
                     })
            {
                if (productEnv.TryGetProperty(forbiddenKey, out _))
                    throw new InvalidOperationException($"Parser service runtime config must not contain {forbiddenKey}.");
            }

            if (productEnv.TryGetProperty("PARSER_BATCH_QUEUE_URL", out var queueUrl) &&
                queueUrl.ValueKind == JsonValueKind.String &&
                (queueUrl.GetString() ?? string.Empty).Contains("localhost:5019", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Parser service runtime config must not contain localhost batch queue URL.");
            }
        }
    }

    private static string SafeFileName(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "launch-context" : normalized;
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
