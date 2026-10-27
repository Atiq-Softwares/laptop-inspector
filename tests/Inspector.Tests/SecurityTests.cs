using System.IO;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inspector.App.Diagnostics;
using Inspector.App.Reports;
using Inspector.Core;
using Xunit;

namespace Inspector.Tests;

public class SecurityTests
{
    [Fact] public async Task StreamingLimitRejectsBeforeConsumingTheRest()
    {
        using var reader=new StringReader(new string('x',100_000));
        await Assert.ThrowsAsync<InvalidDataException>(()=>BoundedText.ReadAsync(reader,32));
        Assert.Equal('x',reader.Read());
    }
    [Fact] public async Task StreamingLimitAcceptsExactBoundaryAndEmptyResponse()
    {
        Assert.Equal("abc",await BoundedText.ReadAsync(new StringReader("abc"),3));Assert.Equal("",await BoundedText.ReadAsync(new StringReader(""),0));
    }
    [Fact] public async Task OversizedProgressLineIsRejected()
        => await Assert.ThrowsAsync<InvalidDataException>(()=>BoundedText.ReadLinesAsync(new StringReader(new string('x',1000)),32,2000,_=>{},CancellationToken.None));
    [Fact] public async Task ProgressTotalIsBoundedAcrossShortLines()
        => await Assert.ThrowsAsync<InvalidDataException>(()=>BoundedText.ReadLinesAsync(new StringReader(string.Concat(Enumerable.Repeat("1|CPU\n",100))),32,64,_=>{},CancellationToken.None));
    [Fact] public async Task BoundedReaderHonorsCancellation()
    {
        using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>BoundedText.ReadAsync(new StringReader("abc"),10,cancel.Token));
    }
    [Theory][InlineData("[]")][InlineData("null")][InlineData("42")]
    public void DriveRootMustBeAnObject(string json)=>Assert.Throws<JsonException>(()=>DriveSmart.Parse(json));
    [Fact] public void DeepSmartJsonIsRejected()=>Assert.ThrowsAny<JsonException>(()=>DriveSmart.Parse(new string('[',32)+"0"+new string(']',32)));
    [Fact] public void OversizedSmartJsonIsRejected()=>Assert.Throws<JsonException>(()=>DriveSmart.Parse(new string(' ',2_000_001)));
    [Fact] public void TooManyAtaAttributesAreRejected()=>Assert.Throws<JsonException>(()=>DriveSmart.Parse("{\"ata_smart_attributes\":{\"table\":["+string.Join(',',Enumerable.Repeat("{}",65))+"]}}"));
    [Fact] public void CapacityDoesNotOverflowFromFloatingPointRounding()
    {
        Assert.Null(DriveSmart.Parse("{\"user_capacity\":{\"bytes\":9223372036854775808}}").Capacity);
        Assert.Equal(long.MaxValue,DriveSmart.Parse("{\"user_capacity\":{\"bytes\":9223372036854775807}}").Capacity);
    }
    [Fact] public void DerivedInfiniteCountersAreRejected()=>Assert.Throws<JsonException>(()=>DriveSmart.Parse("{\"nvme_smart_health_information_log\":{\"data_units_written\":1e308}}"));
    [Fact] public void MalformedInspectionCannotReachExporter()
    {
        var i=Sample.Create();i.Components[0].Rating=(Rating)999;Assert.Throws<InvalidDataException>(()=>ReportWriter.Html(i));
        i=Sample.Create();i.Components[0].Measurements=null!;Assert.Throws<InvalidDataException>(()=>ReportWriter.Json(i));
    }
    [Fact] public void InspectionCountsAndTextAreBounded()
    {
        var i=Sample.Create();i.Model=new string('x',513);Assert.Throws<InvalidDataException>(()=>InspectionValidation.Check(i));
        i=Sample.Create();i.ManualChecks[0].Notes=new string('x',1001);Assert.Throws<InvalidDataException>(()=>InspectionValidation.Check(i));
        i=Sample.Create();i.Components=Enumerable.Range(0,257).Select(_=>new Component()).ToList();Assert.Throws<InvalidDataException>(()=>InspectionValidation.Check(i));
    }
    [Fact] public void NumericEnumInjectionIsRejectedByTransport()=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<Measurement>("{\"Name\":\"a\",\"Value\":\"b\",\"Evidence\":99,\"Source\":\"c\"}",JsonDefaults.Options));
    [Fact] public void HtmlHasContentPolicyAndStillEscapesInput()
    {
        var i=Sample.Create();i.Model="<iframe src=\"file:///private\"></iframe>";var html=ReportWriter.Html(i);Assert.Contains("Content-Security-Policy",html);Assert.Contains("script-src 'none'",html);Assert.Contains("object-src 'none'",html);Assert.DoesNotContain("<iframe",html);Assert.Contains("&lt;iframe",html);Assert.Contains(Branding.Copyright,html);
    }
    [Fact] public void FailedExportPreservesPreviousFileAndRemovesTemporaryFile()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var file=Path.Combine(dir,"report.html");File.WriteAllText(file,"previous");
        try { Assert.Throws<IOException>(()=>AtomicReportFile.Write(file,s=>{s.Write(Encoding.UTF8.GetBytes("partial"));throw new IOException("simulated failure");}));Assert.Equal("previous",File.ReadAllText(file));Assert.Single(Directory.GetFiles(dir)); }
        finally {Directory.Delete(dir,true);}
    }
    [Fact] public void AtomicExportWritesOnlySupportedFormats()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {Assert.Throws<ArgumentException>(()=>AtomicReportFile.Write(Path.Combine(dir,"app.exe"),_=>{}));var file=Path.Combine(dir,"report.json");AtomicReportFile.Write(file,s=>s.Write(Encoding.UTF8.GetBytes("{}")));Assert.Equal("{}",File.ReadAllText(file));Assert.Single(Directory.GetFiles(dir));}
        finally {Directory.Delete(dir,true);}
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string link,string existing,IntPtr reserved);
    [Fact] public void ExportReplacesAnExistingHardLinkWithoutModifyingItsOtherName()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var original=Path.Combine(dir,"original.html");var report=Path.Combine(dir,"report.html");
        try
        {
            File.WriteAllText(original,"original");Assert.True(CreateHardLink(report,original,IntPtr.Zero));
            AtomicReportFile.Write(report,s=>s.Write(Encoding.UTF8.GetBytes("new report")));
            Assert.Equal("original",File.ReadAllText(original));Assert.Equal("new report",File.ReadAllText(report));
        }
        finally {Directory.Delete(dir,true);}
    }
    [Fact] public void VerifiedDriveReaderCannotBeReplacedWhileInUse()
    {
        var dir=Path.Combine(AppContext.BaseDirectory,"third-party","smartmontools");using var verified=new VerifiedEngineFiles(dir);
        Assert.Throws<IOException>(()=>File.Open(verified.Executable,FileMode.Open,FileAccess.Write,FileShare.ReadWrite));
        Assert.Throws<IOException>(()=>File.Open(verified.Database,FileMode.Open,FileAccess.Write,FileShare.ReadWrite));
    }
    [Fact] public void TamperedDriveReaderIsRejectedBeforeExecution()
    {
        var original=Path.Combine(AppContext.BaseDirectory,"third-party","smartmontools");var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {File.Copy(Path.Combine(original,"smartctl.exe"),Path.Combine(dir,"smartctl.exe"));File.Copy(Path.Combine(original,"drivedb.h"),Path.Combine(dir,"drivedb.h"));File.AppendAllText(Path.Combine(dir,"drivedb.h"),"tamper");Assert.Throws<IOException>(()=>new VerifiedEngineFiles(dir));}
        finally {Directory.Delete(dir,true);}
    }
    [Fact] public void OwnedReleaseAssembliesContainNoEmbeddedDebugSymbols()
    {
        foreach(var assembly in new[]{typeof(Inspection).Assembly,typeof(ReportWriter).Assembly}){using var stream=File.OpenRead(assembly.Location);using var pe=new PEReader(stream);Assert.DoesNotContain(pe.ReadDebugDirectory(),e=>e.Type==DebugDirectoryEntryType.EmbeddedPortablePdb);}
    }
}
