# Release readiness — 2026-09-06

## dottype 0.2.0

The CLI 0.1.0 is already published on NuGet. Version 0.2.0 adds the first
`dottype.Build` package and changes the CLI argument contract and default
sampling. CHANGELOG.md includes explicit upgrade instructions. The existing
`v0.1.0` tag and published package are preserved.

## Verified locally

- Locked restore with the pinned .NET SDK 10.0.400.
- Release tests: 214 passed, zero failed, one Windows symlink test skipped;
  Linux/macOS CI exercises that regression.
- CLI and build-package dependency audits report no known vulnerable packages.
- Both 0.2.0 packages pass listing/icon/documentation verification. CLI package
  and portable symbols pass package-content and neutral-source-path checks.
- Packed build integration passes fresh design-time generation, quoted paths,
  execution, caching, invalidation, unchanged output, and error diagnostics.
- Visual Studio integration passes both dotnet MSBuild and installed Visual
  Studio 2026 MSBuild: Build Action, Properties schema, generated compilation,
  refresh, diagnostics, and removal.
- Windows x64 and ARM64 self-contained executables build. The x64 binary passes
  subprocess file and binary-stdin checks for plain, gzip, and Brotli JSON.
- Formatting verification passes after correcting whitespace and import order.
- Both workflow YAML files and release PowerShell scripts parse successfully.
- Initial hosted CI passed all 215 tests on Linux/macOS but exposed a verification
  script exit-code bug: the final expected failure leaked into the runner's exit
  status. The helpers now clear that code only after validating the expected
  `dottype001` failure; unexpected failures still stop verification.
- Gitleaks reports no secrets in the 86-file publication snapshot or outgoing
  branch history. A separate source path/email scan reports no findings.
- The additional VS Code UI check could not start because VS Code's updater
  holds its startup mutex. Editor generator unit tests pass; the earlier tested
  extension versions remain documented in USAGE.md.

## Release results

- Annotated tag `v0.2.0` points to commit `775ac89533fcb65c519a8dcb5ad4862d10fdced1`.
- [Windows/Linux/macOS CI](https://github.com/DouglasCleghorn/dottype/actions/runs/34046135967)
  passed for that commit, including packed integration checks on all platforms.
- The [unpublished candidate run](https://github.com/DouglasCleghorn/dottype/actions/runs/34046326229)
  passed, including execution of its uploaded binary on native Windows ARM64.
  Downloaded packages passed inspection and all release checksums matched.
- [GitHub publication](https://github.com/DouglasCleghorn/dottype/actions/runs/34046572938)
  succeeded. The [0.2.0 release](https://github.com/DouglasCleghorn/dottype/releases/tag/v0.2.0)
  contains both packages, CLI symbols, Windows x64/ARM64 binaries, license notices,
  checksums, and upgrade notes. Published asset digests match SHA256SUMS.txt.
- With maintainer authorization, the existing NuGet trusted policy now permits
  the exact IDs `dottype` and `dottype.Build`, retaining the same repository,
  `release.yml`, and protected `release` environment.
- [NuGet publication](https://github.com/DouglasCleghorn/dottype/actions/runs/34048160727)
  succeeded for both 0.2.0 packages and the CLI symbols. NuGet performs validation
  and indexing after accepting uploads; install/restore availability follows.

The publishing jobs use the same artifacts validated by their respective runs.
Existing versioned assets and the published 0.1.0 package were preserved.

The live CI and release workflow results are authoritative for publication
status. See [RELEASING.md](RELEASING.md) for the procedure and
[PUBLISHING-CHECKLIST.md](PUBLISHING-CHECKLIST.md) for review scope.
