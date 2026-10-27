# Validation record

## v1.1.1 security update

The current Release suite has **87 passing test cases**, including 23 new security
cases for streaming limits, invalid/deep SMART JSON, malformed models, overflow,
HTML escaping/CSP, transactional exports, hard-link replacement, native-tool
integrity/locking and absence of embedded debug symbols.

The final portable package is tested again before publication: engine integrity,
same-user helper transport, WPF views/themes, synthetic PDF/HTML/JSON generation,
ZIP extraction/startup, payload allowlist and SHA-256 verification. The Windows
Actions job validates the source with read-only permissions and retains test
results only. It never publishes on tags. Local Windows 11 x64 real inspection
and SMART read paths were verified; raw hardware reports stay private.

Windows 10, broader hardware and an actual UAC approval remain unqualified. A
same-user helper self-test does not test elevation. No online activation or
licence server was added. The release remains unsigned.

The records below describe earlier baseline validation.

## Automated checks / v1.1

New cases validate SMBIOS populated/empty/unknown devices, truncated tables, extended size/speed fields, part-number extraction without serial export, configured versus rated MT/s, estimated MHz, independent charge percentage, partial ATA identity, NVMe unit conversion, absent counters and private drive-index filtering.

The release test suite contains **64 passing test cases** covering:

- Battery capacity ratios, missing/zero/invalid readings and policy boundaries.
- Storage provider warnings, SMART failure precedence and wear thresholds.
- Six-byte ATA raw-value parsing and truncated/empty SMART tables.
- Supported and unsupported CPU naming, memory capacity and unavailable values.
- Overall assessment with pending checks, unknown components and manual failures.
- Sensitive measurement filtering, note opt-in and source-model immutability.
- HTML escaping, offline resources, report branding and clickable profile URLs.
- JSON schema fields, evidence labels, sample flags and overall assessment.
- A4 PDF pages, publisher metadata, link annotations and long-table pagination.

The packaging script additionally verifies WPF startup, all main views, both
themes, synthetic PDF/HTML/JSON generation and startup of the EXE extracted from
the actual release ZIP. Failures stop packaging. GitHub Actions retains TRX test
results only for each Windows run in the current workflow.

## Local validation performed

- Compiled the solution on Windows with the .NET 10 SDK.
- Ran the 64 test cases successfully.
- Launched the real WPF application and completed a real local Windows-provider
  hardware scan; no real hardware report or screenshot is published.
- Rendered and visually reviewed all six pages of the synthetic sample PDF.
- Independently extracted PDF text to verify branding and footer link annotations
  on every sample page.
- Reviewed the actual native interface and corrected theme inheritance and
  small-screen sizing discovered during that review.
- Switched dark/light themes in the native window, opened the guided manual page
  and verified that a key press is detected by the in-memory keyboard checker.
- Opened the full-screen display helper, verified color cycling and exited with
  Escape. This checks the helper's behavior, not physical display pixel quality.

Windows GitHub Actions run [40549302331](https://github.com/Atiq-Softwares/laptop-inspector/actions/runs/40549302331)
passed all 44 tests and verified the portable package on a clean Windows runner.
Current releases require explicit publication after source and final-package checks.

The repository and release use **synthetic sample hardware data only** for report
examples and documentation images.

## Qualification limits

A live scan on the current Windows environment is not a compatibility matrix,
physical laptop qualification, battery runtime test or hardware stress test.
No claim is made that every manufacturer, physical port, display, keyboard,
sensor, NVMe device or battery has been tested. Windows 10 execution and native
ARM64/32-bit builds have not been physically qualified. See LIMITATIONS.md.

For reproducible Windows runner results, see the repository's **Actions** tab.
The workflow builds from the checked-out source, runs the tests, generates reports
and validates the ZIP. Publication is a separate explicitly approved step.

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.

## v1.1 local verification

The expanded collector completed on the available Windows machine, identified
both populated memory devices, and returned actual SSD powered-on hours, cycles,
temperature and wear. Battery design/full/remaining capacity and charge now were
present. These observations validate the implemented read path on this machine,
not every laptop model. Real machine exports remain local and are not published.
The UI and report redesign use explicitly synthetic specimens for public samples.

The v1.1 package script also validates the pinned SMART engine/database hashes and a same-user, one-shot named-pipe diagnostic transaction without elevation. The interactive UAC approval itself has not been automated or bypassed. The nine-page synthetic PDF was rendered and visually reviewed.
