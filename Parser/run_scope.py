from __future__ import annotations

import os
from typing import Any


def parser_run_scope_from_env() -> dict[str, Any]:
    is_test_run = os.environ.get("PARSER_IS_TEST_RUN", "").strip().lower() in {"1", "true", "yes", "on"}
    test_label = os.environ.get("PARSER_TEST_LABEL", "").strip()
    pipeline_run_id = os.environ.get("PARSER_PIPELINE_RUN_ID", "").strip()
    batch_id = os.environ.get("PARSER_BATCH_ID", "").strip()
    batch_index = os.environ.get("PARSER_BATCH_INDEX", "").strip()
    worker_id = os.environ.get("PARSER_WORKER_ID", "").strip()
    shard_key = os.environ.get("PARSER_SHARD_KEY", "").strip()
    source_niche = os.environ.get("PARSER_SOURCE_NICHE", "").strip()
    is_complete_card_batch = os.environ.get("PARSER_IS_COMPLETE_CARD_BATCH", "").strip().lower() in {
        "1",
        "true",
        "yes",
        "on",
    }
    run_purpose = os.environ.get("PARSER_RUN_PURPOSE", "").strip()

    scope: dict[str, Any] = {}
    if is_test_run:
        scope["is_test_run"] = True
    if test_label:
        scope["test_label"] = test_label
    if pipeline_run_id:
        scope["pipeline_run_id"] = pipeline_run_id
    if batch_id:
        scope["batch_id"] = batch_id
    if batch_index:
        try:
            scope["batch_index"] = int(batch_index)
        except ValueError:
            scope["batch_index"] = batch_index
    if worker_id:
        scope["worker_id"] = worker_id
    if shard_key:
        scope["shard_key"] = shard_key
    if source_niche:
        scope["source_niche"] = source_niche
    if is_complete_card_batch:
        scope["is_complete_card_batch"] = True
    if run_purpose:
        scope["run_purpose"] = run_purpose
    return scope
