# Architecture and diagnostic contract

`Inspector.Core` owns report models, policy evaluation, legacy ATA table parsing,
privacy filtering and synthetic fixtures. `Inspector.App` is the native WPF
interface, Windows collector and report engine. `Inspector.Tests` validates
boundaries, missing values, SMART bytes, privacy and report structure.

The desktop process starts the same EXE as a read-only `--collect` worker.
The worker writes progress to redirected stderr and a typed JSON inspection to
redirected stdout. No intermediate hardware file is required. The parent kills
its own worker on cancellation or after three minutes. Each WMI enumeration also
has a four-second provider timeout, although Windows does not honor every timeout
in every provider. There are no persistent agents or scheduled tasks.

## Evidence and ratings

Measured means reported by a Windows provider, not independently calibrated.
Estimated means derived, for example battery capacity retention or a limited
CPU model-name inference. Unavailable means no usable reading. Manual outcomes
are entered by an operator and never silently promoted to measurements.

Battery retention is full-charge/design capacity, not current charge level.
Invalid, zero, missing and implausible (>120%) ratios are withheld. Below 80%
prompts capacity-service review; below 50% is labelled critical capacity loss.
These are app policies, not manufacturer failure or safety predictions.

Memory assessment only addresses capacity: less than 8 GiB prompts review.
It does not verify memory integrity, channel mode, slot availability or performance.

Storage uses MSFT_PhysicalDisk health and exact DeviceId-matched reliability
counters. Wear >=80% prompts review; >=100% is the provider's estimated wear
limit. Provider Unhealthy or a legacy SMART failure prediction is critical.
Uncorrected reported errors prompt review. Legacy ATA records stay explicitly
unmapped; vendor raw attributes are displayed without inventing units or meaning.

Inventory-only CPU, board, display and network records remain NOT VERIFIED for
functional health. A warning or failed manual check takes precedence in the
overall assessment. Otherwise any unknown component, incomplete scan or pending
manual test prevents a GOOD overall assessment. Counts are component records,
not a percentage of hardware verified or a reliability score.

## Privacy boundary

WMI queries use explicit property lists. Matching InstanceName/DeviceId values
remain internal to collection and are never copied into report models. No
passwords, tokens, serials, product keys, account names, addresses, SSIDs or files
are queried. Bluetooth output is limited to radio drivers, excluding paired-device
names. Exports additionally remove sensitive measurement labels and omit manual
notes by default. The collector does not accept report imports or arbitrary WMI
queries. HTML encodes operator and hardware strings.

## Report generation

The app generates PDF directly with pinned MIT-licensed PDFsharp-WPF 6.2.3.
No browser, printer driver, server, network or external program is required.
PDFs have A4 pages, wrapped rows, repeated branded footers, page numbers and
GitHub link annotations. HTML is one self-contained file with print CSS. JSON
contains typed evidence, limitations, manual outcomes and the calculated overall
assessment. Explicit note inclusion applies to all three formats.

## Build and portability

The project targets .NET 10 LTS. Publishing uses self-contained win-x64,
single-file bundling, embedded debug information and native-library extraction.
Trimming is disabled because WPF/WMI require runtime metadata. The ZIP also
contains documentation and licenses. It is portable, although .NET can extract
native libraries into the user's temporary directory at startup.

Developer automation flags: `--smoke-test` builds and renders the real WPF window
then exits; `--demo` loads an explicitly synthetic fixture; `--screenshot <path>`
with smoke-test saves the rendered window; `--sample-report <folder>` generates
synthetic examples; `--scan-report <folder>` runs a real local scan and exports
it. Normal users need no command-line flags.
