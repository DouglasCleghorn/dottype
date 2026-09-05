# Release readiness — 2026-09-05

The implementation passes local release-candidate checks. Complete the external
checks below before publishing 0.1.0. No package, GitHub release, or WinGet
submission was published during this review.

## Verified locally

- Locked dependency restore and Release build using .NET SDK 10.0.400 on Windows x64.
- 43 passing tests on Windows, plus a Linux/macOS symlink regression test, covering schema inference, gzip/Brotli files for JSON/CSV/Parquet,
  binary stdin, suffix case, explicit overrides, default names, output files,
  malformed/truncated compression, and failure exit codes.
- Generated C# compiles with Roslyn; System.Text.Json serialization restores the
  original names for repaired identifiers, collisions, keywords, and Unicode.
- Current NuGet vulnerability query reports no known vulnerable direct or
  transitive packages in the CLI project. Restore audits all dependencies.
- NuGet package and portable PDB archive pass content verification. Test/compiler
  assemblies and loose source files are excluded from the tool package.
- The locally installed NuGet tool generates POCOs from plain/gzip/Brotli files
  and raw binary stdin through real child processes.
- Self-contained Windows x64 and ARM64 executables build; x64 passes the same
  subprocess smoke tests. Application and runtime license notices are included
  among the release assets.
- All three WinGet manifests use Microsoft's published 1.10 JSON schemas;
  installer SHA-256 values match the freshly published local executables.
- Both workflow YAML files and their 25 command blocks parse successfully;
  all release PowerShell scripts parse. Formatting verification passes.
- Gitleaks and an additional privacy scan found no secrets, private emails, or
  local-user paths in the publication files or pre-commit Git object database.
  The initial database contained blobs/trees for local tooling snapshots, but
  no commits, tags, or normal branch history. Only the reviewed branch is intended
  for publication; tooling refs and ignored local artifacts are excluded.
- Portable PDB tests verify neutral `/_/` document paths and no embedded
  untracked sources. Public author/copyright names are retained intentionally.
- Workflow actions are pinned to verified commits; checkout credentials are not
  persisted. Dependency-update configuration and private security-reporting
  instructions are included.

## Release blockers and external checks

The public repository is created and the reviewed branch is pushed. A committed
local build verifies Source Link with zero warnings. The Windows/Linux/macOS
[CI matrix for `4d2dce4`](https://github.com/DouglasCleghorn/dottype/actions/runs/33980686946)
passed, including NuGet package verification and native WinGet manifest validation.

1. Publish artifacts built from the final tagged commit so Source Link receives
   the correct commit and remote. Do not publish old pre-commit artifacts.
2. Run the ARM64 executable on ARM64 hardware; cross-publishing alone does not
   verify execution on that architecture.
3. Confirm NuGet package-name availability/ownership and configure the protected
   `release` environment, `NUGET_USER`, and NuGet trusted-publishing policy.
   The policy's workflow filename is `release.yml`.
   The GitHub release environment already requires maintainer review and `v*`
   tags. Secret scanning, push protection, vulnerability alerts, private reporting,
   and read-only workflow defaults have been enabled.
4. Replace the 0.1.0 changelog's `TBD` with the intended release date, then create
   `v0.1.0`. The release workflow deliberately rejects an undated release entry
   or a project/tag/version mismatch. Start with publishing disabled.
5. Native `winget validate` passed in Windows CI. Perform install/uninstall
   validation on Windows using the released assets before submitting the first
   manifest PR. Manifest validation does not test WinGet installer behavior.

## Scope of the first release

Ship this as a .NET global CLI tool (`dotnet tool install --global dottype`) and
a Windows portable tool. It is not a referenceable NuGet library API today.
If a library is needed, extract a separate core package with public contracts
before making an API stability promise.

The README documents memory behavior, supported compression wrappers, binary
stdin, serializer metadata limits, duplicate CSV headers, and Explorer drop
behavior. JSON/CSV inference and externally compressed Parquet require memory
proportional to decompressed input size; this release is not a bounded-memory
streaming schema inference engine.

See [RELEASING.md](RELEASING.md) for the sequence and official publishing references.
The broader privacy, history, supply-chain, and publication review is recorded in
[PUBLISHING-CHECKLIST.md](PUBLISHING-CHECKLIST.md).
