using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserLaunchCommandBuilderTests
{
    [Fact]
    public void Build_adds_smoke_max_batches_for_limited_all()
    {
        var request = Launch(ParserLaunchModes.LimitedAll, batchLimit: 3);

        var command = ParserLaunchCommandBuilder.Build(request, Options());

        Assert.Contains("--parser-instance-id", command.Arguments);
        Assert.Contains("parser-local-01", command.Arguments);
        Assert.Contains("--smoke-max-batches", command.Arguments);
        Assert.Contains("3", command.Arguments);
        Assert.DoesNotContain("--only-proxy", command.Arguments);
    }

    [Fact]
    public void Build_omits_smoke_limit_for_full_all()
    {
        var request = Launch(ParserLaunchModes.FullAll);

        var command = ParserLaunchCommandBuilder.Build(request, Options());

        Assert.DoesNotContain("--smoke-max-batches", command.Arguments);
        Assert.DoesNotContain("--only-proxy", command.Arguments);
    }

    [Fact]
    public void Build_adds_proxy_and_smoke_limit_for_check_proxy()
    {
        var request = Launch(ParserLaunchModes.CheckProxy, batchLimit: 2, proxyKey: "proxy-1");

        var command = ParserLaunchCommandBuilder.Build(request, Options());

        Assert.Contains("--only-proxy", command.Arguments);
        Assert.Contains("proxy-1", command.Arguments);
        Assert.Contains("--smoke-max-batches", command.Arguments);
        Assert.Contains("2", command.Arguments);
    }

    [Fact]
    public void Build_forces_fresh_browser_session_per_launch()
    {
        var request = Launch(ParserLaunchModes.CheckProxy, batchLimit: 2, proxyKey: "proxy-1");

        var command = ParserLaunchCommandBuilder.Build(request, Options());

        Assert.Equal("run", command.Environment["PARSER_SESSION_CACHE_MODE"]);
        Assert.Equal("1", command.Environment["PARSER_FORCE_REFRESH_TOKEN"]);
    }

    [Fact]
    public void Build_adds_launch_context_for_service_launch()
    {
        var request = Launch(ParserLaunchModes.CheckProxy, batchLimit: 2, proxyKey: "proxy-guid");

        var command = ParserLaunchCommandBuilder.Build(request, Options(), "E:/runtime/launch_context.json");

        Assert.Contains("--launch-context", command.Arguments);
        Assert.Contains("E:/runtime/launch_context.json", command.Arguments);
        Assert.Equal("E:/runtime/launch_context.json", command.Environment["PARSER_LAUNCH_CONTEXT_FILE"]);
        Assert.Equal("http://api:8080/api/v1/parser", command.Environment["PARSER_BATCH_QUEUE_URL"]);
        Assert.Equal("/parser-data/output", command.Environment["PARSER_OUTPUT_BASE_DIR"]);
        Assert.Equal("/parser-data/output/rate_limits", command.Environment["PARSER_RATE_LIMIT_STATE_DIR"]);
    }

    private static ParserLaunchCommandOptions Options() =>
        new(
            "python",
            "Parser/app/proxy_supervisor.py",
            "Parser/presets/production/market_refresh_selected_niches_batched.prod.json",
            "batched_full_enrichment",
            "Parser/output/outbox",
            "E:/AshmesMarketplaces",
            "http://api:8080/api/v1/parser",
            "/parser-data/output",
            "/parser-data/output/rate_limits");

    private static ParserLaunchRequest Launch(string mode, int? batchLimit = null, string? proxyKey = null)
    {
        var now = DateTime.UtcNow;
        return new ParserLaunchRequest(
            Guid.NewGuid(),
            "parser-local-01",
            mode,
            proxyKey,
            batchLimit,
            Guid.NewGuid(),
            now);
    }
}
