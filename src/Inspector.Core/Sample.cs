namespace Inspector.Core;

public static class Sample
{
    public static Inspection Create()
    {
        var i = new Inspection { IsSample = true, IsComplete = true, Manufacturer = "Example manufacturer", Model = "Illustrative laptop - synthetic data", CompletedAt = DateTimeOffset.Now };
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
        i.Components.AddRange([cpu, ram, disk, battery, display, board, system, network, thermal]);
        i.Limitations.Add("SAMPLE ONLY. All hardware values are synthetic and must not be used to assess a real laptop.");
        i.Limitations.Add(Branding.Disclaimer);
        return i;
    }
}
