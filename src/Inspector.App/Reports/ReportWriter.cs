using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Inspector.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Inspector.App.Reports;

public static class ReportWriter
{
    public static void Write(Inspection original, string path, bool includeNotes = false)
    {
        var inspection = ExportPrivacy.Prepare(original, includeNotes);
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".pdf": WritePdf(inspection, path, includeNotes); break;
            case ".html": File.WriteAllText(path, Html(inspection, includeNotes), new UTF8Encoding(false)); break;
            case ".json": File.WriteAllText(path, Json(inspection, includeNotes), new UTF8Encoding(false)); break;
            default: throw new ArgumentException("Choose PDF, HTML or JSON.");
        }
    }

    public static string Json(Inspection source, bool includeNotes = false)
    {
        var i = ExportPrivacy.Prepare(source, includeNotes);
        return JsonSerializer.Serialize(new { report = i, overallRating = i.Overall, assessment = i.Assessment, privacy = Branding.Privacy, manualNotesIncluded = includeNotes }, JsonDefaults.Options);
    }
    public static string Html(Inspection original, bool includeNotes = false)
    {
        var i = ExportPrivacy.Prepare(original, includeNotes);
        static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
        var b = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Atiq-Softwares | Laptop Inspection Report</title><style>");
        b.Append("*{box-sizing:border-box}body{margin:0;background:#eaf0f5;color:#16283c;font:14px/1.6 'Segoe UI',Arial,sans-serif}main{max-width:1020px;margin:36px auto;background:white;padding:48px;border-radius:16px}header{border-top:8px solid #087f79;padding-top:24px}.brand{letter-spacing:3px;font-size:13px;font-weight:700;color:#087f79}h1{font-size:36px;line-height:1.15;letter-spacing:-1px;max-width:650px}h2{font-size:23px;margin-top:36px}h3{font-size:17px;margin:0}.muted{color:#536579}.meta{border-block:1px solid #dce5ed;padding:14px 0}.summary{background:#ecf7f5;border-left:5px solid #087f79;padding:24px;margin:24px 0}.cards{display:flex;gap:12px;flex-wrap:wrap}.stat{flex:1;min-width:130px;background:#f2f5f9;padding:18px;border-radius:10px}.stat strong{font-size:26px;display:block}.badge{font-size:11px;letter-spacing:1px;font-weight:bold;border-radius:5px;padding:5px 9px;display:inline-block}.Good{background:#dff4e9;color:#12653d}.Caution{background:#fff0ce;color:#805100}.Critical{background:#fde3e3;color:#a32525}.NotVerified{background:#e8edf3;color:#43536a}section.component{margin-top:24px;border:1px solid #dce5ed;border-radius:10px;padding:22px}.sectionhead{display:flex;justify-content:space-between;gap:16px}.kicker{color:#087f79;text-transform:uppercase;font-size:11px;letter-spacing:1.5px}table{width:100%;border-collapse:collapse;margin:14px 0}th,td{text-align:left;vertical-align:top;padding:10px 8px;border-bottom:1px solid #e5ebf0;overflow-wrap:anywhere}th{font-size:11px;text-transform:uppercase;color:#536579}td:first-child{width:27%;font-weight:600}td small{display:block;color:#627187}.source{font-size:11px}.notice{border:1px solid #e7c27c;background:#fff8e9;padding:16px;border-radius:8px}li{margin:7px 0}footer{margin-top:40px;border-top:2px solid #087f79;padding-top:18px;font-size:12px;color:#536579}a{color:#087f79}p{overflow-wrap:anywhere}@page{size:A4;margin:16mm}@media print{body{background:white;font-size:10pt}main{padding:0;margin:0;border-radius:0;max-width:none}h1{font-size:28pt}h2,h3,.sectionhead{break-after:avoid}tr,.summary,.stat{break-inside:avoid}section.component{border-radius:0}a{text-decoration:none}footer{break-inside:avoid}header{border-top-width:5px}} </style></head><body><main><header><div class=\"brand\">ATIQ-SOFTWARES</div><h1>LAPTOP INSPECTION REPORT</h1><p class=\"muted\">Hardware Diagnostics &amp; Condition Assessment</p></header>");
        if (i.IsSample) b.Append("<p class=\"notice\"><strong>SAMPLE REPORT - SYNTHETIC DATA.</strong> This is an illustration, not an inspection of a real laptop.</p>");
        b.Append($"<p class=\"meta\"><strong>{E(i.Manufacturer)} / {E(i.Model)}</strong><br>Started: {E(i.StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture))}<br>Completed: {E(i.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "Not completed")} | Application {E(i.ApplicationVersion)}</p>");
        b.Append($"<h2>Executive summary</h2><div class=\"summary\"><span class=\"badge {i.Overall}\">{Evaluation.Label(i.Overall)}</span><p>{E(i.Assessment)}</p><p class=\"muted\">{E(Branding.Disclaimer)}</p></div><div class=\"cards\">");
        foreach (var rating in Enum.GetValues<Rating>()) b.Append($"<div class=\"stat\"><strong>{i.Components.Count(c => c.Rating == rating)}</strong>{Evaluation.Label(rating)} components</div>");
        b.Append("</div><h2>Hardware findings</h2><p class=\"muted\">Measured = Windows/firmware reported; estimated = calculated or heuristic; unavailable = not verified. Specifications alone do not prove functional health.</p>");
        foreach (var c in i.Components)
        {
            b.Append($"<section class=\"component\"><div class=\"sectionhead\"><div><div class=\"kicker\">{E(c.Category)}</div><h3>{E(c.Name)}</h3></div><div><span class=\"badge {c.Rating}\">{Evaluation.Label(c.Rating)}</span></div></div><p>{E(c.Explanation)}</p><table><thead><tr><th>Measurement</th><th>Result &amp; evidence</th></tr></thead><tbody>");
            foreach (var m in c.Measurements) b.Append($"<tr><td>{E(m.Name)}</td><td>{E(m.Value)}<small>{E(m.Evidence.ToString())}</small><small class=\"source\">Source: {E(m.Source)}</small></td></tr>");
            b.Append("</tbody></table>");
            foreach (var recommendation in c.Recommendations) b.Append($"<p><strong>Recommendation:</strong> {E(recommendation)}</p>");
            b.Append("</section>");
        }
        b.Append("<h2>Hardware warnings</h2><ul>");
        var warnings = i.Components.Where(c => c.Rating is Rating.Caution or Rating.Critical).ToList();
        if (warnings.Count == 0) b.Append("<li>No component warnings detected in available data. This does not verify unknown measurements.</li>");
        foreach (var c in warnings) b.Append($"<li><strong>{E(c.Category)}: {Evaluation.Label(c.Rating)}</strong> - {E(c.Name)}. {E(c.Explanation)}</li>");
        foreach (var check in i.ManualChecks.Where(c => c.Outcome == ManualOutcome.Fail)) b.Append($"<li><strong>Manual check failed:</strong> {E(check.Name)}.</li>");
        b.Append("</ul><h2>Guided manual tests</h2><p class=\"muted\">These outcomes are entered by the operator and are not automated measurements. Manual notes are excluded unless explicitly included at export.</p><table><thead><tr><th>Test / outcome</th><th>Procedure / notes</th></tr></thead><tbody>");
        foreach (var c in i.ManualChecks) b.Append($"<tr><td>{E(c.Name)}<small>Manual: {E(Outcome(c.Outcome))}</small></td><td>{E(c.Instructions)}{(string.IsNullOrEmpty(c.Notes) ? "" : "<p><strong>Operator note:</strong> " + E(c.Notes) + "</p>")}</td></tr>");
        b.Append("</tbody></table><h2>Upgrade &amp; service recommendations</h2><ul>");
        foreach (var r in i.Components.SelectMany(c => c.Recommendations).Distinct()) b.Append($"<li>{E(r)}</li>");
        b.Append($"</ul><h2>Buying suitability &amp; final recommendation</h2><p>{E(i.Assessment)}</p><p>Verify your workload, physical condition, real battery runtime, ownership and warranty independently. Resolve warnings before a buying decision. No resale price or reliability guarantee is provided.</p><h2>Coverage &amp; limitations</h2><ul>");
        foreach (var limitation in i.Limitations.Distinct()) b.Append($"<li>{E(limitation)}</li>");
        b.Append($"</ul><p><strong>Privacy:</strong> {E(Branding.Privacy)}</p><footer><strong>{Branding.Copyright}</strong><br>{Branding.DevelopedBy}<br>GitHub: <a href=\"{Branding.GitHub}\">{Branding.GitHub}</a><br>Original code: MIT license. Third-party notices are included with the application.</footer></main></body></html>");
        return b.ToString();
    }
    public static string Outcome(ManualOutcome value) => value switch { ManualOutcome.NotTested => "NOT TESTED", ManualOutcome.NotApplicable => "NOT APPLICABLE", _ => value.ToString().ToUpperInvariant() };

    private static void WritePdf(Inspection i, string path, bool includeNotes)
    {
        using var p = new PdfLayout(i.IsSample);
        p.Kicker("ATIQ-SOFTWARES / INSPECTION SERVICES");
        p.Title("LAPTOP INSPECTION\nREPORT");
        p.Paragraph("Hardware Diagnostics & Condition Assessment", 12, true);
        if (i.IsSample) p.Callout("SAMPLE REPORT - SYNTHETIC DATA. For illustration only; not an inspection of a real laptop.", Rating.Caution);
        p.Heading(i.Manufacturer + " / " + i.Model);
        p.Paragraph("Started: " + i.StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), 9);
        p.Paragraph("Completed: " + (i.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "Not completed") + " | App " + i.ApplicationVersion, 9);
        p.Heading("Executive summary"); p.Callout(Evaluation.Label(i.Overall) + " | " + i.Assessment, i.Overall);
        p.Stats(i);
        p.Paragraph(Branding.Disclaimer, 10, true);
        p.Heading("Read the evidence");
        p.Paragraph("MEASURED: Windows/firmware-reported values. ESTIMATED: calculations or clearly labelled heuristics. UNAVAILABLE: not verified. MANUAL: an operator-entered result. Detected specifications alone do not establish functional health.");
        p.Heading("Inspection scope");
        p.Paragraph("Processor, memory, storage, battery, display and graphics, motherboard and BIOS, Windows and security, networking and available thermal information. Guided manual tests cover the functions software inventory cannot verify.");
        p.NewPage(); p.Heading("Hardware findings");
        foreach (var c in i.Components)
        {
            p.Ensure(120); p.Kicker(c.Category); p.Heading(c.Name);
            p.Status(c.Rating); p.Paragraph(c.Explanation);
            foreach (var m in c.Measurements) p.Row(m.Name, m.Value, m.Evidence + " | " + m.Source);
            foreach (var recommendation in c.Recommendations) p.Paragraph("Recommendation: " + recommendation, 9, true);
            p.Gap(14);
        }
        p.NewPage(); p.Heading("Hardware warnings");
        var warnings = i.Components.Where(c => c.Rating is Rating.Caution or Rating.Critical).ToList();
        if (warnings.Count == 0) p.Paragraph("No component warnings detected in available data. Unknown measurements remain unverified.");
        foreach (var c in warnings) p.Callout(c.Category + ": " + Evaluation.Label(c.Rating) + " - " + c.Explanation, c.Rating);
        foreach (var c in i.ManualChecks.Where(c => c.Outcome == ManualOutcome.Fail)) p.Callout("Manual check failed: " + c.Name, Rating.Critical);
        p.Heading("Guided manual tests");
        p.Paragraph("Operator-entered results. Not automated measurements. Notes are excluded by default; this export " + (includeNotes ? "includes notes." : "excludes notes."), 9, true);
        foreach (var check in i.ManualChecks)
        {
            p.Ensure(85); p.Kicker(check.Name + " / " + Outcome(check.Outcome));
            p.Paragraph(check.Instructions, 9);
            if (!string.IsNullOrEmpty(check.Notes)) p.Paragraph("Operator note: " + check.Notes, 9, true);
        }
        p.Heading("Upgrade & service recommendations");
        foreach (var r in i.Components.SelectMany(c => c.Recommendations).Distinct()) p.Paragraph("- " + r, 10);
        p.Heading("Buying suitability & final recommendation"); p.Callout(i.Assessment, i.Overall);
        p.Paragraph("Verify your workload, physical condition, unplugged runtime, ownership and warranty independently. Resolve warnings before a buying decision. No resale price or reliability guarantee is provided.");
        p.Heading("Coverage & limitations"); foreach (var s in i.Limitations.Distinct()) p.Paragraph("- " + s, 9);
        p.Heading("Local-first privacy"); p.Paragraph(Branding.Privacy, 9);
        p.Paragraph("Original code: MIT license. Third-party license notices are distributed with the portable application.", 9, true);
        p.Save(path);
    }
}

internal sealed class PdfLayout : IDisposable
{
    private readonly PdfDocument doc = new();
    private PdfPage page = null!;
    private XGraphics graphics = null!;
    private double y;
    private readonly bool sample;
    private readonly XColor ink = XColor.FromArgb(22, 40, 60);
    private readonly XColor teal = XColor.FromArgb(8, 127, 121);
    private const double Left = 44, Width = 507, Bottom = 756;
    private static XFont Font(double size, bool bold = false) => new("Segoe UI", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
    public PdfLayout(bool sample)
    {
        this.sample = sample;
        doc.Info.Title = "ATIQ-SOFTWARES - Laptop Inspection Report";
        doc.Info.Author = Branding.Publisher;
        doc.Info.Subject = sample ? "Synthetic sample - not a real inspection" : "Hardware Diagnostics & Condition Assessment";
        NewPage();
    }
    public void NewPage()
    {
        graphics?.Dispose(); page = doc.AddPage(); page.Size = PdfSharp.PageSize.A4;
        graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(new XSolidBrush(teal), 0, 0, page.Width.Point, 8);
        Text("ATIQ-SOFTWARES", Left, 28, Font(9, true), teal);
        Text(sample ? "SAMPLE / SYNTHETIC DATA" : "LAPTOP INSPECTION REPORT", 337, 28, Font(8), ink);
        y = 58;
    }
    public void Ensure(double height) { if (y + height > Bottom) NewPage(); }
    public void Gap(double points) { y += points; }
    public void Kicker(string text) { Ensure(32); foreach (var line in Wrap(text.ToUpperInvariant(), Font(8, true), Width)) { Text(line, Left, y, Font(8, true), teal); y += 12; } y += 4; }
    public void Title(string title) { foreach (var s in title.Split('\n')) { Text(s, Left, y, Font(32, true), ink); y += 39; } y += 10; }
    public void Heading(string text) { Ensure(72); y += 9; foreach (var line in Wrap(text, Font(17, true), Width)) { Ensure(24); Text(line, Left, y, Font(17, true), ink); y += 23; } y += 5; }
    public void Paragraph(string text, double size = 10, bool muted = false)
    {
        var font = Font(size); var lines = Wrap(text, font, Width);
        foreach (var line in lines) { Ensure(size * 1.5 + 2); Text(line, Left, y, font, muted ? XColor.FromArgb(83, 101, 121) : ink); y += size * 1.5; }
        y += 8;
    }
    public void Status(Rating rating)
    {
        Ensure(26); var color = StatusColor(rating); graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(239, 244, 247)), Left, y - 2, 135, 22, 4, 4);
        Text(Evaluation.Label(rating), Left + 10, y + 3, Font(9, true), color); y += 30;
    }
    public void Callout(string text, Rating rating)
    {
        var lines = Wrap(text, Font(10), Width - 32); double height = lines.Count * 15 + 26;
        Ensure(height + 8);
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 246, 247)), Left, y, Width, height);
        graphics.DrawRectangle(new XSolidBrush(StatusColor(rating)), Left, y, 4, height);
        y += 13;
        foreach (var line in lines) { Text(line, Left + 16, y, Font(10), ink); y += 15; }
        y += 21;
    }
    public void Stats(Inspection i)
    {
        Ensure(78); double cell = (Width - 18) / 4;
        int n = 0;
        foreach (var r in Enum.GetValues<Rating>())
        {
            double x = Left + n * (cell + 6); graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(244, 247, 250)), x, y, cell, 64);
            Text(i.Components.Count(c => c.Rating == r).ToString(), x + 10, y + 8, Font(22, true), StatusColor(r));
            Text(Evaluation.Label(r), x + 10, y + 43, Font(7, true), ink); n++;
        }
        y += 79;
    }
    public void Row(string label, string value, string evidence)
    {
        var labels = Wrap(label, Font(9, true), 130);
        var values = Wrap(value, Font(10), 350);
        var sources = Wrap(evidence, Font(8), 350);
        double height = Math.Max(labels.Count * 14, values.Count * 15 + sources.Count * 11 + 5) + 16;
        Ensure(height);
        graphics.DrawLine(new XPen(XColor.FromArgb(220, 229, 237), .5), Left, y, Left + Width, y);
        double ly = y + 8, vy = ly;
        foreach (var line in labels) { Text(line, Left + 5, ly, Font(9, true), ink); ly += 14; }
        foreach (var line in values) { Text(line, Left + 148, vy, Font(10), ink); vy += 15; }
        vy += 3;
        foreach (var line in sources) { Text(line, Left + 148, vy, Font(8), XColor.FromArgb(83, 101, 121)); vy += 11; }
        y += height;
    }
    private List<string> Wrap(string text, XFont font, double width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (graphics.MeasureString(candidate, font).Width <= width) { line = candidate; continue; }
                if (line.Length > 0) { lines.Add(line); line = ""; }
                foreach (char ch in word)
                {
                    if (graphics.MeasureString(line + ch, font).Width > width && line.Length > 0) { lines.Add(line); line = ""; }
                    line += ch;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
    private void Text(string value, double x, double top, XFont font, XColor color) => graphics.DrawString(value, font, new XSolidBrush(color), new XRect(x, top, 0, 0), XStringFormats.TopLeft);
    private static XColor StatusColor(Rating r) => r switch { Rating.Good => XColor.FromArgb(18, 101, 61), Rating.Caution => XColor.FromArgb(138, 88, 0), Rating.Critical => XColor.FromArgb(172, 37, 37), _ => XColor.FromArgb(67, 83, 106) };
    public void Save(string path)
    {
        graphics.Dispose(); graphics = null!;
        for (int n = 0; n < doc.PageCount; n++)
        {
            using var footer = XGraphics.FromPdfPage(doc.Pages[n], XGraphicsPdfPageOptions.Append);
            footer.DrawLine(new XPen(teal, 1), Left, 779, Left + Width, 779);
            footer.DrawString(Branding.Copyright + "  " + Branding.DevelopedBy, Font(7), new XSolidBrush(ink), new XRect(Left, 789, Width, 12), XStringFormats.TopLeft);
            footer.DrawString("GitHub: " + Branding.GitHub, Font(7), new XSolidBrush(teal), new XRect(Left, 802, Width, 12), XStringFormats.TopLeft);
            footer.DrawString($"{n + 1} / {doc.PageCount}", Font(8), new XSolidBrush(ink), new XRect(485, 802, 66, 12), XStringFormats.TopRight);
            var rect = new PdfRectangle(new XPoint(Left, doc.Pages[n].Height.Point - 814), new XPoint(Left + 300, doc.Pages[n].Height.Point - 800));
            doc.Pages[n].AddWebLink(rect, Branding.GitHub);
        }
        doc.Save(path);
    }
    public void Dispose() { graphics?.Dispose(); doc.Dispose(); }
}
