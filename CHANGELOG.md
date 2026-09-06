# Changelog

All notable changes to dottype are documented here.

## [Unreleased]

## [0.2.0] - 2026-09-06

### Upgrade from 0.1.0

- Replace `--name Customer --namespace Models` with `--name Models.Customer`.
- Inference now samples the first 1,000 JSON/CSV records. Add `--sample all` to
  retain full-input inference; data outside the sample is not validated.
- Use lowercase format, compression, and `all` values.

### Changes

- Add CLI shell completion for option names, format/compression values, sampling, and paths,
  with cursor-aware `[suggest]` requests and shell setup instructions in USAGE.md.
- Require lowercase format, compression, and `all` argument values in every entry point;
  use System.CommandLine's allowed-value validation and suggestions for format/compression.
- Add an embedded package icon, package-specific NuGet READMEs, richer descriptions
  and discovery tags, and direct documentation and release-history links to both packages.
- Prefer `--sample`; retain `--sample-records` as an alias across all entry points.
- Shorten README and CLI help, with detailed behavior and editor setup in USAGE.md.
- Share one settings record between CLI and build parsing; isolate qualified-name splitting.
- Migrate shared argument parsing and CLI help to System.CommandLine 2.0.11.
  Accept `--name=value` syntax and `--` before filenames beginning with a dash.
- Combine type and namespace into `--name Models.Customer` in every entry point;
  remove the separate `--namespace` option. Plain names use the global namespace.
- Generate CLI help from the shared System.CommandLine definitions; cover `--help` and `-h` with
  tests for aliases, defaults, examples, and avoiding generation.
- Include `llms.txt` in both packages and prepare both packages in the release workflow.
- Share argument parsing and validation across all entry points. Add CLI sampling
  and use the same 1,000-record default everywhere; pass `--sample-records all`
  to retain the CLI's previous full-input behavior.
- Add a canonical usage reference, package discovery metadata, and an agent entry point.
- Expose a `dottype options` field in Visual Studio's file Properties window.
- Add a lowercase `dottype` Build Action for Visual Studio projects, reusing
  inference and caching to provide generated C# during design-time builds.
- Use lowercase `dottype` for configuration, examples, and diagnostics.
- Add `dottype.Build` for generating types from one example file through a single
  `dottype` property in file-based apps and ordinary projects.
- Add editor generation, saved-example change tracking, and `dottype001` diagnostics.
- Default inference to 1,000 complete JSON/CSV records, with configurable
  sampling and full-input inference.
- Cache by input modification time/size, options, and generator identity; support
  forced refresh and preserve generated-source timestamps for unchanged schemas.

## [0.1.0] - 2026-09-05

- Generate partial C# POCOs from JSON, CSV, and Parquet sources.
- Preserve source field names when valid and record reversible metadata when changed.
- Read input from a file or standard input and write output to a file or standard output.
- Read gzip (`.gz`) and Brotli (`.br`) wrappers for JSON and CSV; allow explicit wrapper compression for binary stdin. Parquet files use internal compression and keep the `.parquet` extension.
- Reject corrupt and truncated compressed input with a clean error.
- Correct the default type-name option and strip compression suffixes from inferred names.
- Preserve valid names during repair collisions and handle enclosing-class, keyword, and Unicode identifier conflicts.
- Verify generated models compile and preserve JSON field names through serialization.
- Add NuGet packaging checks, binary-input smoke tests, dependency auditing, and protected release version checks.
- Protect output targets with atomic replacement, reject invalid UTF-8 CSV, and verify nested Parquet generation.
- Remove local source paths from symbols and prevent untracked-source embedding.
