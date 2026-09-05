# Initial public release checklist

Use this checklist when opening the repository and publishing a new package.
Repository publication and NuGet/WinGet publication are separate operations.

## Repository contents and history

- Review every tracked/unignored file, including hidden files, fixtures, scripts,
  lock files, project metadata, documentation, and dependency notices.
- Scan credentials and keys with a secret scanner. Also inspect private email
  addresses, personal datasets, local usernames/paths, internal URLs, connection
  strings, and generated files; secret scanners do not cover every privacy risk.
- Inspect all branches, tags, reflogs, dangling objects, commit messages, and
  author/committer identities. Review remote history separately if it exists.
  Removing a working-tree file does not remove it from history.
- Use a GitHub no-reply address for commits. Public author and copyright names
  and required third-party attributions are intentional.
- Check the staged file list and exact outgoing commits. Push only the intended
  branch, never `--mirror` or internal tooling refs. Keep build outputs, local
  configuration, keys, audit reports, and local datasets ignored.
- If credentials are found, revoke/rotate them before history cleanup. Review
  existing forks, caches, release assets, Actions logs/artifacts, issues, PRs,
  wikis, and Pages before changing an existing private repository's visibility.

## Package contents and behavior

- Restore pinned/locked dependencies and audit both direct and transitive
  dependencies for vulnerabilities. Review license obligations and bundled
  native/runtime notices.
- Run tests and real CLI smoke tests on Windows, Linux, and macOS. Cover file
  and binary stdin, gzip/Brotli corruption/truncation, output handling, format
  inference, valid generated C#, and reversible serialization names.
- Inspect the actual `.nupkg`, `.snupkg`, DLLs, and portable release payloads.
  Exclude tests, compiler tooling, private source, machine paths, and local
  credentials. Verify neutral PDB paths and commit-based Source Link.
- Install the package from a local-only feed and test that installed command.
  Build Windows x64/ARM64 portable assets and execute them on their target
  architectures. Verify installer hashes and official WinGet manifests.
- Confirm the package ID/ownership, command name, supported runtime, license,
  version, README links, and changelog are accurate. A `PackAsTool` package is
  a CLI distribution, not a public library API.

## GitHub and release automation

- Give CI read-only permissions. Do not expose publishing credentials to fork
  PRs. Pin actions to verified immutable commits and keep dependency updates on.
- Keep credentials out of checkout config and shell-script interpolation; use
  scoped environment variables and OIDC/short-lived NuGet credentials.
- Enable private vulnerability reporting, Dependabot alerts, secret scanning,
  and push protection where available. Review repository Actions permissions
  and collaborator access. Consider branch protection before accepting changes.
- Review the first CI run and artifacts after pushing. Public repositories make
  Actions logs accessible; never upload real input data to CI.
- Protect the `release` environment, configure `NUGET_USER`, and set the NuGet
  trusted publisher to owner `DouglasCleghorn`, repository `dottype`, workflow
  filename `release.yml`, and environment `release`.
- Finalize the release date, ensure project/tag/package versions agree, and
  run the manual release workflow with publishing disabled first.
- Publish reviewed artifacts to NuGet and GitHub. Submit WinGet only after its
  exact versioned assets exist and `winget validate` plus install/uninstall tests
  pass. Do not replace published assets with rebuilt files under the same version.

## Official guidance

- [GitHub: removing sensitive data](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)
- [GitHub: repository visibility](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility)
- [GitHub: secure Actions use](https://docs.github.com/en/actions/reference/security/secure-use)
- [NuGet: package authoring](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices)
- [NuGet: Source Link](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink)
- [NuGet: trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
- [WinGet: submission and validation](https://learn.microsoft.com/en-us/windows/package-manager/package/repository)
