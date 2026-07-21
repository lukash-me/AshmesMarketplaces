# Parser ingestion map

The parser ingestion CLI is the boundary between Python parser artifacts and the
backend database. Its public command contract is unchanged by the parser
refactor.

## CLI entrypoint

`Backend/AshmesMarketplaces.ParserIngestionCli/Program.cs` parses command-line
arguments, creates `ApplicationDbContext`, then calls the application facade
`IParserIngestionService`.

Supported command groups:

- `validate-*` reads manifests and JSONL files without writing rows;
- `stage-*` imports artifacts into parser ingestion tables;
- `stage-complete-batch` registers a finished streaming batch;
- `complete-parser-pipeline` marks a full parser pipeline as complete;
- `promote-products` moves staged product rows into production-visible state;
- maintenance commands audit, purge or repair parser data quality.

## Application service layout

`ParserIngestionService` is intentionally kept as the facade used by the CLI.
The large implementation is split with partial files:

- `ParserIngestionService.cs` keeps command orchestration and import logic;
- `ParserIngestionService.Types.cs` keeps private records and helper classes
  used by the facade.

The next safe split points are:

- parser run registration;
- artifact readers;
- product, rank, review, logistics and detail row ingestors;
- pipeline completion and promotion.

Those splits should preserve the CLI command names, DTOs and database contracts.

## Read-side layout

`ParserProductReadService` remains the facade used by parser observability
controllers. Private read-side helper types are in
`ParserProductReadService.Types.cs`; future extraction can split list, details,
filters, demo-card options, evidence loading and logistics assembly without
changing public HTTP routes.
