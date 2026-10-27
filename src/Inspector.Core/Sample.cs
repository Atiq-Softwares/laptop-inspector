namespace Inspector.Core;

public static class Sample
{
    public static Inspection Create()
    {
        var i = new Inspection { IsSample = true, IsComplete = true, Manufacturer = "EXAMPLE DEVICES", Model = "Fieldbook 14 / synthetic specimen", CompletedAt = DateTimeOffset.Now };
        var cpu = new Component { Category = "Processor", Name = "Example 8-core processor", Rating = Rating.NotVerified, Explanation = "Illustrative specifications only. No benchmark or stress test has been performed." };
        cpu.Add("Cores / logical processors", "8 / 16", "Synthetic sample"); cpu.Add("Maximum reported clock", "3200 MHz", "Synthetic sample");
        var ram = new Component { Category = "Memory", Name = "16 GiB DDR4", Rating = Rating.Good, Explanation = "Meets the app's 8 GiB general-use capacity policy. Capacity is not a memory integrity test." };
        ram.Add("Modules", "2 × 8 GiB; 3200 MT/s", "Synthetic sample");
        var disk = new Component { Category = "Storage", Name = "Example NVMe SSD", Rating = Rating.Good, Explanation = "Illustrative provider health is Healthy. A healthy provider status does not guarantee drive reliability." };
        disk.Add("Capacity", "476.94 GiB", "Synthetic sample"); disk.Add("Power-on hours", "1240 hours", "Synthetic sample"); disk.Add("Wear used", "6%", "Synthetic sample");
        var battery = new Component { Category = "Battery", Name = "Example internal battery", Rating = Rating.Caution, Explanation = "Estimated capacity retention is 76%. Below the app's 80% service-review threshold; runtime depends on workload." };
        battery.Add("Design / full charge", "50000 / 38000 mWh", "Synthetic sample"); battery.Add("Capacity retention", "76%", "Synthetic sample", Evidence.Estimated); battery.Add("Cycle count", null, "Synthetic sample"); battery.Recommendations.Add("Consider battery service if runtime no longer meets your needs.");
        var display = new Component { Category = "Display & graphics", Name = "Example integrated graphics", Explanation = "Display pixel quality and GPU stability require manual checks." };
        display.Add("Current mode", "1920 × 1080 at 60 Hz", "Synthetic sample");
        var board = new Component { Category = "Motherboard & BIOS", Name = "Example system board", Explanation = "Inventory only; firmware security and board health are not inferred from version numbers." };
        board.Add("BIOS", "Example UEFI 1.0", "Synthetic sample");
        var system = new Component { Category = "Windows & security", Name = "Example Windows 11 system", Explanation = "Synthetic security state; no claim about the computer running this report." };
        system.Add("Secure Boot", "Enabled", "Synthetic sample");
        var network = new Component { Category = "Network & Bluetooth", Name = "Example wireless adapter", Explanation = "Detected adapters do not verify wireless performance or pairing." };
        network.Add("Bluetooth", "Example Bluetooth radio", "Synthetic sample");
        var thermal = new Component { Category = "Temperature & reliability", Name = "Sensor coverage", Explanation = "No CPU/GPU temperature or stability testing is included in this sample." };
        thermal.Add("CPU temperature", null, "Synthetic sample");
        cpu.Name = "Intel Core i7-1260P (synthetic example)";
        cpu.Measurements.Clear(); cpu.Add("Physical cores",12,"Synthetic sample");cpu.Add("Logical processors",16,"Synthetic sample");cpu.Add("Maximum reported clock","4700 MHz","Synthetic sample");cpu.Add("L3 cache","18432 KiB","Synthetic sample");cpu.Add("Generation","Intel Core generation 12","Synthetic sample",Evidence.Estimated);
        ram.Measurements.Clear();ram.Add("Installed capacity","16 GiB","Synthetic sample");ram.Add("Populated memory devices",1,"Synthetic sample");ram.Add("Empty memory devices",1,"Synthetic sample");
        var slotA = new Component { Category = "Memory slot", Name = "DIMM A / BANK 0", Explanation = "Synthetic populated module. Effective rate and estimated DDR clock are shown separately." };
        slotA.Add("Slot state","Populated","Synthetic sample");slotA.Add("Module capacity","16 GiB","Synthetic sample");slotA.Add("Manufacturer","Example Memory Co.","Synthetic sample");slotA.Add("Part number","EX16G4S2666","Synthetic sample");slotA.Add("Memory type","DDR4","Synthetic sample");slotA.Add("Configured data rate","2666 MT/s","Synthetic sample");slotA.Add("Rated data rate","3200 MT/s","Synthetic sample");slotA.Add("Equivalent DDR clock","1333 MHz","Synthetic sample",Evidence.Estimated);slotA.Add("Form factor","SO-DIMM","Synthetic sample");slotA.Add("Ranks",2,"Synthetic sample");slotA.Add("Configured voltage","1.2 V","Synthetic sample");
        var slotB = new Component { Category = "Memory slot", Name = "DIMM B / BANK 2", Explanation = "Synthetic empty memory-device record. Physical upgrade access still requires verification." };
        slotB.Add("Slot state","Empty (firmware reported)","Synthetic sample");
        battery.Measurements.Clear();battery.Add("Design capacity","50000 mWh","Synthetic sample");battery.Add("Full-charge capacity","38000 mWh","Synthetic sample");battery.Add("Current remaining capacity","28500 mWh","Synthetic sample");battery.Add("Capacity retention","76%","Synthetic sample",Evidence.Estimated);battery.Add("Current charge","75%","Synthetic sample",Evidence.Estimated);battery.Add("Capacity wear","24%","Synthetic sample",Evidence.Estimated);battery.Add("Condition","Reduced capacity / service review","Synthetic sample",Evidence.Estimated);battery.Add("Cycle count",318,"Synthetic sample");battery.Add("Voltage","11.84 V","Synthetic sample");battery.Add("Power state","Discharging","Synthetic sample");
        disk.Measurements.Clear();disk.Add("Capacity","476.94 GiB","Synthetic sample");disk.Add("Bus / interface","NVMe / PCIe","Synthetic sample");disk.Add("Firmware version","EX2.1","Synthetic sample");disk.Add("Power-on hours","4812 hours","Synthetic sample");disk.Add("Power cycles",742,"Synthetic sample");disk.Add("Wear used","7% of rated endurance used","Synthetic sample");disk.Add("Drive temperature","37 °C","Synthetic sample");disk.Add("Host data written","12.48 TB","Synthetic sample");disk.Add("SMART overall result","Passed (drive-reported)","Synthetic sample");disk.Add("Media / data integrity errors",0,"Synthetic sample");
        i.Components.AddRange([cpu, ram, slotA, slotB, disk, battery, display, board, system, network, thermal]);
        i.Limitations.Add("SAMPLE ONLY. All hardware values are synthetic and must not be used to assess a real laptop.");
        i.Limitations.Add(Branding.Disclaimer);
        return i;
    }
}
