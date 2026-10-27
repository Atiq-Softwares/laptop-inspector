# Validation record

## Automated checks

The release test suite contains **44 passing test cases** covering:

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
results and package artifacts for each Windows run.

## Local validation performed

- Compiled the solution on Windows with the .NET 10 SDK.
- Ran the 44 test cases successfully.
- Launched the real WPF application and completed a real local Windows-provider
  hardware scan; no real hardware report or screenshot is published.
- Rendered and visually reviewed all six pages of the synthetic sample PDF.
- Independently extracted PDF text to verify branding and footer link annotations
  on every sample page.
- Reviewed the actual native interface and corrected theme inheritance and
  small-screen sizing discovered during that review.

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
and validates the ZIP before publishing a tagged release.

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.
