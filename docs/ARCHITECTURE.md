# Architecture / v1.1

The native WPF app targets .NET 10 LTS. Inspector.Core contains the typed model,
policy, SMBIOS Type 17 parser, SMART JSON allowlist and export privacy boundary.
Inspector.App contains Windows readers, the UI, optional privileged helper and
PDF/HTML/JSON exporters. Inspector.Tests contains 64 boundary and integration
fixture cases. No external service is required at runtime.

## Collection and cancellation

The normal UI starts the same executable as a non-elevated --collect worker.
JSON arrives over redirected stdout; progress arrives over stderr. The parent
cancels its worker after three minutes or an operator cancellation. WMI has
per-enumeration timeouts. smartctl reads have a 12-second timeout per drive.
The collector tries the bundled engine with ordinary process permissions first.

Advanced drive access is an explicit optional operation. An explanation precedes
the Windows runas permission prompt. A one-shot copy of the app reads restricted
drive counters and returns only allowlisted DriveFacts through a random local
named pipe using CurrentUserOnly. It receives no file paths or executable names
from a request. A cancelled parent closes the channel; the helper checks the
channel between drive requests. No service, task, setting change or stress test
is installed or run. Different-user elevation is not supported by this channel.
The engine and its drive database are SHA-256 checked before execution.

## Memory

GetSystemFirmwareTable(RSMB) reads Windows' SMBIOS wrapper. The parser follows
DMTF DSP0134 Type 17, including extended size/speed fields. It bounds every record
and string traversal, distinguishes zero-size empty slots from unknown sizes,
reads only selected properties and clears the raw firmware byte buffers.
Firmware may omit a device or misdescribe an inaccessible/soldered slot. WMI is
used as a fallback; absent firmware records are not invented as physical sockets.

DDR configured/rated transfer rates are shown in MT/s. For known DDR types,
rate/2 is explicitly an estimated equivalent clock in MHz, not a live measurement.
The WMI form-factor enum and the raw SMBIOS enum are mapped separately.

## Storage

Windows disk inventory provides index, model and capacity. The reader invokes
smartctl 7.5 with fixed read-only -j -x /dev/pdN arguments. Exit codes are a bitmask;
health warnings do not discard otherwise valid JSON. Drive updates require a
matching Windows disk index and normalized model, plus compatible capacity when
both sides expose it. Missing capacity alone does not discard a valid partial
ATA SMART response. Duplicate/ambiguous Windows-provider records stay unverified.

Only selected SMART model/firmware/counters and vendor-labelled ATA fields enter
the report model. Raw JSON, serials and device paths are not saved. Standard NVMe
data units are converted using 512000 bytes per unit. No universal meaning is
assigned to arbitrary ATA raw attributes. Unmapped legacy WMI tables remain
separate from mapped smartctl drive evidence. Logical-volume used/free space is
reported separately from drive endurance and powered-on time.

## Battery and assessment

Battery static, full-charge, cycle and live status records are joined by exact
instance. Full/design capacity is retained capacity; remaining/full is charge now.
Unknown units are labelled and unknown counters are not changed to zero.
Below 80% retained capacity prompts service review; below 50% is severe capacity
loss. These are app capacity policies, not physical safety or failure predictions.

CPU and board inventory do not establish functional health. Memory assessment
only addresses capacity. Warnings and manual failures take priority; unknown
components and pending manual checks prevent a blanket GOOD assessment.

## Privacy and reports

WMI uses explicit property lists. Firmware/drive buffers can contain identifiers
in process memory during collection, but only the documented allowlist is decoded
into exported data. Raw firmware buffers are cleared; raw SMART JSON remains
inside the short-lived worker. Internal drive indices are stripped on export.
Notes are excluded unless explicitly included. HTML encodes all variable text.
No credentials, accounts, SSIDs, IP/MAC addresses or user files are queried.

PDFsharp-WPF 6.2.3 generates A4 PDFs locally. The register has a designed cover,
factual summary, compact source/evidence tables, page context, numbered sections,
page numbers and branded link footers. HTML is self-contained. JSON retains the
full evidence model. Reports are unsigned point-in-time documents.

## Third-party aggregation

Original code is MIT. The separate unmodified smartctl executable and drivedb.h
are GPL-2.0-or-later. Each portable package contains the exact upstream source
archive, COPYING and INSTALL files; no smartd or installer is shipped. Other
runtime dependencies retain their included notices. Publishing is self-contained
win-x64 with the SMART files alongside the EXE; extract every file from the ZIP.

Reference: https://www.dmtf.org/sites/default/files/standards/documents/DSP0134_3.8.0.pdf
SMART engine: https://github.com/smartmontools/smartmontools/releases/tag/RELEASE_7_5

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.
