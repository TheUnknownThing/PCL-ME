#!/usr/bin/env python3

from __future__ import annotations

import argparse
import fnmatch
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


EXCLUDED_FILE_PATTERNS = ("*.toml", "*.md", "*.yaml", "*.yml", "*Test.*", "*.xml")
SKIPPED_DIRECTORIES = {".git", ".idea", ".vs", ".vscode", "bin", "node_modules", "obj"}
COMMENT_LINE_PATTERN = re.compile(r"^\s*//")
CHINESE_PATTERN = re.compile(r"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]")


def configure_utf8_output() -> None:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Find source lines containing Chinese characters outside documentation, YAML, TOML, and test files."
    )
    parser.add_argument(
        "root",
        nargs="?",
        default=Path(__file__).resolve().parent.parent,
        type=Path,
        help="Repository root to scan. Defaults to the repository that contains this script.",
    )
    return parser


def build_ripgrep_command() -> list[str]:
    command = [
        "rg",
        "-n",
        "--no-heading",
        "--color",
        "never",
        "-P",
        r"^(?:(?!//).)*\p{Han}",
    ]

    for pattern in EXCLUDED_FILE_PATTERNS:
        command.extend(["-g", f"!{pattern}"])

    for directory in sorted(SKIPPED_DIRECTORIES):
        command.extend(["-g", f"!**/{directory}/**"])

    command.append(".")
    return command


def run_ripgrep(root: Path) -> int:
    process = subprocess.Popen(
        build_ripgrep_command(),
        cwd=root,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    assert process.stdout is not None
    assert process.stderr is not None

    for raw_line in process.stdout:
        line = raw_line.decode("utf-8", errors="replace")
        if line.startswith("./"):
            line = line[2:]
        sys.stdout.write(line)

    stderr_output = process.stderr.read().decode("utf-8", errors="replace")
    return_code = process.wait()

    if return_code not in (0, 1):
        if stderr_output:
            print(stderr_output.rstrip(), file=sys.stderr)
        return return_code

    return 0


def is_text_file(path: Path) -> bool:
    try:
        with path.open("rb") as file_handle:
            return b"\0" not in file_handle.read(4096)
    except OSError:
        return False


def run_python_fallback(root: Path) -> int:
    for current_root, directory_names, file_names in os.walk(root):
        directory_names[:] = [name for name in directory_names if name not in SKIPPED_DIRECTORIES]

        current_directory = Path(current_root)
        for file_name in file_names:
            if any(fnmatch.fnmatch(file_name, pattern) for pattern in EXCLUDED_FILE_PATTERNS):
                continue

            path = current_directory / file_name
            if not is_text_file(path):
                continue

            try:
                with path.open("r", encoding="utf-8", errors="replace", newline="") as file_handle:
                    for line_number, line in enumerate(file_handle, start=1):
                        if COMMENT_LINE_PATTERN.match(line) is not None:
                            continue

                        code_portion = line.split("//", 1)[0]
                        if CHINESE_PATTERN.search(code_portion) is None:
                            continue

                        relative_path = path.relative_to(root).as_posix()
                        print(f"{relative_path}:{line_number}:{line.rstrip()}")
            except OSError:
                continue

    return 0


def main() -> int:
    configure_utf8_output()

    args = build_argument_parser().parse_args()
    root = args.root.resolve()

    if not root.exists():
        print(f"Scan root does not exist: {root}", file=sys.stderr)
        return 2

    if shutil.which("rg") is not None:
        return run_ripgrep(root)

    return run_python_fallback(root)


if __name__ == "__main__":
    raise SystemExit(main())
