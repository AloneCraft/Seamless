using System.Buffers.Binary;
using MemoryPack;
using Seamless.Database.Client;
using Wire=Seamless.Database.Client.NativeWireValidation;

namespace Seamless.Database.Gateway.Tests;

[MemoryPackable]public sealed partial class WirePayload{
    public string? Name{get;set;}
    public int[]? Values{get;set;}
}
[MemoryPackable]public partial record struct WireRefValue(string? Text);
[MemoryPackable]public sealed partial class WireCycle{public WireCycle? Next{get;set;}}
[MemoryPackable]public sealed partial class WireHook{
    public static int Calls;
    public int Value{get;set;}
    [MemoryPackOnDeserializing]private static void Before(){Calls++;}
}

public sealed class NativeWireValidation{
    [Fact,Trait("Category","Unit")]
    public void Validate(){
        static void Check<T>(T value){
            var bytes=MemoryPackSerializer.Serialize(value);Wire.Validate<T>(bytes);
            for(var length=0;length<bytes.Length;length++){
                var truncated=bytes[..length];Assert.Throws<InvalidDataException>(()=>Wire.Validate<T>(truncated));
            }
            var extra=new byte[bytes.Length+1];bytes.CopyTo(extra,0);Assert.Throws<InvalidDataException>(()=>Wire.Validate<T>(extra));
        }
        Check(42);Check(-42L);Check(true);Check((int?)42);Check((int?)null);Check(Guid.NewGuid());Check(DateTime.UtcNow);Check(DateTimeOffset.UtcNow);Check(DateOnly.FromDateTime(DateTime.Today));
        Check<string?>(null);Check("");Check("text 日本語 😀");Check(new[]{1,2,3});Check(Array.Empty<int>());Check<int[]?>(null);Check(new string?[]{"a",null,"日本語"});
        Check(new WirePayload{Name="payload",Values=[1,2]});Check<WirePayload?>(null);Check(new WirePayload());Check(new WireRefValue("text"));Check((WireRefValue?)new WireRefValue("nullable"));Check((WireRefValue?)null);
        Check(new[]{new WirePayload{Name="one"},new WirePayload{Values=[2,3]}});
        Assert.Throws<NotSupportedException>(()=>Wire.For<object>());Assert.Throws<NotSupportedException>(()=>Wire.For<IntPtr>());Assert.Throws<NotSupportedException>(()=>Wire.For<int[,]>());Assert.Throws<NotSupportedException>(()=>Wire.For<WireCycle>());
        Assert.Throws<NotSupportedException>(()=>Wire.For<WireHook>());Assert.Equal(0,WireHook.Calls);
    }

    [Fact,Trait("Category","Unit")]
    public void InvalidLengthsAndEncodingAreRejected(){
        static byte[] Header(int value){var bytes=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,value);return bytes;}
        foreach(var count in new[]{int.MinValue,-2,int.MaxValue,65537}){Assert.Throws<InvalidDataException>(()=>Wire.Validate<int[]>(Header(count)));}
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<int[]>([2,0,0,0,1,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<string>(Header(int.MaxValue)));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<string>(Header(int.MinValue)));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<string>([0xfe,0xff,0xff,0xff,1,0,0,0,0xff]));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<string>([0xfe,0xff,0xff,0xff,2,0,0,0,0x61]));
        Wire.Validate<string>([1,0,0,0,0x61,0]);
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<WirePayload>([0]));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<int[]>(MemoryPackSerializer.Serialize(new[]{1,2}),new(){MaximumArrayElements=1}));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<string>(MemoryPackSerializer.Serialize("long"),new(){MaximumStringCharacters=3}));
        Assert.Throws<InvalidDataException>(()=>Wire.Validate<WirePayload>(MemoryPackSerializer.Serialize(new WirePayload()),new(){MaximumValueNodes=1}));
        var random=new Random(173);
        for(var index=0;index<2000;index++){
            var bytes=new byte[random.Next(0,40)];random.NextBytes(bytes);
            try{Wire.Validate<WirePayload>(bytes);}catch(InvalidDataException){}
            try{Wire.Validate<int[]>(bytes);}catch(InvalidDataException){}
            try{Wire.Validate<string>(bytes);}catch(InvalidDataException){}
        }
    }

}
