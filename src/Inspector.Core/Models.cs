using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inspector.Core;

public enum Rating { Good, Caution, Critical, NotVerified }
public enum Evidence { Measured, Estimated, Unavailable, Manual }
public enum ManualOutcome { NotTested, Pass, Fail, NotApplicable }

public sealed record Measurement(string Name, string Value, Evidence Evidence, string Source);
public sealed class Component
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DiskIndex { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? CapacityBytes { get; set; }
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public Rating Rating { get; set; } = Rating.NotVerified;
    public string Explanation { get; set; } = "";
    public List<Measurement> Measurements { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public void Add(string name, object? value, string source, Evidence evidence = Evidence.Measured)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        Measurements.Add(string.IsNullOrWhiteSpace(text)
            ? new(name, "Not available", Evidence.Unavailable, source)
            : new(name, text, evidence, source));
    }
}

public sealed class ManualCheck
{
    public string Name { get; set; } = "";
    public string Instructions { get; set; } = "";
    public ManualOutcome Outcome { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class Inspection
{
    public string SchemaVersion { get; set; } = "1.0";
    public string ApplicationVersion { get; set; } = "1.1.0";
    public string Publisher { get; set; } = Branding.Publisher;
    public string Copyright { get; set; } = Branding.Copyright;
    public string GitHub { get; set; } = Branding.GitHub;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? CompletedAt { get; set; }
    public string Manufacturer { get; set; } = "Not available";
    public string Model { get; set; } = "Not available";
    public bool IsSample { get; set; }
    public bool IsComplete { get; set; }
    public List<Component> Components { get; set; } = [];
    public List<ManualCheck> ManualChecks { get; set; } = ManualCatalog.Create();
    public List<string> Limitations { get; set; } = [];
    [JsonIgnore] public Rating Overall => Evaluation.Overall(this);
    [JsonIgnore] public string Assessment => Evaluation.Assessment(this);
}

public static class Branding
{
    public const string Publisher = "Atiq-Softwares";
    public const string Copyright = "Copyright © 2026 Atiq-Softwares.";
    public const string DevelopedBy = "Developed by Atiq-Softwares";
    public const string GitHub = "https://github.com/Atiq-Softwares";
    public const string Disclaimer = "This is a point-in-time inspection, not a guarantee of future reliability. Unavailable measurements and untested functions require separate verification.";
    public const string Privacy = "Reports exclude usernames, computer names, serial numbers, product keys, IP/MAC addresses, SSIDs and file paths by design. Review manual notes before sharing; free text may contain personal information.";
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, MaxDepth = 16, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
}

public static class ManualCatalog
{
    public static List<ManualCheck> Create() => [
        new() { Name = "Keyboard", Instructions = "Open the keyboard checker. Press every letter, number, modifier, arrow and function key. Check the layout, backlight and repeated/missing input. The checker only holds key names in memory; it does not save typed text." },
        new() { Name = "Display", Instructions = "Open the display test. Cycle through solid colors, inspect every corner for dead pixels, lines, bleeding and flicker. Check brightness, hinges and touch input if present. Escape closes the test." },
        new() { Name = "Webcam", Instructions = "Open Windows Camera from Start. Observe a live picture, focus, lens condition and any privacy shutter. Close Camera when done. This app does not record or request camera access." },
        new() { Name = "Microphone", Instructions = "Open Windows Sound settings. Select the intended input and speak while watching its input meter or using Windows' microphone test. Verify clarity with your own recording if needed; delete it afterward." },
        new() { Name = "Speakers", Instructions = "Use Windows Sound settings to test left and right channels at a comfortable volume. Listen for distortion, rattles and imbalance. Test the headphone jack separately." },
        new() { Name = "Touchpad", Instructions = "Test pointer movement, tap, physical left/right clicks, scrolling and multi-finger gestures. Check for lifting or bulging, which can indicate battery swelling. Stop using a swollen battery and seek service." },
        new() { Name = "Ports & connectivity", Instructions = "With known-good accessories, test each USB port, video output, charging port, audio jack and card reader. Test Wi-Fi internet and Bluetooth pairing yourself. Adapter detection alone does not verify connectivity." },
        new() { Name = "Physical condition", Instructions = "Inspect the case, screws, hinges, charger, feet and vents. Check for cracks, liquid damage, swelling and unusual noise. Confirm seller ownership and warranty separately; this app does not verify them." }
    ];
}
