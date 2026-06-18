using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Bootstrap;
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
    if (parsed.Command == "ensure-admin-user")
    {
        var password = parsed.Password
            ?? Environment.GetEnvironmentVariable("ASHMES_ADMIN_PASSWORD");
        var workspaceName = parsed.WorkspaceName
            ?? Environment.GetEnvironmentVariable("ASHMES_ADMIN_WORKSPACE_NAME")
            ?? "Ashmes Production Workspace";
        var bootstrap = new AdminBootstrapService(dbContext, new PasswordHashService());
        var bootstrapResult = await bootstrap.EnsureAdminAsync(
            parsed.Target,
            password ?? string.Empty,
            workspaceName,
            parsed.ResetPassword,
            cancellation.Token);

        Console.WriteLine(JsonSerializer.Serialize(bootstrapResult, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    if (parsed.Command is "audit-quality" or "purge-quality" or "audit-defective-cards" or "purge-defective-cards" or "repair-parser-batch-timestamps")
    {
        var maintenance = new ParserDataQualityMaintenance(dbContext);
        var maintenanceResult = parsed.Command switch
        {
            "audit-quality" => await maintenance.AuditAsync(parsed.Target, cancellation.Token),
            "purge-quality" => await maintenance.PurgeAsync(parsed.Target, parsed.Confirm, cancellation.Token),
            "audit-defective-cards" => await maintenance.AuditDefectiveCardsAsync(parsed.Target, cancellation.Token),
            "purge-defective-cards" => await maintenance.PurgeDefectiveCardsAsync(parsed.Target, parsed.Confirm, cancellation.Token),
            "repair-parser-batch-timestamps" => await maintenance.RepairParserBatchTimestampsAsync(parsed.Target, parsed.Confirm, cancellation.Token),
            _ => throw new ArgumentException($"Unsupported command '{parsed.Command}'.")
        };

        Console.WriteLine(JsonSerializer.Serialize(maintenanceResult, new JsonSerializerOptions { WriteIndented = true }));
        return maintenanceResult.ErrorCount == 0 ? 0 : 2;
    }

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
        "stage-complete-batch" => await service.StageCompleteBatchAsync(parsed.Target, options, cancellation.Token),
        "complete-parser-pipeline" => await service.CompleteParserPipelineAsync(parsed.Target, options, cancellation.Token),
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
    {
        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        npgsqlOptions.CommandTimeout(300);
    });
    return new ApplicationDbContext(options.Options);
}

internal sealed record CliArguments(
    string Command,
    string Target,
    int BatchSize,
    long? MaxRows,
    bool DryRun,
    string? ConnectionString,
    bool Confirm,
    string? Password,
    string? WorkspaceName,
    bool ResetPassword,
    bool ShowHelp)
{
    public bool RequiresDatabase =>
        Command is "ensure-admin-user" or "promote-products" or "complete-parser-pipeline" or "audit-quality" or "purge-quality" or "audit-defective-cards" or "purge-defective-cards" or "repair-parser-batch-timestamps"
        || (!DryRun && Command is "stage-products" or "stage-reviews" or "stage-ranks" or "stage-logistics" or "stage-product-details" or "stage-complete-batch");

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
          stage-complete-batch <batch-directory> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          complete-parser-pipeline <pipeline-run-id> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          promote-products <parser-run-id> [--dry-run] [--batch-size <rows>] [--limit <rows>]
          audit-quality <output-directory>
          purge-quality <output-directory> --confirm
          audit-defective-cards <output-directory>
          purge-defective-cards <output-directory> --confirm
          repair-parser-batch-timestamps <output-directory> [--confirm]
          ensure-admin-user <login> [--password <password>] [--workspace-name <name>] [--reset-password]

        Write commands need --connection-string or ConnectionStrings__Postgres.
        ensure-admin-user can read ASHMES_ADMIN_PASSWORD and ASHMES_ADMIN_WORKSPACE_NAME from environment.
        purge-quality creates backup tables in schema ParserQualityBackups before deleting rows.
        purge-defective-cards creates backup tables in schema ParserQualityBackups before deleting rows.
        repair-parser-batch-timestamps writes a report and updates batch ParserRuns / parser-owned Products only with --confirm.
        Stage commands with --dry-run only read parser artifacts and do not need database access.
        Review staging preserves capped/root-level partial snapshot semantics and does not write domain Reviews.
        Rank staging preserves observed rank/page-fetch evidence and does not infer product positions.
        Logistics staging preserves raw WB availability evidence and does not write domain Logistics or Warehouses.
        """;

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
            return new CliArguments(string.Empty, string.Empty, 5000, null, false, null, false, null, null, false, true);

        if (args.Length < 2)
            throw new ArgumentException("A command and target are required. Use --help for syntax.");

        var command = args[0].Trim().ToLowerInvariant();
        if (command is not ("validate-products" or "validate-reviews" or "validate-ranks" or "validate-logistics" or "validate-product-details" or "stage-products" or "stage-reviews" or "stage-ranks" or "stage-logistics" or "stage-product-details" or "stage-complete-batch" or "complete-parser-pipeline" or "promote-products" or "audit-quality" or "purge-quality" or "audit-defective-cards" or "purge-defective-cards" or "repair-parser-batch-timestamps" or "ensure-admin-user"))
            throw new ArgumentException($"Unsupported command '{args[0]}'. Use --help for syntax.");

        var batchSize = command is "promote-products" or "stage-complete-batch" ? 1000 : 5000;
        long? maxRows = null;
        var dryRun = false;
        string? connectionString = null;
        var confirm = false;
        string? password = null;
        string? workspaceName = null;
        var resetPassword = false;
        for (var index = 2; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--confirm":
                    confirm = true;
                    break;
                case "--reset-password":
                    resetPassword = true;
                    break;
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
                case "--password":
                    password = RequiredOptionValue(args, ref index, "--password");
                    break;
                case "--workspace-name":
                    workspaceName = RequiredOptionValue(args, ref index, "--workspace-name");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'. Use --help for syntax.");
            }
        }

        return new CliArguments(command, args[1], batchSize, maxRows, dryRun, connectionString, confirm, password, workspaceName, resetPassword, false);
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
