from __future__ import annotations

import argparse
import json
import statistics
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from logistics_exporters import iter_jsonl  # noqa: E402


PRODUCT_FIELDS = [
    "total_quantity_observed",
    "product_wh_raw",
    "product_time1_raw",
    "product_time2_raw",
    "product_dtype_raw",
    "product_dist_raw",
]
DESTINATION_FIELDS = [
    "product_wh_raw",
    "product_time1_raw",
    "product_time2_raw",
    "product_dtype_raw",
    "product_dist_raw",
]
WAREHOUSE_STABILITY_FIELDS = [
    "warehouse_id_on_mp",
    "quantity_observed",
    "stock_time1_raw",
    "stock_time2_raw",
    "stock_dtype_raw",
    "stock_dist_raw",
    "price_logistics_raw",
    "price_return_raw",
]


@dataclass(frozen=True)
class LogisticsArtifactRun:
    run_dir: Path
    manifest: dict[str, Any]
    snapshots: list[dict[str, Any]]
    warehouse_rows: list[dict[str, Any]]
    errors: list[dict[str, Any]]

    @property
    def parser_run_id(self) -> str:
        return str(self.manifest.get("parser_run_id") or self.run_dir.name)

    @property
    def destination(self) -> str:
        return str(self.manifest.get("source_region_dest") or _first_value(self.snapshots, "source_region_dest") or "")

    @property
    def status(self) -> str:
        return str(self.manifest.get("status") or "missing_manifest")


def load_artifact_run(run_dir: Path) -> LogisticsArtifactRun:
    manifest_path = run_dir / "manifest.json"
    manifest = _read_json(manifest_path) if manifest_path.exists() else {
        "parser_run_id": run_dir.name,
        "status": "missing_manifest",
    }
    return LogisticsArtifactRun(
        run_dir=run_dir,
        manifest=manifest,
        snapshots=list(iter_jsonl(run_dir / "logistics_snapshots.jsonl") or []),
        warehouse_rows=list(iter_jsonl(run_dir / "warehouse_availability.jsonl") or []),
        errors=list(iter_jsonl(run_dir / "errors.jsonl") or []),
    )


def analyze_runs(run_dirs: Iterable[Path], *, products_jsonl: Path | None = None) -> dict[str, Any]:
    runs = [load_artifact_run(Path(run_dir)) for run_dir in run_dirs]
    snapshots = [row for run in runs for row in run.snapshots]
    warehouse_rows = [row for run in runs for row in run.warehouse_rows]
    errors = [row for run in runs for row in run.errors]
    product_input_ids = _read_product_ids(products_jsonl) if products_jsonl else set()

    return {
        "generated_at_utc": _utc_now_iso(),
        "products_jsonl": str(products_jsonl) if products_jsonl else None,
        "product_input_count": len(product_input_ids) if product_input_ids else None,
        "run_summaries": _run_summaries(runs),
        "quantity_summary": _quantity_summary(snapshots),
        "stock_consistency": _stock_consistency(snapshots, warehouse_rows),
        "destination_impact": _destination_impact(snapshots),
        "repeat_stability": _repeat_stability(snapshots, warehouse_rows),
        "warehouse_summary": _warehouse_summary(warehouse_rows),
        "errors_summary": _errors_summary(runs, errors),
    }


def render_markdown(report: dict[str, Any]) -> str:
    lines: list[str] = [
        "# WB Logistics Artifact Validation",
        "",
        f"Generated UTC: `{report['generated_at_utc']}`",
    ]
    if report.get("products_jsonl"):
        lines.append(f"Products input: `{_safe_path(report['products_jsonl'])}`")
    if report.get("product_input_count") is not None:
        lines.append(f"Products input unique ids available: `{report['product_input_count']}`")

    lines.extend([
        "",
        "## Run Summary",
        "",
        "| Run id | Status | Dest | Requested | Snapshots | Warehouse rows | Errors | Products succeeded | Products failed |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|",
    ])
    for row in report["run_summaries"]:
        lines.append(
            "| {run_id} | {status} | {dest} | {products_requested} | {snapshots} | {warehouse_rows} | {errors} | {products_succeeded} | {products_failed} |".format(
                run_id=_md(row["run_id"]),
                status=_md(row["status"]),
                dest=_md(row["dest"]),
                products_requested=row["products_requested"],
                snapshots=row["snapshot_rows"],
                warehouse_rows=row["warehouse_rows"],
                errors=row["error_rows"],
                products_succeeded=row["products_succeeded"],
                products_failed=row["products_failed"],
            )
        )

    quantity = report["quantity_summary"]
    lines.extend([
        "",
        "## Quantity And Cap Findings",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Snapshot rows | {quantity['snapshot_rows']} |",
        f"| Unique products | {quantity['unique_products']} |",
        f"| Min quantity | {_value(quantity['min_quantity'])} |",
        f"| Max quantity | {_value(quantity['max_quantity'])} |",
        f"| Unique quantities | {_md(', '.join(str(item) for item in quantity['unique_quantities']))} |",
        f"| Rows equal to 50 | {quantity['rows_equal_50']} |",
        f"| Share equal to 50 | {quantity['share_equal_50_percent']}% |",
        "",
        "**Cap note:** `50` is cap-like when frequent, but this report does not prove a WB cap because the response has no explicit cap/max flag.",
    ])

    consistency = report["stock_consistency"]
    lines.extend([
        "",
        "## Product Total Vs Warehouse Quantity",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Compared product snapshots | {consistency['compared_snapshots']} |",
        f"| Sum matches total | {consistency['matching_sum_count']} |",
        f"| Sum differs from total | {consistency['mismatching_sum_count']} |",
        f"| Missing warehouse rows | {consistency['missing_warehouse_count']} |",
    ])
    if consistency["examples"]:
        lines.extend(["", "Mismatch examples:"])
        for example in consistency["examples"][:5]:
            lines.append(
                f"- `{example['run_id']}` product `{example['wb_product_id']}`: total `{example['total_quantity_observed']}`, warehouse sum `{example['warehouse_quantity_sum']}`"
            )

    destination = report["destination_impact"]
    lines.extend([
        "",
        "## Destination Impact",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Destinations observed | {_md(', '.join(destination['destinations']))} |",
        f"| Products present in multiple destinations | {destination['overlap_product_count']} |",
    ])
    for field, count in destination["changed_counts_by_field"].items():
        lines.append(f"| Products with changed `{field}` | {count} |")
    if destination["non_overlapping_products"]:
        lines.extend(["", "Non-overlapping product ids by destination:"])
        for dest, ids in destination["non_overlapping_products"].items():
            sample = ", ".join(ids[:10])
            suffix = "..." if len(ids) > 10 else ""
            lines.append(f"- `{dest}`: `{sample}{suffix}`")

    stability = report["repeat_stability"]
    lines.extend([
        "",
        "## Repeat Stability",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Product/destination groups with repeats | {stability['repeated_product_dest_groups']} |",
        f"| Groups with stable product fields | {stability['stable_product_groups']} |",
        f"| Groups with changed product fields | {stability['changed_product_groups']} |",
        f"| Warehouse groups with repeated observations | {stability['repeated_warehouse_groups']} |",
        f"| Warehouse groups with changed fields | {stability['changed_warehouse_groups']} |",
    ])
    for field, count in stability["changed_product_fields"].items():
        lines.append(f"| Changed product `{field}` | {count} |")
    for field, count in stability["changed_warehouse_fields"].items():
        lines.append(f"| Changed warehouse `{field}` | {count} |")

    warehouse = report["warehouse_summary"]
    lines.extend([
        "",
        "## Warehouse And Price Raw Findings",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Warehouse rows | {warehouse['warehouse_rows']} |",
        f"| Unique external WB warehouse ids | {warehouse['unique_warehouse_ids']} |",
        f"| Warehouse ids seen on multiple products | {warehouse['warehouse_ids_reused_across_products']} |",
        f"| Warehouses per product/run min | {_value(warehouse['warehouses_per_product_min'])} |",
        f"| Warehouses per product/run max | {_value(warehouse['warehouses_per_product_max'])} |",
        f"| Warehouses per product/run average | {_value(warehouse['warehouses_per_product_avg'])} |",
        f"| Quantity min | {_value(warehouse['quantity_min'])} |",
        f"| Quantity max | {_value(warehouse['quantity_max'])} |",
        f"| Price logistics unique values | {_md(', '.join(str(item) for item in warehouse['price_logistics_unique']))} |",
        f"| Price return unique values | {_md(', '.join(str(item) for item in warehouse['price_return_unique']))} |",
    ])

    errors = report["errors_summary"]
    lines.extend([
        "",
        "## Errors And Missing Data",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Error rows | {errors['error_rows']} |",
        f"| Runs without snapshots | {errors['runs_without_snapshots']} |",
        f"| Runs not succeeded | {errors['runs_not_succeeded']} |",
    ])
    for error_type, count in errors["error_types"].items():
        lines.append(f"| Error `{_md(error_type)}` | {count} |")

    lines.extend([
        "",
        "## L4 Staging Recommendation",
        "",
        "- Keep logistics as parser staging evidence first; do not import into domain Logistics yet.",
        "- First-class snapshot columns: parser run/file/line lineage, schema version, marketplace, observed UTC, request fingerprint, request family, endpoint family, destination, WB product/root ids, seller id/name, source category/subcategory/query, total quantity observed, cap fields, raw product `wh/time1/time2/dtype/dist`.",
        "- First-class warehouse columns: option/size fields, external WB warehouse id, quantity observed, raw stock `priority/time1/time2/dtype/dist`, raw price logistics/return.",
        "- JSONB/raw fields: `raw_observed_fields`, `raw_stock`, `raw_size_observed_fields`, and undecoded WB fragments retained for future research.",
        "- Unsafe for seller-facing UI: cap claims, stock risk, delivery forecasts/dates, exact logistics costs, warehouse names or internal meaning, route/geography claims, profitability implications.",
    ])
    return "\n".join(lines) + "\n"


def main() -> None:
    args = parse_args()
    report = analyze_runs(args.runs, products_jsonl=args.products_jsonl)
    markdown = render_markdown(report)
    if args.output_md:
        args.output_md.parent.mkdir(parents=True, exist_ok=True)
        args.output_md.write_text(markdown, encoding="utf-8")
        print(f"Validation report: {args.output_md}")
    else:
        print(markdown)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Analyze WB logistics JSONL artifacts without network or DB access.")
    parser.add_argument("--runs", type=Path, nargs="+", required=True, help="Logistics run directories to compare.")
    parser.add_argument("--products-jsonl", type=Path, help="Optional source products.jsonl used to report input product count.")
    parser.add_argument("--output-md", type=Path, help="Optional markdown report output path.")
    return parser.parse_args()


def _run_summaries(runs: list[LogisticsArtifactRun]) -> list[dict[str, Any]]:
    summaries: list[dict[str, Any]] = []
    for run in runs:
        counters = run.manifest.get("counters") or {}
        summaries.append({
            "run_id": run.parser_run_id,
            "status": run.status,
            "dest": run.destination,
            "snapshot_rows": len(run.snapshots),
            "warehouse_rows": len(run.warehouse_rows),
            "error_rows": len(run.errors),
            "products_requested": int(counters.get("products_requested") or run.manifest.get("products_requested") or 0),
            "products_succeeded": int(counters.get("products_succeeded") or run.manifest.get("products_succeeded") or 0),
            "products_failed": int(counters.get("products_failed") or run.manifest.get("products_failed") or 0),
        })
    return summaries


def _quantity_summary(snapshots: list[dict[str, Any]]) -> dict[str, Any]:
    quantities = [_int_or_none(row.get("total_quantity_observed")) for row in snapshots]
    quantities = [value for value in quantities if value is not None]
    rows_equal_50 = sum(1 for value in quantities if value == 50)
    return {
        "snapshot_rows": len(snapshots),
        "unique_products": len({str(row.get("wb_product_id")) for row in snapshots if row.get("wb_product_id") is not None}),
        "min_quantity": min(quantities) if quantities else None,
        "max_quantity": max(quantities) if quantities else None,
        "unique_quantities": sorted(set(quantities)),
        "rows_equal_50": rows_equal_50,
        "share_equal_50_percent": round((rows_equal_50 / len(quantities)) * 100, 1) if quantities else 0,
    }


def _stock_consistency(snapshots: list[dict[str, Any]], warehouse_rows: list[dict[str, Any]]) -> dict[str, Any]:
    stock_sums: dict[tuple[str, str], int] = defaultdict(int)
    stock_counts: Counter[tuple[str, str]] = Counter()
    for row in warehouse_rows:
        key = (str(row.get("parser_run_id")), str(row.get("wb_product_id")))
        quantity = _int_or_none(row.get("quantity_observed"))
        if quantity is not None:
            stock_sums[key] += quantity
            stock_counts[key] += 1

    compared = 0
    matching = 0
    mismatching = 0
    missing = 0
    examples: list[dict[str, Any]] = []
    for row in snapshots:
        key = (str(row.get("parser_run_id")), str(row.get("wb_product_id")))
        total = _int_or_none(row.get("total_quantity_observed"))
        if total is None:
            continue
        compared += 1
        if stock_counts[key] == 0:
            missing += 1
            continue
        stock_sum = stock_sums[key]
        if total == stock_sum:
            matching += 1
        else:
            mismatching += 1
            examples.append({
                "run_id": key[0],
                "wb_product_id": key[1],
                "total_quantity_observed": total,
                "warehouse_quantity_sum": stock_sum,
            })

    return {
        "compared_snapshots": compared,
        "matching_sum_count": matching,
        "mismatching_sum_count": mismatching,
        "missing_warehouse_count": missing,
        "examples": examples,
    }


def _destination_impact(snapshots: list[dict[str, Any]]) -> dict[str, Any]:
    by_dest: dict[str, dict[str, list[dict[str, Any]]]] = defaultdict(lambda: defaultdict(list))
    for row in snapshots:
        dest = str(row.get("source_region_dest") or "")
        product_id = str(row.get("wb_product_id") or "")
        if dest and product_id:
            by_dest[dest][product_id].append(row)

    destinations = sorted(by_dest)
    product_destinations: dict[str, set[str]] = defaultdict(set)
    for dest, products in by_dest.items():
        for product_id in products:
            product_destinations[product_id].add(dest)

    overlap_products = {product_id for product_id, dests in product_destinations.items() if len(dests) > 1}
    changed_counts = {field: 0 for field in DESTINATION_FIELDS}
    for product_id in overlap_products:
        for field in DESTINATION_FIELDS:
            values_by_dest = {
                dest: {_normalized_value(row.get(field)) for row in by_dest[dest][product_id]}
                for dest in product_destinations[product_id]
            }
            flattened = {value for values in values_by_dest.values() for value in values}
            if len(flattened) > 1:
                changed_counts[field] += 1

    non_overlap: dict[str, list[str]] = {}
    for dest, products in by_dest.items():
        only_here = sorted(product_id for product_id in products if product_destinations[product_id] == {dest})
        if only_here:
            non_overlap[dest] = only_here

    return {
        "destinations": destinations,
        "overlap_product_count": len(overlap_products),
        "changed_counts_by_field": changed_counts,
        "non_overlapping_products": non_overlap,
    }


def _repeat_stability(snapshots: list[dict[str, Any]], warehouse_rows: list[dict[str, Any]]) -> dict[str, Any]:
    product_groups: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in snapshots:
        key = (str(row.get("wb_product_id") or ""), str(row.get("source_region_dest") or ""))
        if key[0] and key[1]:
            product_groups[key].append(row)

    repeated_product_groups = [rows for rows in product_groups.values() if len({row.get("parser_run_id") for row in rows}) > 1]
    changed_product_fields = {field: 0 for field in PRODUCT_FIELDS}
    changed_product_groups = 0
    stable_product_groups = 0
    for rows in repeated_product_groups:
        group_changed = False
        for field in PRODUCT_FIELDS:
            if len({_normalized_value(row.get(field)) for row in rows}) > 1:
                changed_product_fields[field] += 1
                group_changed = True
        if group_changed:
            changed_product_groups += 1
        else:
            stable_product_groups += 1

    warehouse_groups: dict[tuple[str, str, str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in warehouse_rows:
        key = (
            str(row.get("wb_product_id") or ""),
            str(row.get("source_region_dest") or ""),
            str(row.get("option_id") or ""),
            str(row.get("size_orig_name") or ""),
            str(row.get("warehouse_id_on_mp") or ""),
        )
        if key[0] and key[1] and key[4]:
            warehouse_groups[key].append(row)

    repeated_warehouse_groups = [rows for rows in warehouse_groups.values() if len({row.get("parser_run_id") for row in rows}) > 1]
    changed_warehouse_fields = {field: 0 for field in WAREHOUSE_STABILITY_FIELDS}
    changed_warehouse_groups = 0
    for rows in repeated_warehouse_groups:
        group_changed = False
        for field in WAREHOUSE_STABILITY_FIELDS:
            if len({_normalized_value(row.get(field)) for row in rows}) > 1:
                changed_warehouse_fields[field] += 1
                group_changed = True
        if group_changed:
            changed_warehouse_groups += 1

    return {
        "repeated_product_dest_groups": len(repeated_product_groups),
        "stable_product_groups": stable_product_groups,
        "changed_product_groups": changed_product_groups,
        "changed_product_fields": changed_product_fields,
        "repeated_warehouse_groups": len(repeated_warehouse_groups),
        "changed_warehouse_groups": changed_warehouse_groups,
        "changed_warehouse_fields": changed_warehouse_fields,
    }


def _warehouse_summary(warehouse_rows: list[dict[str, Any]]) -> dict[str, Any]:
    warehouse_ids = [str(row.get("warehouse_id_on_mp")) for row in warehouse_rows if row.get("warehouse_id_on_mp") is not None]
    products_by_warehouse: dict[str, set[str]] = defaultdict(set)
    per_product_run: Counter[tuple[str, str]] = Counter()
    quantities: list[int] = []
    price_logistics: set[Any] = set()
    price_return: set[Any] = set()

    for row in warehouse_rows:
        run_product = (str(row.get("parser_run_id")), str(row.get("wb_product_id")))
        warehouse_id = row.get("warehouse_id_on_mp")
        if warehouse_id is not None:
            per_product_run[run_product] += 1
            products_by_warehouse[str(warehouse_id)].add(str(row.get("wb_product_id")))
        quantity = _int_or_none(row.get("quantity_observed"))
        if quantity is not None:
            quantities.append(quantity)
        price_logistics.add(_normalized_value(row.get("price_logistics_raw")))
        price_return.add(_normalized_value(row.get("price_return_raw")))

    warehouses_per_product = list(per_product_run.values())
    return {
        "warehouse_rows": len(warehouse_rows),
        "unique_warehouse_ids": len(set(warehouse_ids)),
        "warehouse_ids_reused_across_products": sum(1 for products in products_by_warehouse.values() if len(products) > 1),
        "warehouses_per_product_min": min(warehouses_per_product) if warehouses_per_product else None,
        "warehouses_per_product_max": max(warehouses_per_product) if warehouses_per_product else None,
        "warehouses_per_product_avg": round(statistics.mean(warehouses_per_product), 2) if warehouses_per_product else None,
        "quantity_min": min(quantities) if quantities else None,
        "quantity_max": max(quantities) if quantities else None,
        "price_logistics_unique": _sorted_values(price_logistics),
        "price_return_unique": _sorted_values(price_return),
    }


def _errors_summary(runs: list[LogisticsArtifactRun], errors: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "error_rows": len(errors),
        "runs_without_snapshots": sum(1 for run in runs if not run.snapshots),
        "runs_not_succeeded": sum(1 for run in runs if run.status != "succeeded"),
        "error_types": dict(sorted(Counter(str(row.get("error_type") or "unknown") for row in errors).items())),
    }


def _read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _read_product_ids(path: Path | None) -> set[str]:
    if path is None or not path.exists():
        return set()
    ids: set[str] = set()
    for row in iter_jsonl(path) or []:
        product_id = row.get("wb_product_id")
        if product_id is not None:
            ids.add(str(product_id))
    return ids


def _first_value(rows: list[dict[str, Any]], field: str) -> Any:
    for row in rows:
        value = row.get(field)
        if value is not None:
            return value
    return None


def _int_or_none(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _normalized_value(value: Any) -> Any:
    if isinstance(value, float):
        return round(value, 6)
    if isinstance(value, list):
        return tuple(value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def _sorted_values(values: set[Any]) -> list[Any]:
    return sorted(values, key=lambda item: str(item))


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _safe_path(value: str) -> str:
    return str(value).replace("\\", "/")


def _md(value: Any) -> str:
    return str(value).replace("|", "\\|")


def _value(value: Any) -> str:
    return "n/a" if value is None else _md(value)


if __name__ == "__main__":
    main()
