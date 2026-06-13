using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

var parsed = CliArguments.Parse(args);
if (parsed.ShowHelp)
{
    Console.WriteLine(CliArguments.HelpText);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    await using var dbContext = CreateDbContext(parsed);
    var service = new ParserIngestionService(dbContext);
    var options = new ParserIngestionOptions(parsed.BatchSize, parsed.MaxRows, parsed.DryRun);
    var result = parsed.Command switch
    {
        "validate-products" => await service.ValidateProductsAsync(parsed.Target, options, cancellation.Token),
        "validate-reviews" => await service.ValidateReviewsAsync(parsed.Target, options, cancellation.Token),
        "validate-ranks" => await service.ValidateRanksAsync(parsed.Target, options, cancellation.Token),
        "validate-logistics" => await service.ValidateLogisticsAsync(parsed.Target, options, cancellation.Token),
        "validate-product-details" => await service.ValidateProductDetailsAsync(parsed.Target, options, cancellation.Token),
        "stage-products" => await service.StageProductsAsync(parsed.Target, options, cancellation.Token),
        "stage-reviews" => await service.StageReviewsAsync(parsed.Target, options, cancellation.Token),
        "stage-ranks" => await service.StageRanksAsync(parsed.Target, options, cancellation.Token),
        "stage-logistics" => await service.StageLogisticsAsync(parsed.Target, options, cancellation.Token),
        "stage-product-details" => await service.StageProductDetailsAsync(parsed.Target, options, cancellation.Token),
        "promote-products" => await service.PromoteProductsAsync(parsed.Target, options, cancellation.Token),
        _ => throw new ArgumentException($"Unsupported command '{parsed.Command}'.")
    };

    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return result.ErrorCount == 0 ? 0 : 2;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Parser ingestion command was cancelled.");
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static ApplicationDbContext CreateDbContext(CliArguments parsed)
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>();
    if (!parsed.RequiresDatabase)
        return new ApplicationDbContext(options.Options);

    var connectionString = parsed.ConnectionString
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? Environment.GetEnvironmentVariable("ASHMES_POSTGRES_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "A PostgreSQL connection string is required. Pass --connection-string or set ConnectionStrings__Postgres.");
    }

    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
    return new ApplicationDbContext(options.Options);
}

internal sealed record CliArguments(
    string Command,
    string Target,
    int BatchSize,
    long? MaxRows,
    bool DryRun,
    string? ConnectionString,
    bool ShowHelp)
{
    public bool RequiresDatabase =>
        Command == "promote-products"
        || (!DryRun && Command is "stage-products" or "stage-reviews" or "stage-ranks" or "stage-logistics" or "stage-product-details");

    public static string HelpText =>
        """
        AshmesMarketplaces parser ingestion CLI

        Commands:
          validate-products <run-directory> [--limit <rows>]
          validate-reviews <run-directory> [--limit <rows>]
          validate-ranks <run-directory> [--limit <rows>]
          validate-logistics <run-directory> [--limit <rows>]
          validate-product-details <run-directory> [--limit <rows>]
          stage-products <run-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          stage-reviews <run-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          stage-ranks <run-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          stage-logistics <run-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          stage-product-details <run-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          promote-products <parser-run-id> [--dry-run] [--batch-size <rows>] [--limit <rows>]

        Write commands need --connection-string or ConnectionStrings__Postgres.
        Stage commands with --dry-run only read parser artifacts and do not need database access.
        Review staging preserves capped/root-level partial snapshot semantics and does not write domain Reviews.
        Rank staging preserves observed rank/page-fetch evidence and does not infer product positions.
        Logistics staging preserves raw WB availability evidence and does not write domain Logistics or Warehouses.
        """;

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
            return new CliArguments(string.Empty, string.Empty, 5000, null, false, null, true);

        if (args.Length < 2)
            throw new ArgumentException("A command and target are required. Use --help for syntax.");

        var command = args[0].Trim().ToLowerInvariant();
        if (command is not ("validate-products" or "validate-reviews" or "validate-ranks" or "validate-logistics" or "validate-product-details" or "stage-products" or "stage-reviews" or "stage-ranks" or "stage-logistics" or "stage-product-details" or "promote-products"))
            throw new ArgumentException($"Unsupported command '{args[0]}'. Use --help for syntax.");

        var batchSize = command == "promote-products" ? 1000 : 5000;
        long? maxRows = null;
        var dryRun = false;
        string? connectionString = null;
        for (var index = 2; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--batch-size":
                    batchSize = ParseIntOption(args, ref index, "--batch-size");
                    break;
                case "--limit":
                    maxRows = ParseLongOption(args, ref index, "--limit");
                    break;
                case "--connection-string":
                    connectionString = RequiredOptionValue(args, ref index, "--connection-string");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'. Use --help for syntax.");
            }
        }

        return new CliArguments(command, args[1], batchSize, maxRows, dryRun, connectionString, false);
    }

    private static int ParseIntOption(string[] args, ref int index, string option)
    {
        var raw = RequiredOptionValue(args, ref index, option);
        if (!int.TryParse(raw, out var value) || value < 1)
            throw new ArgumentException($"{option} must be a positive integer.");
        return value;
    }

    private static long ParseLongOption(string[] args, ref int index, string option)
    {
        var raw = RequiredOptionValue(args, ref index, option);
        if (!long.TryParse(raw, out var value) || value < 1)
            throw new ArgumentException($"{option} must be a positive integer.");
        return value;
    }

    private static string RequiredOptionValue(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}
