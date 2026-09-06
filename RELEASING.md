# Releasing dottype

The release workflow produces these distribution forms from the same tagged source:

- a `dottype` .NET global-tool package and symbol package for nuget.org;
- a `dottype.Build` NuGet package for compilation and editor integration;
- self-contained `win-x64` and `win-arm64` portable executables.

## One-time setup

1. Confirm the public repository and `origin` are `https://github.com/DouglasCleghorn/dottype`.
2. Confirm the protected GitHub environment named `release` requires review and allows `v*` tags.
3. Confirm `dottype` and `dottype.Build` are available to your NuGet account. Configure a trusted-publishing policy with repository owner `DouglasCleghorn`, repository `dottype`, workflow filename **`release.yml`**, and environment `release`. Include both package IDs in the policy scope; package ownership/name availability must be confirmed on nuget.org.
4. Add the nuget.org profile name—not an email address—as the GitHub environment variable `NUGET_USER`.

## Release checklist

1. Replace `TBD` with the release date and move relevant entries from `Unreleased` in `CHANGELOG.md`.
2. Confirm `Version` in both `src/dottype/dottype.csproj` and `src/dottype.Build/dottype.Build.csproj` matches the intended release. Update package-version literals in the consumer docs, examples, workflow inputs, and script defaults. Never reuse an already published package version.
3. Run:

   ```powershell
   dotnet restore dottype.slnx --locked-mode
   dotnet test --solution dottype.slnx -c Release --no-restore --minimum-expected-tests 1
   dotnet package list --project src/dottype/dottype.csproj --include-transitive --vulnerable --no-restore
   dotnet pack src/dottype/dottype.csproj -c Release --no-restore -p:RuntimeIdentifiers= -o artifacts/package
   ./eng/verify-package.ps1
   dotnet pack src/dottype.Build/dottype.Build.csproj -c Release --no-restore -o artifacts/package
   ./eng/verify-build-package.ps1
   ./eng/verify-visual-studio.ps1
   ./eng/publish-portables.ps1
   ./eng/smoke-test.ps1 -Executable artifacts/portable/release/dottype-win-x64.exe
   ```

4. Commit the release state and create an annotated `v<version>` tag.
5. Run the `Release` workflow against that tag with both publish switches initially disabled.
6. Inspect the uploaded release candidate and successful native ARM64 smoke test. Re-run with the desired publish switches. The protected `release` environment gates publication; the publishing job downloads the exact artifacts tested by that run without rebuilding them.

The release workflow requires a selected `v<version>` tag, an identical project
version, and a dated changelog entry. It currently accepts stable numeric versions
such as `0.1.0`; this is an early public CLI release, not a 1.0 API commitment.
Run the workflow from a tag only after the same commit passes CI on Windows,
Linux, and macOS. Update version literals in CI and script defaults when bumping
the project version.

The release workflow smoke-tests the uploaded ARM64 executable on a `windows-11-arm`
runner before allowing publication. The x64 executable is tested in the build job.

Publish the executables together with the generated application and .NET runtime
license/notice assets. Verify checksums against those exact release executables;
rebuilding locally can change hashes. Do not overwrite published versioned assets.

For consumers wanting a library, first extract a separate core project with a
documented public API and contract tests. The current `PackAsTool` package is
installed with `dotnet tool install`. Use a `PackageReference` to `dottype.Build`
for build integration; it does not expose a public runtime inference API.

## Documentation discovery

Each package uses its own listing README from `docs/nuget/`, packed as README.md.
Keep these quick starts aligned with USAGE.md and update their version literals
when releasing. Listing links must be absolute HTTPS URLs so they work on nuget.org.
Both packages embed `assets/icon.png` as icon.png; `assets/icon.svg` is its editable
source. Keep the PNG at 128 × 128 and under 1 MB. Publish the icon and linked docs
to the public repository before publishing a package so README images resolve.
Preview each package's README and icon on nuget.org before completing a release.
Existing published versions require a new package version for metadata changes.

Both packages also include USAGE.md and llms.txt. Keep USAGE.md as the
canonical argument reference and llms.txt as a short index. When hosting these
docs, publish all three files together so the relative links resolve. If the docs
live under a subpath, place llms.txt in that same directory. Repository agents
start at AGENTS.md; it should point to consumer docs rather than repeat them.

## Local preview versions

Do not overwrite a package version already restored by consumers. Pack a unique
preview version, then update the consuming package reference or directive:

```powershell
dotnet pack src/dottype.Build/dottype.Build.csproj -c Release -p:PackageVersion=0.3.0-local.1 -o artifacts/package
./eng/verify-build-package.ps1 -ExpectedVersion 0.3.0-local.1
./eng/verify-visual-studio.ps1 -ExpectedVersion 0.3.0-local.1
```

Increment the preview suffix for subsequent builds. The public release workflow
continues to require a stable version and its matching tag.

References: [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
[creating .NET tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create).
