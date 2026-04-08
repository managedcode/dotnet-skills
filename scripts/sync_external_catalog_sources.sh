#!/usr/bin/env bash
set -euo pipefail

if ! command -v vendir >/dev/null 2>&1; then
  echo "vendir is required. Install it from https://carvel.dev/vendir/ and rerun this script." >&2
  exit 1
fi

vendir sync --chdir external-sources
python3 scripts/import_external_catalog_sources.py
