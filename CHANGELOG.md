# Changelog

All notable changes to dottype are documented here.

## [Unreleased]

## [0.1.0] - TBD

- Generate partial C# POCOs from JSON, CSV, and Parquet sources.
- Preserve source field names when valid and record reversible metadata when changed.
- Read input from a file or standard input and write output to a file or standard output.
- Read gzip (`.gz`) and Brotli (`.br`) wrappers for every supported format; allow explicit compression for binary stdin.
- Reject corrupt and truncated compressed input with a clean error.
- Correct the default type-name option and strip compression suffixes from inferred names.
- Preserve valid names during repair collisions and handle enclosing-class, keyword, and Unicode identifier conflicts.
- Verify generated models compile and preserve JSON field names through serialization.
- Add NuGet/WinGet packaging checks, binary-input smoke tests, dependency auditing, and protected release version checks.
- Protect output targets with atomic replacement, reject invalid UTF-8 CSV, and verify nested Parquet generation.
- Remove local source paths from symbols and prevent untracked-source embedding.
