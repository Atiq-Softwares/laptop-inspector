using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Inspector.App.Diagnostics;
using Inspector.Core;
using Xunit;

namespace Inspector.Tests;

public class ExpandedDiagnosticsTests
{
    private static byte[] Device(ushort size=16384,ushort speed=3200,ushort configured=2666)
    {
        var data=new byte[0x5C];data[0]=17;data[1]=(byte)data.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0C),size);data[0x0E]=12;data[0x10]=1;data[0x11]=2;data[0x12]=26;data[0x17]=3;data[0x18]=4;data[0x1A]=5;data[0x1B]=2;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x15),speed);BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x20),configured);BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x26),1200);
        return data.Concat(Encoding.ASCII.GetBytes("DIMM A\0BANK 0\0Example Memory\0PRIVATE_SERIAL_SENTINEL\0PART-16G\0\0")).ToArray();
    }
    [Fact] public void FirmwareSlotHasCapacityPartAndSeparateConfiguredRate()
    {
        var d=Assert.Single(MemoryInventory.Parse(Device()));Assert.Equal("DIMM A",d.Locator);Assert.Equal(16UL*1073741824,d.CapacityBytes);Assert.Equal(3200u,d.RatedRate);Assert.Equal(2666u,d.ConfiguredRate);Assert.Equal("PART-16G",d.PartNumber);Assert.Equal(1200,d.VoltageMv);Assert.False(d.Empty);Assert.Equal(1333,MemoryInventory.DdrClock(d.ConfiguredRate,d.Type));Assert.DoesNotContain("PRIVATE_SERIAL_SENTINEL",JsonSerializer.Serialize(d));
    }
    [Fact] public void EmptySlotIsExplicitAndUnknownSizeIsNotCalledEmpty()
    {
        Assert.True(Assert.Single(MemoryInventory.Parse(Device(0))).Empty);var unknown=Assert.Single(MemoryInventory.Parse(Device(ushort.MaxValue)));Assert.False(unknown.Empty);Assert.Null(unknown.CapacityBytes);
    }
    [Fact] public void ExtendedSizeAndExtendedSpeedsAreDecoded()
    {
        var data=Device(0x7FFF,ushort.MaxValue,ushort.MaxValue);BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x1C),65536);BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x54),8800);BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x58),8000);
        var slot=Assert.Single(MemoryInventory.Parse(data));Assert.Equal(64UL*1073741824,slot.CapacityBytes);Assert.Equal(8000u,slot.ConfiguredRate);Assert.Equal(8800u,slot.RatedRate);
    }
    [Fact] public void MultipleFirmwareSlotsAreNotCollapsed() => Assert.Equal(2,MemoryInventory.Parse(Device().Concat(Device(0)).ToArray()).Count);
    [Theory][InlineData(0)][InlineData(3)][InlineData(16)][InlineData(92)] public void TruncatedTablesDoNotReadPastBuffer(int length)=>Assert.Empty(MemoryInventory.Parse(Device()[..length]));
    [Fact] public void UnterminatedFirmwareStringSetIsRejected() => Assert.Empty(MemoryInventory.Parse(Device()[..^2]));
    [Fact] public void UnknownDdrTypeDoesNotInventClock()=>Assert.Null(MemoryInventory.DdrClock(2666,0));
    [Theory][InlineData(28500d,38000d,75d)][InlineData(0d,38000d,0d)][InlineData(null,38000d,null)][InlineData(100d,0d,null)][InlineData(-1d,100d,null)]
    public void ChargeIsIndependentOfDesignCapacity(double? remaining,double? full,double? expected)=>Assert.Equal(expected,Evaluation.BatteryCharge(remaining,full));
    [Fact] public void NvmeCountersUseStandardUnitsAndExcludeSerial()
    {
        const string json="""{"model_name":"Example NVMe","serial_number":"PRIVATE_SERIAL_SENTINEL","firmware_version":"1.0","user_capacity":{"bytes":512000000000},"device":{"protocol":"NVMe"},"smart_status":{"passed":false},"nvme_smart_health_information_log":{"power_on_hours":41234,"power_cycles":500,"percentage_used":83,"available_spare":95,"data_units_read":1000,"data_units_written":2000,"media_errors":3,"unsafe_shutdowns":14}}""";
        var f=DriveSmart.Parse(json);Assert.Equal(41234,f.Hours);Assert.Equal(83,f.WearUsed);Assert.Equal(1024000000,f.WrittenBytes);Assert.Equal(3,f.MediaErrors);Assert.False(f.Passed);Assert.DoesNotContain("PRIVATE_SERIAL_SENTINEL",JsonSerializer.Serialize(f));
        var c=new Component();DriveSmart.Apply(c,f);Assert.Equal(Rating.Critical,c.Rating);
    }
    [Fact] public void AtaPartialIdentityStillPreservesRealOperatingHours()
    {
        var f=DriveSmart.Parse("""{"model_name":"Example SATA SSD","power_on_time":{"hours":52518},"power_cycle_count":2728,"endurance_used":{"current_percent":12},"smart_status":{"passed":true}}""");Assert.Null(f.Capacity);Assert.Equal(52518,f.Hours);Assert.Equal(12,f.WearUsed);
        var c=new Component{DiskIndex=0,Name="Example SATA SSD",CapacityBytes=500000000000};Assert.True(SmartEngine.Matches(c,new(0,f)));Assert.False(SmartEngine.Matches(c,new(1,f)));c.Name="Different SSD";Assert.False(SmartEngine.Matches(c,new(0,f)));
    }
    [Fact] public void MissingSmartCountersRemainNull() {var f=DriveSmart.Parse("{}");Assert.Null(f.Hours);Assert.Null(f.WearUsed);Assert.Null(f.Passed);Assert.Empty(f.Attributes);}
    [Fact] public void NegativeSmartCountersAreNotAccepted(){var f=DriveSmart.Parse("""{"power_on_time":{"hours":-1},"temperature":{"current":null}}""");Assert.Null(f.Hours);Assert.Null(f.Temperature);}
    [Fact] public void ExportRemovesInternalDriveMappingWithoutMutatingWorkerData()
    {
        var i=Sample.Create();i.Components[0].DiskIndex=42;var prepared=ExportPrivacy.Prepare(i);Assert.Null(prepared.Components[0].DiskIndex);Assert.Equal(42,i.Components[0].DiskIndex);Assert.DoesNotContain("DiskIndex",JsonSerializer.Serialize(prepared,JsonDefaults.Options));
    }
}
