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
- All three WinGet manifests pass Microsoft's published 1.12 JSON schemas;
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

1. Build the committed candidate from `DouglasCleghorn/dottype` so Source Link
   receives the real commit and remote. Do not publish old pre-commit artifacts.
2. Run the Windows/Linux/macOS CI matrix for that commit. Only Windows x64
   execution has been verified here. Run the ARM64 executable on ARM64 hardware.
3. Confirm NuGet package-name availability/ownership and configure the protected
   `release` environment, `NUGET_USER`, and NuGet trusted-publishing policy.
   The policy's workflow filename is `release.yml`.
4. Replace the 0.1.0 changelog's `TBD` with the intended release date, then create
   `v0.1.0`. The release workflow deliberately rejects an undated release entry
   or a project/tag/version mismatch. Start with publishing disabled.
5. WinGet is unavailable locally. Run `winget validate` and install/uninstall
   validation on Windows using the released assets before submitting the first
   manifest PR. JSON-schema/hash checks do not test WinGet installer behavior.

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
