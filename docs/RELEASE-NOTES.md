# v1.1.1 — security update

Download **Atiq-Laptop-Inspector-v1.1.1-Windows-x64.zip**, extract the entire ZIP
and run **Atiq-Laptop-Inspector.exe**.

- Bound and validate diagnostic/helper input, verify same-user process peers and protect the pinned SMART reader while it runs.
- Write reports transactionally, preserve existing files on export failure, and strengthen HTML content policy and long PDF pagination.
- Lock dependencies, remove embedded release debug symbols, and enforce an explicit package allowlist with required legal/vendor hashes.
- Include SHA-256 ZIP and payload checks plus a verification tool that reports actual Authenticode status.
- Keep portable/offline inspection, CPU/RAM/battery/storage coverage and PDF/HTML/JSON reports. No activation, device binding or background service.

The EXE is **unsigned**. Checksums provide integrity relative to a trusted digest;
they do not establish a validated publisher certificate. Windows 10, broader
hardware and the actual UAC prompt still require device qualification. Local
Windows 11 x64, same-user helper transport and all 87 automated tests passed.

The original application remains **MIT licensed**. Existing MIT grants and
notices are preserved. The separate GPL-2.0-or-later smartctl executable retains
its source, licence and build instructions in the ZIP. No repository visibility
change or proprietary licence adoption is part of this patch.

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.
https://github.com/Atiq-Softwares

---

## v1.1 history

Atiq Laptop Inspector v1.1 adds a redesigned inspection desk and substantially deeper hardware evidence.

Download **Atiq-Laptop-Inspector-v1.1-Windows-x64.zip**, extract every file and run **Atiq-Laptop-Inspector.exe**. Keep the third-party and licences folders beside it.

- Battery: design, full-charge and remaining capacity; charge now, retention, wear, cycles, voltage and charging state.
- RAM: per-slot firmware inventory, explicit empty slots, module parts, capacities, DDR type, configured/rated MT/s, estimated clock MHz, ranks, voltage and widths.
- Storage: read-only smartctl plus Windows counters for actual operating hours, cycles, temperature, wear, SMART status and available ATA/NVMe details. Filesystem use is reported separately.
- Searchable/filterable hardware register, paper/graphite/brass interface, and a redesigned branded PDF with a specification summary and compact evidence tables.
- Optional advanced drive access requests Windows permission only after the operator chooses it. No automatic stress tests, service installation or security bypass.

Sample reports contain synthetic data. Readings depend on firmware/controller access; support for every reading on every model cannot be guaranteed. The application is unsigned.

Original application code: MIT. The separate smartctl executable is GPL-2.0-or-later, with its unmodified source, licence and build instructions included. All other notices are included.

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.
GitHub: https://github.com/Atiq-Softwares
