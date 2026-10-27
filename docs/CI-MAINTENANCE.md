# Windows verification and dependency updates

The protected public `main` branch requires the exact GitHub Actions check
**`windows`** (app ID 15368), with an up-to-date branch. Its workflow is
`.github/workflows/release.yml`, displayed as **Windows security validation**.
The workflow must stay active. The job ID and explicit job name are both
`windows`. A workflow display name is not the required check name.

All pull requests run the job; there are no path filters, approval-dependent
job conditions or matrix suffixes that could leave the required check absent.
Main/security pushes and manual dispatch also validate. There is no tag trigger,
release command, publication credential or contents:write permission. Only test
results are uploaded. Keep all actions pinned and checkout credentials disabled.

The previously disabled workflow was restored before v1.1.1 publication. That
release's PR and main checks passed. Do not disable protection or introduce a
synthetic success status to work around a failing build. After re-enabling a
workflow, an old PR may need a synchronize/reopen event so its required PR check
runs; a manual dispatch alone did not satisfy the original PR's merge check.

## NU1004 on a dependency PR

The live System.Management and PDFsharp dependency PRs changed the app manifest
and app lockfile but left `tests/Inspector.Tests/packages.lock.json` describing
the old project-reference dependency graph. The locked restore correctly fails
with NU1004. This is different from a missing or disabled `windows` check.

After reviewing the proposed dependency version and its terms, use the pinned
SDK on a local checkout of the dependency PR:

```powershell
./scripts/Update-DependencyLocks.ps1
git diff -- '**/packages.lock.json'
./scripts/package.ps1
```

The maintainer helper refreshes the whole solution and win-x64 publish graph,
then verifies both with locked restores. It is rejected in ordinary CI/Actions
environments. Review every changed lockfile and package hash; check advisory,
licence and legal-notice implications before committing them to the same PR.
Do not assume updating one app lockfile updates the test project's graph.

CI and packaging call `Restore-LockedDependencies.ps1`, which always uses
`--locked-mode --force` and explains the maintainer action on failure. `--force`
rechecks the assets graph even in a previously restored workspace; it is not
`--force-evaluate` and does not authorize a lock refresh. CI must never
invoke the update helper, remove the lockfiles, use a force-evaluated restore,
ignore the error, or auto-commit newly trusted hashes. Retain default read-only
tokens and do not run dependency PR source with a release/signing credential.

The prepared lock-only repairs for existing PRs are review artifacts, not approval
to adopt their dependency versions. PDFsharp's later AGPL/commercial transition
remains held for separate licence review. Keep release notices and the payload
policy synchronized with any approved package changes before distribution.

Reference: [NuGet locking dependencies](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies).
