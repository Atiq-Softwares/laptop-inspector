using Inspector.Core;
using Microsoft.Win32;
using static Inspector.App.Diagnostics.WmiReader;

namespace Inspector.App.Diagnostics;

public sealed class HardwareCollector
{
    private const string Cim = @"root\cimv2";
    private const string Storage = @"root\Microsoft\Windows\Storage";
    private const string BatteryScope = @"root\wmi";
    private readonly Inspection result = new();

    public Inspection Collect(Action<int, string>? progress = null)
    {
        (string Name, Action Action)[] groups = [
            ("System identity", Identity), ("Processor", Cpu), ("Memory", Memory),
            ("Storage & reliability", Disks), ("Battery", Battery), ("Display & graphics", Display),
            ("Motherboard & BIOS", Board), ("Windows & security", Security),
            ("Network & Bluetooth", Network), ("Temperature coverage", Temperature)
        ];
        for (int index = 0; index < groups.Length; index++)
        {
            progress?.Invoke(index * 100 / groups.Length, groups[index].Name);
            try { groups[index].Action(); }
            catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException)
            {
                result.Components.Add(new Component { Category = groups[index].Name, Name = "Diagnostic unavailable", Explanation = "The Windows provider did not return usable data. No hardware condition has been inferred." });
                result.Limitations.Add($"{groups[index].Name}: diagnostic did not complete ({e.GetType().Name}).");
            }
        }
        result.IsComplete = true;
        result.CompletedAt = DateTimeOffset.Now;
        result.Limitations.Add("No stress test, memory integrity test, surface scan, firmware update or system modification was performed.");
        result.Limitations.Add("Provider values can be missing, stale, inaccurate or restricted by firmware, drivers, USB/RAID bridges or access permissions.");
        result.Limitations.Add("No elevated diagnostics are implemented. Unsupported counters remain unavailable; running as administrator is not required.");
        result.Limitations.Add(Branding.Disclaimer);
        progress?.Invoke(100, "Inspection complete");
        return result;
    }

    private QueryResult Read(string scope, string name, string properties, string? where = null)
    {
        var q = Query(scope, name, properties, where);
        if (q.Failure != null) result.Limitations.Add($"{name}: {q.Failure}.");
        return q;
    }
    private void Identity()
    {
        var r = Read(Cim, "Win32_ComputerSystem", "Manufacturer,Model").Rows.FirstOrDefault();
        result.Manufacturer = r?.S("Manufacturer") ?? "Not available";
        result.Model = r?.S("Model") ?? "Not available";
    }
    private void Cpu()
    {
        const string source = "Win32_Processor";
        var rows = Read(Cim, source, "Name,Manufacturer,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,CurrentClockSpeed,L2CacheSize,L3CacheSize").Rows;
        foreach (var r in rows)
        {
            var c = new Component { Category = "Processor", Name = r.S("Name") ?? "Unknown processor", Explanation = "Specifications are provider-reported. No benchmark or CPU stability test was performed; suitability is an estimate for general use." };
            c.Add("Manufacturer", r.S("Manufacturer"), source);
            c.Add("Physical cores", r.V("NumberOfCores"), source); c.Add("Logical processors", r.V("NumberOfLogicalProcessors"), source);
            c.Add("Maximum reported clock", Units(r.N("MaxClockSpeed"), "MHz"), source);
            c.Add("Current reported clock", Units(r.N("CurrentClockSpeed"), "MHz"), source);
            c.Add("L2 cache", Units(r.N("L2CacheSize"), "KiB"), source); c.Add("L3 cache", Units(r.N("L3CacheSize"), "KiB"), source);
            c.Add("Generation", Evaluation.IntelGeneration(c.Name), "Legacy Intel model-name parsing; other naming schemes unsupported", Evidence.Estimated);
            var cores = r.I("NumberOfCores");
            c.Add("General-use suitability", cores == null ? null : cores >= 4 ? "Potentially suitable for everyday office/web work; actual performance depends on CPU architecture, cooling and workload." : "Limited core count; review your workload with a representative benchmark.", "Core-count heuristic; not a performance measurement", Evidence.Estimated);
            c.Recommendations.Add("For gaming, CAD or video editing, compare this exact CPU with requirements for your applications. Laptop CPUs are usually not upgradeable.");
            result.Components.Add(c);
        }
        if (rows.Count == 0) result.Components.Add(new() { Category = "Processor", Name = "Processor unavailable", Explanation = "Windows returned no processor inventory." });
    }
    private void Memory()
    {
        const string source = "Win32_PhysicalMemory";
        var rows = Read(Cim, source, "Manufacturer,PartNumber,Capacity,Speed,ConfiguredClockSpeed,SMBIOSMemoryType,DeviceLocator,FormFactor").Rows;
        double? total = rows.Count > 0 && rows.All(r => r.N("Capacity") > 0) ? rows.Sum(r => r.N("Capacity")!.Value) : null;
        var c = new Component { Category = "Memory", Name = total == null ? "Memory capacity unavailable" : $"{Evaluation.Bytes(total)} installed memory", Rating = Evaluation.MemoryRating(total / 1073741824), Explanation = "Capacity policy: below 8 GiB prompts an upgrade review; at least 8 GiB meets a basic general-use baseline. This is not a memory integrity test. Channel mode is not inferred from module count." };
        c.Add("Total installed capacity", total == null ? null : Evaluation.Bytes(total), source);
        for (int n = 0; n < rows.Count; n++)
        {
            var r = rows[n]; string prefix = $"Module {n + 1}";
            c.Add(prefix + " capacity", r.N("Capacity") == null ? null : Evaluation.Bytes(r.N("Capacity")), source);
            c.Add(prefix + " manufacturer / part", Join(r.S("Manufacturer"), r.S("PartNumber")), source);
            c.Add(prefix + " type", Evaluation.MemoryType(r.I("SMBIOSMemoryType")), source);
            c.Add(prefix + " configured / rated speed", Join(Units(Positive(r.N("ConfiguredClockSpeed")), "MT/s"), Units(Positive(r.N("Speed")), "MT/s")), source);
            c.Add(prefix + " form factor", r.I("FormFactor") switch { 8 => "DIMM", 12 => "SO-DIMM", 0 or null => null, _ => "Other / firmware-defined" }, source);
        }
        var slots = Read(Cim, "Win32_PhysicalMemoryArray", "MemoryDevices").Rows;
        c.Add("Firmware-reported memory slots", slots.Count > 0 && slots.All(r => r.I("MemoryDevices") > 0) ? slots.Sum(r => r.I("MemoryDevices")!.Value) : null, "Win32_PhysicalMemoryArray; may include soldered memory");
        c.Recommendations.Add(total is > 0 and < 8 * 1073741824d ? "Consider at least 8 GiB, or 16 GiB for heavier multitasking. Verify supported modules, slots and soldered memory first." : "Check the service manual before upgrading. Run a dedicated offline memory test if errors or instability are suspected.");
        result.Components.Add(c);
    }
    private void Disks()
    {
        var disks = Read(Storage, "MSFT_PhysicalDisk", "DeviceId,FriendlyName,MediaType,BusType,Size,HealthStatus").Rows;
        var counters = Read(Storage, "MSFT_StorageReliabilityCounter", "DeviceId,Temperature,PowerOnHours,Wear,ReadErrorsUncorrected,WriteErrorsUncorrected").Rows;
        foreach (var r in disks)
        {
            var matches = counters.Where(x => !string.IsNullOrEmpty(r.S("DeviceId")) && x.S("DeviceId") == r.S("DeviceId")).ToList();
            var counter = matches.Count == 1 ? matches[0] : null;
            int? health = r.I("HealthStatus"); int? wear = counter?.I("Wear");
            if (wear is < 0 or > 100) wear = null;
            var rating = Evaluation.StorageRating(health, null, wear);
            var c = new Component { Category = "Storage", Name = r.S("FriendlyName") ?? "Unnamed physical disk", Rating = rating, Explanation = "Windows storage-provider status and any available reliability counters are point-in-time readings. Missing NVMe/ATA counters are not zero. A Healthy status does not guarantee future reliability." };
            c.Add("Capacity", r.N("Size") == null ? null : Evaluation.Bytes(r.N("Size")), "MSFT_PhysicalDisk");
            c.Add("Media type", r.I("MediaType") switch { 3 => "HDD", 4 => "SSD", 5 => "Storage-class memory", _ => null }, "MSFT_PhysicalDisk");
            c.Add("Bus / interface", Evaluation.BusType(r.I("BusType")), "MSFT_PhysicalDisk");
            c.Add("Provider health", health switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", 5 => "Unknown", _ => null }, "MSFT_PhysicalDisk");
            c.Add("Power-on hours", Units(counter?.N("PowerOnHours"), "hours"), "MSFT_StorageReliabilityCounter; matched by DeviceId");
            c.Add("Wear used", Units(wear, "%"), "MSFT_StorageReliabilityCounter; 100% is estimated wear limit");
            c.Add("Drive temperature", Units(TemperatureValue(counter?.N("Temperature")), "°C"), "MSFT_StorageReliabilityCounter");
            c.Add("Uncorrected read errors", counter?.V("ReadErrorsUncorrected"), "MSFT_StorageReliabilityCounter");
            c.Add("Uncorrected write errors", counter?.V("WriteErrorsUncorrected"), "MSFT_StorageReliabilityCounter");
            if (counter?.N("ReadErrorsUncorrected") > 0 || counter?.N("WriteErrorsUncorrected") > 0)
            {
                if (c.Rating != Rating.Critical) c.Rating = Rating.Caution;
                c.Recommendations.Add("Uncorrected errors were reported. Back up important data and review the drive with the manufacturer's diagnostic utility.");
            }
            c.Recommendations.Add(c.Rating is Rating.Caution or Rating.Critical ? "Back up important data and investigate the drive's warnings before relying on it." : "Keep regular backups. Use the manufacturer's diagnostics to verify unsupported SMART/NVMe fields.");
            result.Components.Add(c);
        }
        if (disks.Count == 0)
        {
            var fallback = Read(Cim, "Win32_DiskDrive", "Model,InterfaceType,Size,MediaType").Rows;
            foreach (var r in fallback)
            {
                var c = new Component { Category = "Storage", Name = r.S("Model") ?? "Disk", Explanation = "Inventory fallback only. Physical-disk health and reliability counters could not be verified." };
                c.Add("Capacity", r.N("Size") == null ? null : Evaluation.Bytes(r.N("Size")), "Win32_DiskDrive"); c.Add("Reported interface", r.V("InterfaceType"), "Win32_DiskDrive; may show SCSI for NVMe/RAID");
                c.Add("SMART / operating hours", null, "Windows storage provider"); result.Components.Add(c);
            }
            if (fallback.Count == 0) result.Components.Add(new() { Category = "Storage", Name = "Storage unavailable", Explanation = "Windows returned no usable disk inventory." });
        }
        // Keep legacy SMART records separate: do not guess their mapping to NVMe/RAID disks.
        var predictions = Read(BatteryScope, "MSStorageDriver_FailurePredictStatus", "InstanceName,PredictFailure").Rows;
        var ata = Read(BatteryScope, "MSStorageDriver_FailurePredictData", "InstanceName,VendorSpecific").Rows;
        for (int n = 0; n < predictions.Count; n++)
        {
            var r = predictions[n]; var flag = r.B("PredictFailure");
            var c = new Component { Category = "Storage", Name = $"Legacy ATA SMART record {n + 1} (unmapped)", Rating = flag == true ? Rating.Critical : Rating.NotVerified, Explanation = "This legacy provider record is not mapped to an inventory disk. Raw attributes are vendor-specific; they are not interpreted as hours, errors or remaining life without vendor documentation." };
            c.Add("SMART failure prediction", flag == null ? null : flag.Value ? "Failure predicted" : "No failure prediction reported", "MSStorageDriver_FailurePredictStatus");
            var data = ata.SingleOrDefault(x => x.S("InstanceName") == r.S("InstanceName"))?.V("VendorSpecific") as byte[];
            if (data != null)
                foreach (var a in SmartParser.Parse(data)) c.Add($"ATA attribute 0x{a.Id:X2}", $"normalized {a.Current}; worst {a.Worst}; raw {a.Raw}", "MSStorageDriver_FailurePredictData; vendor-specific raw value");
            result.Components.Add(c);
        }
        if (predictions.Count == 0) result.Limitations.Add("Legacy ATA SMART tables unavailable. NVMe and USB/RAID adapters often do not expose this provider; this does not indicate a healthy or failed drive.");
    }
    private void Battery()
    {
        var stat = Read(BatteryScope, "BatteryStaticData", "InstanceName,DesignedCapacity,Capabilities").Rows;
        var full = Read(BatteryScope, "BatteryFullChargedCapacity", "InstanceName,FullChargedCapacity").Rows;
        var cycles = Read(BatteryScope, "BatteryCycleCount", "InstanceName,CycleCount").Rows;
        if (stat.Count == 0)
        {
            var detected = Read(Cim, "Win32_Battery", "Name,EstimatedChargeRemaining").Rows;
            var c = new Component { Category = "Battery", Name = detected.Count > 0 ? "Battery detected; capacity data unavailable" : "Battery not detected or provider unavailable", Explanation = "Battery health cannot be calculated without matched, valid design and full-charge capacities. Desktops and some firmware do not expose battery data." };
            c.Add("Battery capacity / cycles", null, "Battery WMI providers");
            if (detected.Count == 1) c.Add("Current charge (not health)", Units(detected[0].N("EstimatedChargeRemaining"), "%"), "Win32_Battery");
            result.Components.Add(c); return;
        }
        for (int n = 0; n < stat.Count; n++)
        {
            var r = stat[n];
            var f = full.Where(x => x.S("InstanceName") == r.S("InstanceName")).ToList();
            var y = cycles.Where(x => x.S("InstanceName") == r.S("InstanceName")).ToList();
            var design = Capacity(r.N("DesignedCapacity")); var charged = f.Count == 1 ? Capacity(f[0].N("FullChargedCapacity")) : null;
            var percent = Evaluation.BatteryHealth(design, charged);
            bool relative = r.N("Capabilities") is double caps && ((ulong)caps & 0x40000000) != 0;
            string unit = relative ? "relative units" : "mWh";
            var c = new Component { Category = "Battery", Name = $"Battery {n + 1}", Rating = Evaluation.BatteryRating(percent), Explanation = percent == null ? "Capacity data is missing or inconsistent (including values over 120% of design). No health estimate is available." : "Capacity retention = reported full-charge / design capacity. Policy: below 80% prompts service review; below 50% is critical capacity loss. This estimate is not a runtime or swelling test. Values above 100% can occur after calibration." };
            c.Add("Design capacity", Units(design, unit), "BatteryStaticData"); c.Add("Full-charge capacity", Units(charged, unit), "BatteryFullChargedCapacity; matched by instance");
            c.Add("Capacity retention", Units(percent, "%"), "Calculated from matched battery capacity readings", Evidence.Estimated);
            c.Add("Cycle count", y.Count == 1 ? y[0].V("CycleCount") : null, "BatteryCycleCount; matched by instance");
            c.Recommendations.Add(c.Rating is Rating.Caution or Rating.Critical ? "Consider battery replacement if runtime is insufficient. Inspect for swelling separately and use manufacturer-approved service." : "Verify real unplugged runtime and physical condition. Do not infer battery safety from capacity retention alone.");
            result.Components.Add(c);
        }
    }
    private void Display()
    {
        var rows = Read(Cim, "Win32_VideoController", "Name,DriverVersion,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate,VideoModeDescription").Rows;
        foreach (var r in rows)
        {
            var c = new Component { Category = "Display & graphics", Name = r.S("Name") ?? "Graphics adapter", Explanation = "Current mode is driver-reported and can be absent or reflect a remote/virtual display. Pixel condition, brightness, display size and GPU stability require manual checks." };
            c.Add("Driver version", r.S("DriverVersion"), "Win32_VideoController");
            c.Add("Current resolution", r.N("CurrentHorizontalResolution") > 0 && r.N("CurrentVerticalResolution") > 0 ? $"{r.N("CurrentHorizontalResolution")} × {r.N("CurrentVerticalResolution")}" : null, "Win32_VideoController");
            c.Add("Current refresh rate", r.N("CurrentRefreshRate") is > 1 and < 1000 ? Units(r.N("CurrentRefreshRate"), "Hz") : null, "Win32_VideoController; 0/1 treated as unknown");
            c.Add("Mode description", r.S("VideoModeDescription"), "Win32_VideoController"); result.Components.Add(c);
        }
        if (rows.Count == 0) result.Components.Add(new() { Category = "Display & graphics", Name = "Graphics data unavailable", Explanation = "No usable graphics inventory returned. Check display and graphics manually." });
    }
    private void Board()
    {
        var r = Read(Cim, "Win32_BaseBoard", "Manufacturer,Product,Version").Rows.FirstOrDefault();
        var c = new Component { Category = "Motherboard & BIOS", Name = Join(r?.S("Manufacturer"), r?.S("Product")) ?? "Motherboard unavailable", Explanation = "Inventory only. Firmware version does not establish security support or board health. Check the manufacturer's official support page before any update." };
        c.Add("Board version", r?.S("Version"), "Win32_BaseBoard");
        var bios = Read(Cim, "Win32_BIOS", "Manufacturer,SMBIOSBIOSVersion,ReleaseDate").Rows.FirstOrDefault();
        c.Add("BIOS manufacturer", bios?.S("Manufacturer"), "Win32_BIOS"); c.Add("BIOS version", bios?.S("SMBIOSBIOSVersion"), "Win32_BIOS");
        c.Add("BIOS release date", ParseWmiDate(bios?.S("ReleaseDate")), "Win32_BIOS"); result.Components.Add(c);
    }
    private void Security()
    {
        var os = Read(Cim, "Win32_OperatingSystem", "Caption,Version,BuildNumber,OSArchitecture,LastBootUpTime").Rows.FirstOrDefault();
        var c = new Component { Category = "Windows & security", Name = os?.S("Caption") ?? "Windows information unavailable", Explanation = "Security configuration is a snapshot, not a malware scan or compliance certification. Disabled Defender can be intentional when another antivirus is installed; alternative antivirus products are not evaluated." };
        c.Add("OS version / build", Join(os?.S("Version"), os?.S("BuildNumber")), "Win32_OperatingSystem");
        c.Add("Architecture", os?.S("OSArchitecture"), "Win32_OperatingSystem");
        c.Add("Last boot", ParseWmiDate(os?.S("LastBootUpTime")), "Win32_OperatingSystem");
        int? secure = ReadRegistryInt(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
        c.Add("Secure Boot", secure switch { 1 => "Enabled", 0 => "Disabled", _ => null }, "Read-only registry SecureBoot state");
        var tpm = Read(@"root\cimv2\Security\MicrosoftTpm", "Win32_Tpm", "SpecVersion,IsEnabled_InitialValue,IsActivated_InitialValue").Rows.FirstOrDefault();
        c.Add("TPM specification", tpm?.S("SpecVersion"), "Win32_Tpm"); c.Add("TPM enabled (initial state)", tpm?.V("IsEnabled_InitialValue"), "Win32_Tpm");
        var defender = Read(@"root\Microsoft\Windows\Defender", "MSFT_MpComputerStatus", "AntivirusEnabled,RealTimeProtectionEnabled,AntivirusSignatureLastUpdated").Rows.FirstOrDefault();
        c.Add("Defender antivirus enabled", defender?.V("AntivirusEnabled"), "MSFT_MpComputerStatus"); c.Add("Defender realtime protection", defender?.V("RealTimeProtectionEnabled"), "MSFT_MpComputerStatus");
        c.Add("Defender signature update", ParseWmiDate(defender?.S("AntivirusSignatureLastUpdated")), "MSFT_MpComputerStatus");
        var encrypted = Read(@"root\cimv2\Security\MicrosoftVolumeEncryption", "Win32_EncryptableVolume", "DriveLetter,ProtectionStatus").Rows;
        foreach (var volume in encrypted.Where(v => !string.IsNullOrEmpty(v.S("DriveLetter")))) c.Add($"Encryption {volume.S("DriveLetter")}", volume.I("ProtectionStatus") switch { 0 => "Protection off", 1 => "Protection on", _ => null }, "Win32_EncryptableVolume");
        if (encrypted.Count == 0) c.Add("Drive encryption", null, "Win32_EncryptableVolume");
        if (secure == 0 || tpm?.B("IsEnabled_InitialValue") == false)
        {
            c.Rating = Rating.Caution;
            c.Recommendations.Add("Review disabled Secure Boot / TPM against your security requirements. Changing firmware settings can affect boot and encryption; consult the manufacturer.");
        }
        else c.Recommendations.Add("Verify Windows support, installed updates and your intended security product separately; this offline scan does not check online advisories.");
        result.Components.Add(c);
    }
    private void Network()
    {
        var c = new Component { Category = "Network & Bluetooth", Name = "Network hardware inventory", Explanation = "Adapter detection and link state do not verify Wi-Fi signal, internet access, throughput or Bluetooth pairing. No SSIDs, MAC addresses or IP addresses are collected." };
        var rows = Read(Cim, "Win32_NetworkAdapter", "Name,Manufacturer,NetConnectionStatus,PhysicalAdapter", "PhysicalAdapter = TRUE").Rows;
        for (int n = 0; n < rows.Count; n++)
        {
            c.Add($"Adapter {n + 1}", Join(rows[n].S("Manufacturer"), rows[n].S("Name")), "Win32_NetworkAdapter");
            c.Add($"Adapter {n + 1} link state", rows[n].I("NetConnectionStatus") switch { 2 => "Connected", 0 => "Disconnected", 7 => "Media disconnected", null => null, _ => "Other Windows connection state" }, "Win32_NetworkAdapter");
        }
        if (rows.Count == 0) c.Add("Network adapters", null, "Win32_NetworkAdapter");
        var bluetooth = Read(Cim, "Win32_PnPEntity", "Name,Service", "PNPClass = 'Bluetooth'").Rows;
        // Only publish local radio/controller names. Paired Bluetooth device names may identify a person.
        var radios = bluetooth.Where(r => r.S("Service")?.Equals("BTHUSB", StringComparison.OrdinalIgnoreCase) == true || r.S("Service")?.Equals("BthMini", StringComparison.OrdinalIgnoreCase) == true).Select(r => r.S("Name")).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        c.Add("Bluetooth radio", radios.Count > 0 ? string.Join("; ", radios) : null, "Win32_PnPEntity; local BTHUSB/BthMini driver only");
        result.Components.Add(c);
    }
    private void Temperature()
    {
        var c = new Component { Category = "Temperature & reliability", Name = "Available thermal sensors", Explanation = "ACPI thermal zones are not identified as CPU or GPU package temperatures. Sensor values do not establish thermal stability. No kernel driver or stress test is used." };
        var rows = Read(BatteryScope, "MSAcpi_ThermalZoneTemperature", "CurrentTemperature").Rows;
        for (int n = 0; n < rows.Count; n++) c.Add($"ACPI thermal zone {n + 1}", Units(TemperatureValue(rows[n].N("CurrentTemperature") / 10 - 273.15), "°C"), "MSAcpi_ThermalZoneTemperature; tenths of kelvin converted to Celsius");
        c.Add("CPU / GPU package temperatures", null, "No universal non-elevated Windows provider");
        c.Add("Stability / memory integrity", null, "No automatic stress or memory test"); result.Components.Add(c);
    }
    private static double? Capacity(double? n) => n is > 0 and < uint.MaxValue ? n : null;
    private static double? Positive(double? n) => n is > 0 ? n : null;
    private static double? TemperatureValue(double? n) => n is > 0 and < 150 ? Math.Round(n.Value, 1) : null;
    private static string? Units(double? value, string unit) => value.HasValue ? $"{value.Value:0.##} {unit}" : null;
    private static string? Join(params string?[] values) { var v = values.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(); return v.Length == 0 ? null : string.Join(" / ", v); }
    private static string? ParseWmiDate(string? date)
    {
        try { return string.IsNullOrEmpty(date) ? null : System.Management.ManagementDateTimeConverter.ToDateTime(date).ToString("yyyy-MM-dd HH:mm:ss"); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
    private static int? ReadRegistryInt(string path, string value)
    {
        try { using var k = Registry.LocalMachine.OpenSubKey(path, false); return k?.GetValue(value) is int n ? n : null; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { return null; }
    }
}
