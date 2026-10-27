# Release security and future private development

Status: proposal for owner review. No migration, proprietary licence adoption,
signing service purchase or release publication is performed by these files.
The root MIT LICENSE and historical releases remain unchanged.

## Repository boundary

Use a **private development repository** for future unpublished additions and a
**public distribution repository** for ZIP downloads, verification instructions,
release notes and required legal files. The existing public repository can remain
the distribution home after an approved transition, preserving historical MIT
source/tags; a new downloads repository is an alternative. Choose names before
creating either repository. Do not make the existing public history private as
a substitute for protecting future code.

Never push proprietary branches to the public remote. Private repository access
must be limited to trusted maintainers, with MFA, periodic access review and no
shared credentials. Keep source archives, symbols, source maps, build logs, crash
dumps, signing credentials and private test reports inside the private boundary.
GitHub's public automatic tag source archives must contain only public
distribution documentation, never the private development commit.

## Actions design after approval

1. **Private validation job:** read-only contents token, no release/signing secrets,
   checkout credentials disabled, full verified action SHAs, locked NuGet restore,
   advisory/secret/code scans and tests. Never run untrusted fork code with secrets
   or use pull_request_target to build a contributor's source. Prefer disposable
   hosted runners. Restrict caches and artifacts to private workflows; do not
   cache secrets. Public validation in this repository is read-only and does not
   publish downloads.
2. **Private build job:** build the reviewed exact commit on a clean Windows runner.
   Retain detailed logs, dependency inventory, provenance, any private PDBs and
   obfuscation maps privately with limited retention. `scripts/package.ps1`
   performs local packaging, tests and unsigned integrity checks. It never
   publishes. `policy/release-payload.json` defines the reviewed legal/vendor
   payload; `scripts/Test-ReleasePayload.ps1` rejects extra files and changed
   required material. Its only source exception is the required upstream GPL
   smartmontools source archive and drive database.
3. **Protected signing job:** require the owner-approved release environment and
   reviewed main commit. Sign the final application EXE after all binary changes
   using a legitimate CA/HSM signing service; use narrowly scoped OIDC where the
   provider supports it. Do not import a PFX from source control or an artifact.
   Keep any signing permission exclusively in this job. Do not modify or re-sign
   the pinned third-party smartctl executable. Verify trust chain, timestamp and
   the explicitly approved publisher identity. Regenerate PAYLOAD-SHA256.json,
   ZIP and SHA256SUMS.txt **after** signing; otherwise the hashes describe an old
   executable. Re-extract, check exact payload, compare the EXE to the ZIP and run
   `Verify-Release.ps1 -RequireSignature -ExpectedSignerThumbprint <trusted-value>`.
   A hash manifest alone does not prove publisher identity.
4. **Distribution approval:** approve the precise final ZIP digest, licence
   contents and sanitized notes. Transfer only these named assets from the same
   run; pin the artifact ID and verify its recorded digest. Never download an
   artifact by an attacker-controlled workflow/run or broad wildcard. A separate
   private publisher job should have no source checkout or source artifacts and
   should never execute downloaded content with its publication credential.
5. **Public draft job:** mint a short-lived GitHub App installation token restricted
   to contents:write on the one public download repository. The private build
   token must not grant this permission. Keep the App private key as a protected
   environment secret (not a code-signing key), allow only the installation's
   selected repository, and revoke/rotate on incident. Use fixed repository and
   tag values, validate version input, and create a draft release on a public
   distribution-only commit. Upload only exact ZIP/checksum/approved legal or
   provenance filenames. No source checkout, symbols, private commit identifiers,
   logs, debug archives or sensitive build files cross this boundary.
6. **Final gate:** owner reviews the actual draft assets before publication. Enable
   supported immutable future releases after reviewing its irreversible effects;
   finish all assets in draft first. Do not overwrite historical assets. Revoke
   the installation token at job completion. Protect release tags as well as main.

Environment review availability depends on the account/plan. If a private
environment cannot enforce a required independent reviewer, use a manual
owner-controlled signing/publication procedure and do not describe it as an
independently enforced approval gate. Do not add an automatic public publisher
until this boundary has been approved and exercised with a nonpublic trial.

GitHub artifact attestations may disclose repository, workflow, commit and event
metadata. Review private-repository feature availability and metadata before
exposing any attestation. A public download workflow can attest receipt/digest,
but must not claim it built source that it did not build. Keep detailed private
build provenance private unless expressly approved for disclosure.

## Verification examples

From PowerShell 7.4 or later, in a trusted copy of the verification tools:

```powershell
./scripts/Verify-Release.ps1 -Archive ./download.zip -ChecksumFile ./SHA256SUMS.txt
# Prefer a digest received through a separately trusted channel:
./scripts/Verify-Release.ps1 -Archive ./download.zip -ExpectedSha256 <64-hex-digest>
# Only for a genuinely signed future build, using the trusted certificate value:
./scripts/Verify-Release.ps1 -Archive ./download.zip -ChecksumFile ./SHA256SUMS.txt -Executable ./extracted/Atiq-Laptop-Inspector.exe -RequireSignature -ExpectedSignerThumbprint <40-hex-thumbprint>
```

The script never extracts a ZIP or installs a trust certificate. v1.0/v1.1 are
unsigned and must fail the signature-required check. A checksum downloaded from
the same compromised source could also be replaced; trust the source of the
expected digest. Certificate rotation requires an authenticated update to the
trusted identity. Strong-name signing is not Authenticode publisher validation.
For the built-in Windows PowerShell, `Get-FileHash ./download.zip -Algorithm SHA256`
also calculates a digest for manual comparison against a separately trusted
expected value. The portable application itself does not require PowerShell 7.

## Legal and branding boundary

Keep `Copyright © 2026 Atiq-Softwares.`, `Developed by Atiq-Softwares` and
`https://github.com/Atiq-Softwares` in the application and PDF/HTML reports.
Third-party notices remain separately identified. Future proprietary terms
must acknowledge inherited MIT material and third-party rights; they cannot
retroactively revoke the published MIT grants or restrict GPL component rights.
No licence server, tracking, device binding or online activation is planned.

References: [GitHub Actions security](https://docs.github.com/en/actions/how-tos/secure-your-work),
[attestation metadata](https://docs.github.com/en/actions/concepts/security/artifact-attestations),
[Windows signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options),
[MIT terms](https://opensource.org/license/mit).
