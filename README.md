<p align="center"><strong>ATIQ-SOFTWARES</strong></p>
<h1 align="center">Atiq Laptop Inspector</h1>
<p align="center">Know the hardware. Understand the warnings. Keep a clear report.</p>
<p align="center">
  <a href="https://github.com/Atiq-Softwares/laptop-inspector/releases/latest/download/Atiq-Laptop-Inspector-v1.0-Windows-x64.zip"><img alt="Download portable Windows x64 ZIP" src="https://img.shields.io/badge/DOWNLOAD-Windows_x64_ZIP-087f79?style=for-the-badge&logo=windows" /></a>
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/License-MIT-16283c?style=for-the-badge" /></a>
</p>

A native **C# / WPF** Windows hardware inspection application with an offline,
portable EXE, evidence-labelled assessments and professional PDF, HTML and JSON
reports. Developed and published by **Atiq-Softwares**.

**Download → Extract → Run EXE → Scan → Get report**

![Atiq Laptop Inspector dashboard; synthetic sample data](docs/images/dashboard.png)

> This screenshot and the downloadable sample reports contain clearly labelled
> synthetic data. They are not measurements from a real laptop.

## Get started

1. [Download the latest Windows x64 ZIP](https://github.com/Atiq-Softwares/laptop-inspector/releases/latest).
2. Extract **all** files to a normal local folder.
3. Double-click **`Atiq-Laptop-Inspector.exe`**.
4. Click **Start Full Inspection** and wait for the scan to finish.
5. Review **Hardware details** and complete **Manual checks**.
6. Open **Export reports** and save a PDF, HTML or JSON report.

**No installation, .NET installation, PowerShell commands, administrator account,
registration or internet connection is required for normal use.** Inspection time
depends on the Windows providers. A diagnostic worker is cancelled after three
minutes if providers stop responding. A scan can also be cancelled manually.

The EXE is **unsigned**. Windows Defender, SmartScreen or organizational application
controls may warn or block execution. Review the source and release checksum and
follow your organization's policy. Do not disable protection or bypass a block.

## What it inspects

| Area | Available information and honest limits |
| --- | --- |
| CPU | Model, manufacturer, cores, logical processors, reported clocks, L2/L3 cache; limited Intel generation inference and general-use suitability estimate. No benchmark. |
| Memory | Installed capacity, module manufacturer/part, DDR type, configured/rated speed, form factor and firmware slot count. No inferred channel mode or integrity claim. |
| Storage | Physical disk model, media, bus, capacity, provider health; matched operating hours, wear, temperature and uncorrected errors where exposed. Legacy ATA SMART records are explicitly unmapped and vendor-specific. |
| Battery | Matched design/full-charge capacity, cycles if available, calculated capacity retention. Missing/invalid data stays unverified. |
| Display & GPU | Graphics adapter, driver, current resolution/refresh if reported. Pixel and GPU functional tests remain manual. |
| Board & BIOS | Manufacturer, board product/version and BIOS version/date. No firmware-security inference. |
| Windows & security | OS version/build, architecture, last boot, Secure Boot, available TPM/Defender/encryption configuration. No malware or online update scan. |
| Network | Physical network adapters, link states and supported local Bluetooth radio names. No internet or pairing test. |
| Sensors | Available ACPI thermal zones, clearly distinguished from CPU/GPU package temperature. No stress test or kernel driver. |

Guided manual checks cover the **keyboard, display, webcam, microphone, speakers,
touchpad, ports/connectivity and physical condition**. The app includes an in-memory
keyboard checker and a full-screen six-color display helper. Camera and audio
guides use Windows' own tools; the app does not record you.

## Read the results correctly

- 🟢 **GOOD**: the available reading meets the stated app policy, within its scope.
- 🟡 **CAUTION**: review the explanation and consider service or upgrades.
- 🔴 **CRITICAL**: investigate a critical reading or failed manual check.
- ⚪ **NOT VERIFIED**: functional condition or a required measurement is unknown.

Every measurement includes its **source** and an evidence label: **Measured**,
**Estimated**, **Unavailable** or **Manual**. “Measured” means Windows/firmware
reported; the app does not independently calibrate hardware sensors.

There is **no invented overall health percentage**. RAM capacity is not a memory
integrity test. CPU inventory is not a benchmark. Battery retention is not remaining
charge or a safety test. Storage “Healthy” is not a reliability guarantee. Unverified
components and pending manual checks prevent an overall GOOD assessment unless
the result is already a higher-priority warning or critical finding.

[Policy and architecture details](docs/ARCHITECTURE.md) · [Complete limitations](docs/LIMITATIONS.md)

## Reports worth keeping

![First page of the synthetic sample PDF report](docs/images/report-cover.png)

- **PDF**: A4 layout, executive summary, component findings and evidence tables,
  manual results, warnings, service recommendations, coverage limits, branded
  headers/footers, page numbers and clickable GitHub links.
- **HTML**: a self-contained offline file with attractive cards, tables and print CSS.
- **JSON**: typed evidence, component evaluations, limitations, manual outcomes and
  the overall assessment for further processing.

All exports include the inspection timestamp with UTC offset, manufacturer/model,
buying-suitability guidance and an explicit explanation of what remains unknown.
Report files are not digitally signed and do not guarantee future reliability.

[Download a synthetic sample PDF](https://github.com/Atiq-Softwares/laptop-inspector/releases/latest/download/Sample-Inspection-Report.pdf)

## Privacy and security

Everything runs locally. There is no telemetry, automatic upload, account, network
request during scanning/export, persistent agent or background service. GitHub links
open only when clicked. The app reads Windows providers and does not change system
settings, install drivers, flash firmware, erase data or run stress tests.

No passwords, tokens, serial numbers, Windows product keys, usernames, computer
names, IP/MAC addresses, SSIDs or personal files are collected. Matching device
identifiers remain internal and are not exported. Manual notes are **excluded from
exports by default**; you may explicitly include them. Review free text before
sharing any report. Exports remain on disk until you choose to remove or share them.

## Compatibility

The portable release targets **Windows 10 22H2 x64 and Windows 11 x64**. The full
.NET 10 runtime is bundled; users do not install it. Runtime native libraries can
be extracted into Windows' temporary folder on first launch. Keep all legal notices
and the licenses folder with the EXE when redistributing the package.

Physical hardware compatibility varies by firmware, drivers, Windows permissions,
virtualization and USB/RAID bridges. Unsupported diagnostics remain explicitly
unavailable. No elevation is requested. 32-bit and native ARM64 releases are not
provided in v1.0. Use a Windows release receiving appropriate security updates.

## Verify your download

The release includes **SHA256SUMS.txt**. Compare the ZIP with a trusted local
SHA-256 tool. Optional PowerShell verification for technical users:

```powershell
Get-FileHash .\Atiq-Laptop-Inspector-v1.0-Windows-x64.zip -Algorithm SHA256
```

## Build and test from source

Developers need Windows, Git and the **.NET 10 SDK**. Normal app users need none
of these tools.

```powershell
git clone https://github.com/Atiq-Softwares/laptop-inspector.git
cd laptop-inspector
dotnet test LaptopInspector.slnx -c Release
./scripts/package.ps1
```

Packaging runs automated tests, publishes a self-contained single-file EXE,
checks real WPF startup, generates sample PDF/HTML/JSON, builds a ZIP, extracts
it to a fresh folder, checks the extracted EXE and writes SHA256SUMS.txt.

The [Windows GitHub Actions workflow](.github/workflows/release.yml) runs for main,
pull requests and tags. Version tags publish release assets. Test TRX and portable
packages are available as workflow artifacts. [Validation details](docs/TESTING.md)
distinguish automated checks from unperformed physical laptop qualification.

## Source layout

```text
src/Inspector.Core    Models, evaluation, SMART parsing, privacy and sample fixture
src/Inspector.App     Native WPF UI, Windows collector and PDF/HTML/JSON exporters
tests/Inspector.Tests Boundary, missing-value, privacy, parser and report tests
scripts/package.ps1  Publish, extracted-startup verification, ZIP and SHA-256
docs/                Architecture, limitations, testing and release documentation
licenses/            Exact upstream legal notices for shipped dependencies
```

## License and publisher

**Copyright © 2026 Atiq-Softwares.**  
**Developed by Atiq-Softwares**  
**GitHub: https://github.com/Atiq-Softwares**

Original code and graphics: [MIT License](LICENSE). Dependencies retain their
licenses; see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and `licenses/`.
