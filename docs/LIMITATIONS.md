# Known limitations and compatibility

- Qualified build target: Windows 10 22H2 / Windows 11, x64. Other architectures
  and physical laptop models have not been qualified. Virtualized and remote
  environments may expose virtual devices and incomplete providers.
- A scan is a Windows-provider snapshot. Firmware may return missing, stale or
  inaccurate values. Collection completion does not mean every component passed.
- No kernel driver is installed. CPU/GPU package temperatures are unavailable;
  any ACPI zone is labelled as an unidentified ACPI sensor, not a CPU reading.
- Storage health, wear, operating hours, error counters and ATA SMART support vary
  by disk, driver, USB bridge, RAID controller and permissions. Windows can report
  Healthy without exposing detailed SMART. The bundled SMART engine supports additional ATA/NVMe paths, but no drive self-test is run. ATA raw attributes are vendor-specific and not interpreted as
  hours or remaining life. Legacy records are not guessed onto inventory disks.
- Battery cycle/capacity data may be unavailable. Capacity retention is not runtime,
  charge percentage, physical safety or a guarantee. Values above 100% can occur
  after calibration; values over 120% are withheld as inconsistent.
- CPU generation inference supports only legacy Intel Core i3/i5/i7/i9 model
  patterns from generations 2-14. Core Ultra, Ryzen and other names stay unknown.
  CPU suitability is only a rough core-count heuristic, not a benchmark.
- Per-slot inventory depends on SMBIOS Type 17 or WMI fallback. Unknown device sizes are not empty slots, and omitted slots are not guessed. Module count does not prove channel
  mode. Soldered-memory expandability and integrity require separate checks.
- Display information is the driver-reported current mode, not panel certification.
  Multi-monitor, HDR, touch, VRAM and display-size verification are not implemented.
- Adapter presence is not a connectivity, throughput, microphone or camera test.
  Some local Bluetooth radios are omitted when their driver service is unknown.
- TPM, Defender and BitLocker providers can be restricted. Basic inspection does not request elevation; optional advanced drive access can retry restricted SMART reads after explicit permission.
  Alternative antivirus, online patch/support status and malware are not evaluated.
- Keyboard/display helpers require operator observation. Fn/media keys may be
  intercepted by firmware or Windows. A manual PASS remains an operator claim.
- Reports are unsigned documents and can be edited by their recipient. They are
  not forensic evidence, a warranty, an ownership check or a resale appraisal.
- Manual notes may contain sensitive information if you choose to include them.
- The EXE is unsigned. SmartScreen, Defender or enterprise policies may warn or
  block execution. No security bypass or disabling instructions are provided.
- This implementation follows the specification included with this project. No
  separate earlier hardware specification was available in the supplied request.

Copyright © 2026 Atiq-Softwares. Developed by Atiq-Softwares.

- No application can guarantee every counter on every laptop. USB/RAID bridges,
  firmware, drivers, corporate policy and privilege restrictions can block reads.
- Advanced drive access requires the same Windows account to approve elevation.
  The one-shot helper stops between requests after parent cancellation; a pending
  smartctl request may take up to its 12-second timeout to end.
- NVMe media errors, wear and power counters are reported when present. Arbitrary
  ATA attributes remain vendor-specific. Missing host write totals are not inferred
  from undocumented sector counters.
- Current battery charge and retained capacity are separate estimates based on
  matched provider values. Charge-rate units can be relative on some batteries.
