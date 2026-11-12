using System.Buffers;
using System.Buffers.Binary;
using MemoryPack;
using Seamless.Database.Client;
using Xunit.Abstractions;

namespace Management.Tests;

public sealed class NativeRequestValidation(ITestOutputHelper output){
    [Theory][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public void OlderTrailingDefaultsRemainReadable(int members){
        var bytes=Request(members,[1,2,3]);
        for(var split=0;split<=bytes.Length;split++){var sequence=Split(bytes,split);NativeQueryRequestWire.Validate(sequence);if(members==8){NativeWireValidation.ValidateSequence<NativeQueryRequest>(sequence);}}
        var request=MemoryPackSerializer.Deserialize<NativeQueryRequest>(bytes)!;Assert.Equal("module",request.ModuleId);Assert.Equal(new byte[]{1,2,3},request.Arguments);Assert.Equal(Guid.Empty,request.TransactionId);Assert.Null(request.ArgumentsContract);Assert.Null(request.ResultContract);
    }
    [Fact]public void LargeArgumentBytesAreSkippedWithoutTheDefaultArrayLimit(){
        var arguments=new byte[70000];var bytes=Request(8,arguments);NativeQueryRequestWire.Validate(Split(bytes,33));Assert.Equal(arguments,MemoryPackSerializer.Deserialize<NativeQueryRequest>(bytes)!.Arguments);
    }
    [Theory][InlineData(int.MinValue)][InlineData(int.MaxValue)][InlineData(-2)]
    public void InvalidMetadataLengthsAreRejectedWithoutDeserializing(int length){
        var bytes=new byte[32];bytes[0]=8;BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1),length);Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(bytes)));
    }
    [Fact]public void InflatedUtf16CountCannotUseTrailingArgumentBytesAsItsBudget(){
        var bytes=Request(8,new byte[70000]);Assert.True(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(1))< -1);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(5),100000);
        Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(Split(bytes,4)));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(5),2);Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(bytes)));
    }
    [Fact]public void NullRequiredMetadataAndInvalidMemberCountsAreRejected(){
        foreach(var count in new byte[]{0,4,9,255}){var bytes=Request(8,[]);bytes[0]=count;Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(bytes)));}
        var missing=new byte[]{8,255,255,255,255};Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(missing)));
    }
    [Fact]public void TruncationAndTrailingDataAreRejected(){
        var bytes=Request(8,[1,2,3]);for(var length=0;length<bytes.Length;length++){var truncated=bytes[..length];Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(truncated)));}
        Assert.Throws<InvalidDataException>(()=>NativeQueryRequestWire.Validate(new(bytes.Concat(new byte[]{0}).ToArray())));
    }
    [Fact]public void GenericSequenceValidationMatchesSpanAcrossUtf8Boundaries(){
        var value=string.Concat(Enumerable.Repeat("日本語😀",120));var bytes=MemoryPackSerializer.Serialize(value);
        for(var split=0;split<=bytes.Length;split++){NativeWireValidation.ValidateSequence<string>(Split(bytes,split));}
        var integers=MemoryPackSerializer.Serialize(new[]{1,2,3});for(var split=0;split<=integers.Length;split++){NativeWireValidation.ValidateSequence<int[]>(Split(integers,split));}
        bytes[^1]=0xff;for(var split=0;split<=bytes.Length;split++){Assert.Throws<InvalidDataException>(()=>NativeWireValidation.ValidateSequence<string>(Split(bytes,split)));}
    }
    [Fact]public void SegmentedArgumentsDoNotRequireAFrameSizedValidationCopy(){
        var bytes=Request(8,new byte[2*1024*1024]);var sequence=Split(bytes,bytes.Length/2);for(var warmup=0;warmup<3;warmup++){NativeQueryRequestWire.Validate(sequence);}
        var before=GC.GetAllocatedBytesForCurrentThread();NativeQueryRequestWire.Validate(sequence);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        output.WriteLine("Validation allocation for a segmented 2 MiB argument: {0} bytes",allocated);Assert.InRange(allocated,0L,65536L);
    }
    private static byte[] Request(int members,byte[] arguments){
        using var output=new MemoryStream();output.WriteByte((byte)members);
        foreach(var value in new[]{"module","query","db","fingerprint"}){output.Write(MemoryPackSerializer.Serialize(value));}output.Write(MemoryPackSerializer.Serialize(arguments));
        if(members>=6){output.Write(MemoryPackSerializer.Serialize(Guid.Empty));}if(members>=7){output.Write(MemoryPackSerializer.Serialize<string?>(null));}if(members>=8){output.Write(MemoryPackSerializer.Serialize<string?>(null));}return output.ToArray();
    }
    private static ReadOnlySequence<byte> Split(byte[] bytes,int split){var first=new Segment(bytes.AsMemory(0,split));var last=first.Append(bytes.AsMemory(split));return new(first,0,last,last.Memory.Length);}
    private sealed class Segment:ReadOnlySequenceSegment<byte>{
        internal Segment(ReadOnlyMemory<byte> memory){this.Memory=memory;}
        internal Segment Append(ReadOnlyMemory<byte> memory){var next=new Segment(memory){RunningIndex=this.RunningIndex+this.Memory.Length};this.Next=next;return next;}
    }
}
