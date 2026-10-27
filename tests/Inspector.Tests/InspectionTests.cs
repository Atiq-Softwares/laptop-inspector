using System.Text.Json;
using System.IO;
using Inspector.App.Reports;
using Inspector.Core;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Inspector.Tests;

public class InspectionTests
{
    [Theory]
    [InlineData(50000, 40000, 80)]
    [InlineData(50000, 25000, 50)]
    [InlineData(50000, 51000, 102)]
    public void BatteryRatioUsesFullChargeNotCurrentCharge(double design, double full, double expected) => Assert.Equal(expected, Evaluation.BatteryHealth(design, full));

    [Theory]
    [InlineData(null, 40000d)] [InlineData(50000d, null)] [InlineData(0d, 2000d)]
    [InlineData(50000d, 0d)] [InlineData(-1d, 2000d)] [InlineData(50000d, 65000d)]
    public void MissingOrImplausibleBatteryCapacityDoesNotBecomeHealth(double? design, double? full) => Assert.Null(Evaluation.BatteryHealth(design, full));

    [Theory]
    [InlineData(null, Rating.NotVerified)] [InlineData(49.9, Rating.Critical)]
    [InlineData(50d, Rating.Caution)] [InlineData(79.9, Rating.Caution)] [InlineData(80d, Rating.Good)]
    public void BatteryPolicyBoundaries(double? percent, Rating expected) => Assert.Equal(expected, Evaluation.BatteryRating(percent));

    [Theory]
    [InlineData(null, null, null, Rating.NotVerified)]
    [InlineData(0, null, null, Rating.Good)]
    [InlineData(0, true, 0, Rating.Critical)]
    [InlineData(2, false, null, Rating.Critical)]
    [InlineData(0, false, 80, Rating.Caution)]
    [InlineData(0, false, 100, Rating.Critical)]
    [InlineData(1, null, 0, Rating.Caution)]
    public void DriveWarningsTakePriorityOverHealthyProviderStatus(int? health, bool? prediction, int? wear, Rating expected) => Assert.Equal(expected, Evaluation.StorageRating(health, prediction, wear));

    [Fact]
    public void SmartParserReadsSixByteLittleEndianRawWithoutInventingUnits()
    {
        var bytes = new byte[512]; bytes[2] = 9; bytes[5] = 98; bytes[6] = 92;
        for (int n = 0; n < 6; n++) bytes[7 + n] = (byte)(n + 1);
        var a = Assert.Single(SmartParser.Parse(bytes));
        Assert.Equal((byte)9, a.Id); Assert.Equal((byte)98, a.Current); Assert.Equal((byte)92, a.Worst); Assert.Equal(0x060504030201UL, a.Raw);
    }
    [Fact] public void TruncatedSmartTableIsUnavailable() => Assert.Empty(SmartParser.Parse(new byte[128]));
    [Fact] public void EmptySmartSlotsAreSkipped() => Assert.Empty(SmartParser.Parse(new byte[512]));

    [Theory]
    [InlineData("Intel Core i7-8650U", "8")]
    [InlineData("Intel Core i5-1235U", "12")]
    [InlineData("Intel Core i7-1065G7", "10")]
    [InlineData("Intel Core i5-1135G7", "11")]
    [InlineData("Intel Core i7-12700H", "12")]
    public void LegacyIntelGenerationParsing(string name, string generation) => Assert.Contains("generation " + generation + " ", Evaluation.IntelGeneration(name));
    [Theory] [InlineData("Intel Core Ultra 7 155H")] [InlineData("AMD Ryzen 7 7840U")] [InlineData("Unknown")]
    public void UnsupportedCpuNamingDoesNotGuessGeneration(string name) => Assert.Null(Evaluation.IntelGeneration(name));

    [Fact]
    public void EmptyInventoryAndPendingManualChecksCannotYieldOverallGood()
    {
        var i = new Inspection { IsComplete = true }; Assert.Equal(Rating.NotVerified, i.Overall);
        i.Components.Add(new() { Rating = Rating.Good }); Assert.Equal(Rating.NotVerified, i.Overall);
        foreach (var m in i.ManualChecks) m.Outcome = ManualOutcome.Pass;
        Assert.Equal(Rating.Good, i.Overall);
        i.Components.Add(new() { Rating = Rating.NotVerified }); Assert.Equal(Rating.NotVerified, i.Overall);
    }
    [Fact]
    public void FailedManualCheckOverridesComponentRatings()
    {
        var i = Sample.Create(); i.ManualChecks[0].Outcome = ManualOutcome.Fail; Assert.Equal(Rating.Critical, i.Overall);
    }
    [Theory] [InlineData(null, Rating.NotVerified)] [InlineData(0d, Rating.NotVerified)] [InlineData(4d, Rating.Caution)] [InlineData(8d, Rating.Good)]
    public void MemoryCapacityIsNotAnIntegrityPercentage(double? capacity, Rating expected) => Assert.Equal(expected, Evaluation.MemoryRating(capacity));
    [Fact]
    public void NullMeasurementsAreExplicitlyUnavailable()
    {
        var c = new Component(); c.Add("Cycle count", null, "Windows provider");
        Assert.Equal(Evidence.Unavailable, c.Measurements[0].Evidence); Assert.Equal("Not available", c.Measurements[0].Value);
    }

    [Fact]
    public void ExportsDropSensitiveLabelsAndManualNotesWithoutMutatingInspection()
    {
        var i = Sample.Create(); i.ManualChecks[0].Notes = "private operator note";
        foreach (var label in new[] { "Serial number", "UUID", "MAC address", "IP address", "SSID", "Computer name", "Username", "Product key", "PNPDeviceID", "File path" }) i.Components[0].Measurements.Add(new(label, "PRIVATE_SENTINEL", Evidence.Measured, "Fixture"));
        var json = ReportWriter.Json(i); var html = ReportWriter.Html(i);
        Assert.DoesNotContain("PRIVATE_SENTINEL", json); Assert.DoesNotContain("PRIVATE_SENTINEL", html);
        Assert.DoesNotContain("private operator note", json); Assert.DoesNotContain("private operator note", html);
        Assert.Equal("private operator note", i.ManualChecks[0].Notes);
        Assert.Contains("private operator note", ReportWriter.Json(i, true));
    }
    [Fact]
    public void HtmlEscapesAllOperatorInputAndWorksWithoutRemoteResources()
    {
        var i = Sample.Create(); i.Model = "<script>alert('unsafe')</script>"; i.ManualChecks[0].Notes = "<img src=x onerror=alert(1)>";
        var html = ReportWriter.Html(i, true);
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<img", html); Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<link", html); Assert.DoesNotContain("<script", html);
        Assert.Contains("SAMPLE REPORT - SYNTHETIC DATA", html); Assert.Contains(Branding.Copyright, html); Assert.Contains("href=\"" + Branding.GitHub, html);
    }
    [Fact]
    public void JsonContainsEvaluationEvidenceBrandingAndSampleFlag()
    {
        using var doc = JsonDocument.Parse(ReportWriter.Json(Sample.Create()));
        Assert.Equal("Caution", doc.RootElement.GetProperty("overallRating").GetString());
        var report = doc.RootElement.GetProperty("report");
        Assert.True(report.GetProperty("IsSample").GetBoolean());
        Assert.Equal(Branding.Copyright, report.GetProperty("Copyright").GetString());
        Assert.Equal("Measured", report.GetProperty("Components")[0].GetProperty("Measurements")[0].GetProperty("Evidence").GetString());
    }
    [Fact]
    public void PdfHasA4PagesBrandingMetadataAndClickableFooterLinks()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            ReportWriter.Write(Sample.Create(), path);
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Assert.Equal(Branding.Publisher, doc.Info.Author); Assert.True(doc.PageCount >= 3);
            foreach (var page in doc.Pages) { Assert.InRange(page.Width.Point, 595, 596); Assert.InRange(page.Height.Point, 841, 843); Assert.True(page.Annotations.Count >= 1); }
            Assert.True(new FileInfo(path).Length > 10000);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void PdfPaginatesLongNamesAndManyMeasurements()
    {
        var i = Sample.Create(); i.Components[0].Name = new string('X', 240);
        for (int n = 0; n < 100; n++) i.Components[0].Add("Reading " + n, string.Join(' ', Enumerable.Repeat("value", 35)), "Long source used to verify pagination");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try { ReportWriter.Write(i, path); using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import); Assert.True(doc.PageCount > 10); }
        finally { File.Delete(path); }
    }
}
