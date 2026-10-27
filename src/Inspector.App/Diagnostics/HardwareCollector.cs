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
        result.Limitations.Add("Basic inspection requires no elevation. Optional advanced drive access can retry restricted SMART reads after explicit Windows permission.");
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
        var rows = Read(Cim, source, "Name,Manufacturer,NumberOfCores,NumberOfEnabledCore,NumberOfLogicalProcessors,MaxClockSpeed,CurrentClockSpeed,L2CacheSize,L3CacheSize,AddressWidth,DataWidth,SocketDesignation,VirtualizationFirmwareEnabled,SecondLevelAddressTranslationExtensions").Rows;
        foreach (var r in rows)
        {
            var c = new Component { Category = "Processor", Name = r.S("Name") ?? "Unknown processor", Explanation = "Specifications are provider-reported. No benchmark or CPU stability test was performed; suitability is an estimate for general use." };
            c.Add("Manufacturer", r.S("Manufacturer"), source);
            var instructionSets = new List<string>();
            if(System.Runtime.Intrinsics.X86.Sse2.IsSupported)instructionSets.Add("SSE2");
            if(System.Runtime.Intrinsics.X86.Sse41.IsSupported)instructionSets.Add("SSE4.1");
            if(System.Runtime.Intrinsics.X86.Sse42.IsSupported)instructionSets.Add("SSE4.2");
            if(System.Runtime.Intrinsics.X86.Avx.IsSupported)instructionSets.Add("AVX");
            if(System.Runtime.Intrinsics.X86.Avx2.IsSupported)instructionSets.Add("AVX2");
            if(System.Runtime.Intrinsics.X86.Fma.IsSupported)instructionSets.Add("FMA");
            if(System.Runtime.Intrinsics.X86.Avx512F.IsSupported)instructionSets.Add("AVX-512F");
            c.Add("OS-enabled instruction sets",instructionSets.Count>0?string.Join(", ",instructionSets):null,".NET processor intrinsics; enabled for this process, not a complete CPUID catalogue");
            c.Add("Socket", r.S("SocketDesignation"), source); c.Add("Enabled cores", r.V("NumberOfEnabledCore"), source); c.Add("Architecture width", Units(r.N("DataWidth"), "bits"), source); c.Add("Virtualization enabled", r.V("VirtualizationFirmwareEnabled"), source); c.Add("Second-level address translation", r.V("SecondLevelAddressTranslationExtensions"), source);
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
        var devices = FirmwareReader.Memory();
        var rows = Read(Cim, "Win32_PhysicalMemory", "Manufacturer,PartNumber,Capacity,Speed,ConfiguredClockSpeed,SMBIOSMemoryType,DeviceLocator,BankLabel,FormFactor,ConfiguredVoltage,DataWidth,TotalWidth,Attributes").Rows;
        double? total = devices.Count > 0 && devices.All(d => d.CapacityBytes != null) ? devices.Sum(d => (double)d.CapacityBytes!.Value) : rows.Count > 0 && rows.All(r => r.N("Capacity") > 0) ? rows.Sum(r => r.N("Capacity")!.Value) : null;
        var summary = new Component { Category = "Memory", Name = total == null ? "Memory inventory" : $"{Evaluation.Bytes(total)} installed", Rating = Evaluation.MemoryRating(total / 1073741824), Explanation = "Capacity and slot inventory. Module count does not establish channel mode; soldered memory and physical upgrade access require the service manual. No memory integrity test was run." };
        summary.Add("Installed capacity", total == null ? null : Evaluation.Bytes(total), "SMBIOS Type 17 / Win32_PhysicalMemory");
        summary.Add("Populated memory devices", devices.Count > 0 ? devices.Count(d => !d.Empty) : rows.Count > 0 ? rows.Count : null, "Firmware memory-device records");
        summary.Add("Empty memory devices", devices.Count > 0 ? devices.Count(d => d.Empty) : null, "SMBIOS Type 17; only explicitly empty records are counted");
        var arrays = Read(Cim, "Win32_PhysicalMemoryArray", "MemoryDevices,MaxCapacityEx").Rows;
        summary.Add("Firmware-reported device count", arrays.Count > 0 ? arrays.Sum(r => r.N("MemoryDevices") ?? 0) : null, "Win32_PhysicalMemoryArray");
        summary.Add("Firmware maximum capacity", arrays.Count > 0 && arrays.All(r => r.N("MaxCapacityEx") > 0) ? Evaluation.Bytes(arrays.Sum(r => r.N("MaxCapacityEx")!.Value) * 1024) : null, "Win32_PhysicalMemoryArray; verify against manufacturer support");
        summary.Recommendations.Add(total is > 0 and < 8 * 1073741824d ? "Review a memory upgrade after checking the service manual and supported modules." : "Compare your workload with installed capacity. Check manufacturer limits before adding or replacing memory.");
        var usage = Read(Cim,"Win32_OperatingSystem","TotalVisibleMemorySize,FreePhysicalMemory").Rows.FirstOrDefault();
        summary.Add("OS-visible memory",usage?.N("TotalVisibleMemorySize") is double visible ? Evaluation.Bytes(visible*1024) : null,"Win32_OperatingSystem; snapshot");
        summary.Add("Available physical memory",usage?.N("FreePhysicalMemory") is double available ? Evaluation.Bytes(available*1024) : null,"Win32_OperatingSystem; snapshot, not an integrity result");
        result.Components.Add(summary);
        if (devices.Count == 0)
            devices = rows.Select((r,n) => new MemoryDevice(r.S("DeviceLocator") ?? $"Unmapped module {n+1}", r.S("BankLabel"), r.N("Capacity") is double size ? (ulong)size : null, false, r.S("Manufacturer"), r.S("PartNumber"), r.I("SMBIOSMemoryType"), r.I("FormFactor") switch {9 => 8,13 => 12,_ => null}, r.N("Speed") is > 0 and var rate ? (uint)rate : null, r.N("ConfiguredClockSpeed") is > 0 and var configured ? (uint)configured : null, r.I("Attributes"), r.I("ConfiguredVoltage"), r.I("DataWidth"), r.I("TotalWidth"))).ToList();
        foreach (var d in devices)
        {
            var c = new Component { Category = "Memory slot", Name = d.Locator, Rating = Rating.NotVerified, Explanation = d.Empty ? "Firmware explicitly reports no installed module at this memory-device location. This alone does not confirm a user-accessible upgrade socket." : "Module specifications from firmware. Effective DDR transfer rate is shown in MT/s; the corresponding clock is an estimate, not a live electrical measurement." };
            const string src = "SMBIOS Type 17 / Windows firmware inventory";
            c.Add("Slot state", d.Empty ? "Empty (firmware reported)" : "Populated", src); c.Add("Bank", d.Bank, src);
            if (!d.Empty)
            {
                c.Add("Module capacity", d.CapacityBytes.HasValue ? Evaluation.Bytes(d.CapacityBytes.Value) : null, src);
                c.Add("Manufacturer", d.Manufacturer, src); c.Add("Part number", d.PartNumber, src); c.Add("Memory type", Evaluation.MemoryType(d.Type), src);
                c.Add("Configured data rate", Units(d.ConfiguredRate, "MT/s"), src); c.Add("Rated data rate", Units(d.RatedRate, "MT/s"), src);
                c.Add("Equivalent DDR clock", Units(MemoryInventory.DdrClock(d.ConfiguredRate, d.Type), "MHz"), "Effective DDR rate / 2; not a live clock reading", Evidence.Estimated);
                c.Add("Form factor", d.FormFactor switch {8 => "DIMM",12 => "SO-DIMM",0x0B => "Row of chips / soldered",0 or null => null,_ => "Other firmware form factor"}, src);
                c.Add("Ranks", d.Ranks, src); c.Add("Configured voltage", d.VoltageMv is > 0 ? Units(d.VoltageMv / 1000d, "V") : null, src);
                c.Add("Data width", Units(d.DataWidth, "bits"), src); c.Add("Total width", Units(d.TotalWidth, "bits"), src);
            }
            result.Components.Add(c);
        }
    }
    private void Disks()
    {
        var inventory = Read(Cim, "Win32_DiskDrive", "Index,Model,Size,FirmwareRevision").Rows;
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
            c.DiskIndex = r.I("DeviceId"); c.CapacityBytes = r.N("Size") is double bytes ? (long)bytes : null;
            c.Add("Capacity", r.N("Size") == null ? null : Evaluation.Bytes(r.N("Size")), "MSFT_PhysicalDisk");
            c.Add("Decimal capacity", r.N("Size") is double sizeGb ? $"{sizeGb/1_000_000_000:0.##} GB" : null,"MSFT_PhysicalDisk; decimal units used by drive vendors");
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
            var diskRecord = inventory.SingleOrDefault(v => v.I("Index") == c.DiskIndex);
            c.Add("Firmware version", diskRecord?.S("FirmwareRevision"), "Win32_DiskDrive");
            if (c.DiskIndex is int number && SmartEngine.Read(number) is DriveFacts facts && SmartEngine.Matches(c, new(number, facts))) DriveSmart.Apply(c, facts);
            else c.Add("Extended SMART access", null, "Read-only smartctl; retry with optional elevated access if permitted");
            result.Components.Add(c);
        }
        if (disks.Count == 0)
        {
            var fallback = Read(Cim, "Win32_DiskDrive", "Index,Model,InterfaceType,Size,MediaType,FirmwareRevision").Rows;
            foreach (var r in fallback)
            {
                var c = new Component { Category = "Storage", Name = r.S("Model") ?? "Disk", Explanation = "Inventory fallback only. Physical-disk health and reliability counters could not be verified." };
                c.Add("Capacity", r.N("Size") == null ? null : Evaluation.Bytes(r.N("Size")), "Win32_DiskDrive"); c.Add("Reported interface", r.V("InterfaceType"), "Win32_DiskDrive; may show SCSI for NVMe/RAID");
                c.DiskIndex = r.I("Index"); c.CapacityBytes = r.N("Size") is double size ? (long)size : null;
                if(c.DiskIndex is int index && SmartEngine.Read(index) is DriveFacts facts && SmartEngine.Matches(c,new(index,facts))) DriveSmart.Apply(c,facts);
                else c.Add("SMART / operating hours", null, "Windows storage provider / smartctl");
                result.Components.Add(c);
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
        foreach(var volume in Read(Cim,"Win32_LogicalDisk","DeviceID,FileSystem,Size,FreeSpace","DriveType = 3").Rows)
        {
            var c=new Component{Category="Volumes",Name=volume.S("DeviceID")??"Local volume",Explanation="Logical filesystem space at inspection time. This is not SSD wear or a physical-disk health measurement. A volume may span or share physical disks."};
            c.Add("Filesystem",volume.S("FileSystem"),"Win32_LogicalDisk");
            c.Add("Volume capacity",volume.N("Size") is double size ? Evaluation.Bytes(size) : null,"Win32_LogicalDisk");
            c.Add("Free space",volume.N("FreeSpace") is double free ? Evaluation.Bytes(free) : null,"Win32_LogicalDisk");
            c.Add("Used space",volume.N("Size") is double all && volume.N("FreeSpace") is double unused && unused<=all ? Evaluation.Bytes(all-unused) : null,"Win32_LogicalDisk; capacity minus free space");
            result.Components.Add(c);
        }
    }
    private void Battery()
    {
        var stat = Read(BatteryScope, "BatteryStaticData", "InstanceName,DesignedCapacity,Capabilities").Rows;
        var full = Read(BatteryScope, "BatteryFullChargedCapacity", "InstanceName,FullChargedCapacity").Rows;
        var cycles = Read(BatteryScope, "BatteryCycleCount", "InstanceName,CycleCount").Rows;
        var status = Read(BatteryScope, "BatteryStatus", "InstanceName,RemainingCapacity,Voltage,ChargeRate,DischargeRate,PowerOnline,Charging,Discharging").Rows;
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
            string unit = r.N("Capabilities") == null ? "reported units (flags unavailable)" : relative ? "relative units" : "mWh";
            var matches = status.Where(x => x.S("InstanceName") == r.S("InstanceName")).ToList();
            var live = matches.Count == 1 ? matches[0] : null;
            double? remaining = live?.N("RemainingCapacity") is >= 0 and < uint.MaxValue and var current ? current : null;
            var currentCharge = Evaluation.BatteryCharge(remaining, charged);
            var c = new Component { Category = "Battery", Name = $"Battery {n + 1}", Rating = Evaluation.BatteryRating(percent), Explanation = percent == null ? "Capacity data is missing or inconsistent (including values over 120% of design). No health estimate is available." : "Capacity retention = reported full-charge / design capacity. Policy: below 80% prompts service review; below 50% is critical capacity loss. This estimate is not a runtime or swelling test. Values above 100% can occur after calibration." };
            c.Add("Design capacity", Units(design, unit), "BatteryStaticData"); c.Add("Full-charge capacity", Units(charged, unit), "BatteryFullChargedCapacity; matched by instance");
            c.Add("Capacity retention", Units(percent, "%"), "Calculated from matched battery capacity readings", Evidence.Estimated);
            c.Add("Current remaining capacity", Units(remaining, unit), "BatteryStatus; matched by instance");
            c.Add("Current charge", Units(currentCharge, "%"), "Remaining capacity / full-charge capacity", Evidence.Estimated);
            c.Add("Capacity wear", percent == null ? null : Units(Math.Max(0, 100-percent.Value), "%"), "100 - capacity retention; calibration can exceed design capacity", Evidence.Estimated);
            c.Add("Condition", percent == null ? "Not verified" : percent >= 80 ? "Normal capacity retention" : percent >= 50 ? "Reduced capacity / service review" : "Severely reduced capacity", "Capacity-based assessment; does not verify physical safety", Evidence.Estimated);
            c.Add("Power state", live == null ? null : live.B("Charging") == true ? "Charging" : live.B("Discharging") == true ? "Discharging" : live.B("PowerOnline") == true ? "AC connected / not charging" : "Battery power / idle", "BatteryStatus");
            c.Add("Voltage", Units(Positive(live?.N("Voltage")) / 1000, "V"), "BatteryStatus");
            c.Add("Charge rate", Units(Capacity(live?.N("ChargeRate")), relative ? "relative units / hour" : "mW"), "BatteryStatus");
            c.Add("Discharge rate", Units(Capacity(live?.N("DischargeRate")), relative ? "relative units / hour" : "mW"), "BatteryStatus");
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
