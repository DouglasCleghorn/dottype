# dottype

To use dottype in an application, start with [USAGE.md](USAGE.md). It is the canonical reference for arguments, defaults, editor setup, and equivalent examples. [README.md](README.md) covers installation and the quick start.

When changing this repository:

- Always spell dottype in lowercase. See [CONTEXT.md](CONTEXT.md) for terminology.
- Keep generation arguments in the shared parser, `src/dottype/CliOptions.cs`; build integration adapts input paths and quoting in `src/dottype.Build/BuildOptions.cs`.
- Keep usage docs, the System.CommandLine definitions that generate CLI help, Visual Studio field help, and argument-parity tests aligned when changing the argument contract. `llms.txt` indexes the consumer docs and ships in both packages.
- Use the SDK pinned in `global.json`. Restore with `dotnet restore dottype.slnx --locked-mode`; test with `dotnet test --solution dottype.slnx -c Release --no-restore --minimum-expected-tests 1`.
- After changing central package versions, regenerate the entire solution's lock files with `dotnet restore dottype.slnx --force-evaluate`, commit all affected `packages.lock.json` files, and verify locked restore. Keep runtime packages referenced directly by the CLI tests so Dependabot updates their lock file when those packages change.
- Build-package changes are verified by `eng/verify-build-package.ps1` and `eng/verify-visual-studio.ps1` after packing the package. Generated files and caches live under `obj` or `artifacts`.
