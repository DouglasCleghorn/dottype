# Release readiness — 2026-09-06

## dottype 0.2.0 candidate

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
- Gitleaks reports no secrets in the 86-file publication snapshot or outgoing
  branch history. A separate source path/email scan reports no findings.
- The additional VS Code UI check could not start because VS Code's updater
  holds its startup mutex. Editor generator unit tests pass; the earlier tested
  extension versions remain documented in USAGE.md.

## Publication gates

1. Push the reviewed source and require successful Windows/Linux/macOS CI for
   the release commit before dispatching its annotated `v0.2.0` tag.
2. Run the Release workflow with publishing disabled and inspect its artifacts.
   It now executes the uploaded ARM64 binary on a native Windows ARM64 runner.
3. Add the exact `dottype.Build` package ID to the existing NuGet trusted policy,
   retaining the same repository, `release.yml`, and protected `release`
   environment. The policy currently permits only `dottype`.
4. Publish through the protected environment. The publishing job downloads the
   same artifacts validated by that run and uses the versioned changelog for
   GitHub release notes. Do not publish old local artifacts or overwrite an
   existing published version.

The live CI and release workflow results are authoritative for publication
status. See [RELEASING.md](RELEASING.md) for the procedure and
[PUBLISHING-CHECKLIST.md](PUBLISHING-CHECKLIST.md) for review scope.
