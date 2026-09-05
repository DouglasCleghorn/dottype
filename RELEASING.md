# Releasing dottype

The release workflow produces two distribution forms from the same tagged source:

- a `dottype` .NET global-tool package and symbol package for nuget.org;
- self-contained `win-x64` and `win-arm64` portable executables plus a WinGet 1.10 multi-file manifest set.

## One-time setup

1. Confirm the public repository and `origin` are `https://github.com/DouglasCleghorn/dottype`.
2. Confirm the protected GitHub environment named `release` requires review and allows `v*` tags.
3. Confirm `dottype` is available to your NuGet account. Configure a trusted-publishing policy with repository owner `DouglasCleghorn`, repository `dottype`, workflow filename **`release.yml`**, and environment `release`. Select the package scope needed for a first publish; package ownership/name availability must be confirmed on nuget.org.
4. Add the nuget.org profile name—not an email address—as the GitHub environment variable `NUGET_USER`.
5. For WinGet's first release, submit the generated multi-file manifest directory to `microsoft/winget-pkgs`. Once accepted, later versions may be submitted with `wingetcreate update DouglasCleghorn.dottype`.

## Release checklist

1. Replace `TBD` with the release date and move relevant entries from `Unreleased` in `CHANGELOG.md`.
2. Confirm `Version` in `src/dottype/dottype.csproj` matches the intended release.
3. Run:

   ```powershell
   dotnet restore dottype.slnx --locked-mode
   dotnet test dottype.slnx -c Release --no-restore
   dotnet package list --project src/dottype/dottype.csproj --include-transitive --vulnerable --no-restore
   dotnet pack src/dottype/dottype.csproj -c Release --no-restore -p:RuntimeIdentifiers= -o artifacts/package
   ./eng/verify-package.ps1
   ./eng/publish-portables.ps1
   ./eng/smoke-test.ps1 -Executable artifacts/portable/release/dottype-win-x64.exe
   ./eng/new-winget-manifests.ps1 `
     -Version 0.1.0 `
     -X64Installer artifacts/portable/release/dottype-win-x64.exe `
     -Arm64Installer artifacts/portable/release/dottype-win-arm64.exe
   ./eng/verify-winget-manifests.ps1
   ```

4. Commit the release state and create an annotated `v<version>` tag.
5. Run the `Release` workflow against that tag with both publish switches initially disabled.
6. Inspect the uploaded release candidate. Re-run with the desired publish switches after approval.
7. Submit the generated WinGet manifest set after the GitHub release exists. The manifest URLs intentionally point to the versioned GitHub release assets.

The release workflow requires a selected `v<version>` tag, an identical project
version, and a dated changelog entry. It currently accepts stable numeric versions
such as `0.1.0`; this is an early public CLI release, not a 1.0 API commitment.
Run the workflow from a tag only after the same commit passes CI on Windows,
Linux, and macOS. Update version literals in CI and script defaults when bumping
the project version.

Before the first WinGet submission, run `winget validate --manifest <directory>`
on a machine with WinGet installed and test installation/uninstallation using
the published assets. The workflow's fallback hash checks are not a substitute
for WinGet validation. Smoke-test ARM64 on an ARM64 machine as well.
Schema 1.10 is used for compatibility with the WinGet client on hosted Windows
runners; no 1.12-only fields are required. Portable installers omit `Scope`.

Publish the executables together with the generated application and .NET runtime
license/notice assets. Submit manifests generated from those exact release
executables; rebuilding locally can change hashes. Do not overwrite existing
versioned assets after submitting a WinGet manifest.

For consumers wanting a library, first extract a separate core project with a
documented public API and contract tests. The current `PackAsTool` package is
installed with `dotnet tool install`, not `dotnet add package`.

References: [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
[creating .NET tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create),
[WinGet submission](https://learn.microsoft.com/en-us/windows/package-manager/package/repository).
