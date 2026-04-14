#!/usr/bin/env python3
"""Recalculate artifact.sha1 values in native-replacements.json from artifact URLs.

Warning: whenever native-replacements.json is modified, ensure artifact.sha1 values
are accurate (manual verification or by running this script) before commit.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
import sys
from pathlib import Path
from typing import Any
from urllib.request import Request, urlopen

DEFAULT_JSON_PATH = (
    Path(__file__).resolve().parent.parent
    / "Assets"
    / "LauncherAssets"
    / "NativeReplacements"
    / "native-replacements.json"
)


def sha1_from_url(url: str, timeout: float) -> str:
    request = Request(url, headers={"User-Agent": "PCL-ME-SHA1-Recalculator/1.0"})
    digest = hashlib.sha1()
    with urlopen(request, timeout=timeout) as response:
        while True:
            chunk = response.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def collect_urls(payload: dict[str, Any]) -> list[str]:
    urls: set[str] = set()
    for replacements in payload.values():
        if not isinstance(replacements, dict):
            continue
        for entry in replacements.values():
            if not isinstance(entry, dict):
                continue
            artifact = entry.get("artifact")
            if not isinstance(artifact, dict):
                continue
            url = artifact.get("url")
            if isinstance(url, str) and url:
                urls.add(url)
    return sorted(urls)


def recalc(
    payload: dict[str, Any],
    sha1_by_url: dict[str, str],
) -> tuple[int, int]:
    updated = 0
    total = 0

    for replacements in payload.values():
        if not isinstance(replacements, dict):
            continue
        for entry in replacements.values():
            if not isinstance(entry, dict):
                continue
            artifact = entry.get("artifact")
            if not isinstance(artifact, dict):
                continue
            url = artifact.get("url")
            if not isinstance(url, str) or not url:
                continue

            total += 1
            new_sha1 = sha1_by_url[url]
            old_sha1 = artifact.get("sha1")
            if old_sha1 != new_sha1:
                artifact["sha1"] = new_sha1
                updated += 1

    return updated, total


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Recalculate artifact.sha1 values in native-replacements.json."
    )
    parser.add_argument(
        "--file",
        type=Path,
        default=DEFAULT_JSON_PATH,
        help=f"Path to native-replacements.json (default: {DEFAULT_JSON_PATH})",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=60.0,
        help="HTTP timeout per request in seconds (default: 60)",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=8,
        help="Number of parallel download workers (default: 8)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Compute and report updates without writing file.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    json_path: Path = args.file.resolve()

    if not json_path.exists():
        print(f"error: file not found: {json_path}", file=sys.stderr)
        return 1

    payload = json.loads(json_path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        print("error: top-level JSON must be an object", file=sys.stderr)
        return 1

    urls = collect_urls(payload)
    if not urls:
        print("No artifact URLs found; nothing to do.")
        return 0

    print(f"Computing SHA1 for {len(urls)} unique URLs...")
    sha1_by_url: dict[str, str] = {}

    workers = max(1, args.workers)
    try:
        with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
            future_to_url = {
                executor.submit(sha1_from_url, url, args.timeout): url for url in urls
            }
            for future in concurrent.futures.as_completed(future_to_url):
                url = future_to_url[future]
                sha1_by_url[url] = future.result()
    except Exception as exc:
        print(f"error: failed to compute SHA1: {exc}", file=sys.stderr)
        return 1

    updated, total = recalc(payload, sha1_by_url)
    print(f"Checked {total} artifact entries; {updated} SHA1 values need update.")

    if args.dry_run:
        print("Dry run mode: file not modified.")
        return 0

    json_path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"Updated file: {json_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
