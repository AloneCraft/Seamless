using System.Runtime.InteropServices;
using Registry=Seamless.Execution.CodecRegistry;
namespace Execution.Tests;
public sealed class CodecRegistry{
    [Fact]
    public void StaticSerializerAdapter(){
        var registry=new Seamless.Execution.CodecRegistry();
        registry.Register<int,Seamless.Execution.Codecs.Serializer<int,Seamless.Collections.Serializers.Blittable<int>>>(new("int-static-v1"));
        var codec=registry.Get<int>();
        var bytes=new byte[codec.GetEncodedLength(123)];codec.Encode(123,bytes);
        Assert.Equal(123,codec.Decode(bytes));
    }

    [Fact]public void Get(){
        {
            var registry=new Registry();
            var value=new{Number=12,Text="日本語",Nested=new{Flag=true,Values=new[]{2,3,5}}};
            Roundtrip(registry,value);
            Roundtrip(registry,(string?)null);
            Roundtrip(registry,"\uD800x\uDFFF");
            Roundtrip(registry,1.25m);
            Roundtrip(registry,Guid.NewGuid());
            Roundtrip(registry,(int?)42);
            Roundtrip(registry,(int?)null);
            Roundtrip(registry,new DateTime(638000000000000000,DateTimeKind.Utc));
        }
        {
            var registry=new Registry();
            var codec=registry.Get<float>();
            var positive=Bytes(codec,0f);
            var negative=Bytes(codec,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)));
            Assert.False(positive.SequenceEqual(negative));
            var first=BitConverter.Int32BitsToSingle(0x7FC00001);
            var second=BitConverter.Int32BitsToSingle(0x7FC00002);
            Assert.False(Bytes(codec,first).SequenceEqual(Bytes(codec,second)));
        }
        {
            var registry=new Registry();
            var codec=registry.Get<Padded>();
            var value=new Padded{A=1,B=4};
            var rawA=new byte[]{1,0,0,0,4,0,0,0};
            var rawB=new byte[]{1,255,128,64,4,0,0,0};
            Assert.Equal(Bytes(codec,MemoryMarshal.Read<Padded>(rawA)),Bytes(codec,MemoryMarshal.Read<Padded>(rawB)));
            var encoded=Bytes(codec,value);
            var decoded=codec.Decode(encoded);
            Assert.Equal(value.A,decoded.A);Assert.Equal(value.B,decoded.B);
            encoded[0]^=1;
            Assert.Throws<InvalidDataException>(()=>codec.Decode(encoded));
        }
        {
            Assert.Throws<Seamless.Execution.CodecUnavailableException>(()=>new Registry().Get<object>());
        }
    }
    private static void Roundtrip<T>(Registry registry,T value){
        var codec=registry.Get<T>();
        var bytes=Bytes(codec,value);
        Assert.Equal(bytes,Bytes(codec,codec.Decode(bytes)));
    }
    private static byte[] Bytes<T>(global::Seamless.Execution.Codecs.IAdapter<T> codec,T value){
        var bytes=new byte[codec.GetEncodedLength(value)];codec.Encode(value,bytes);return bytes;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Padded{public byte A;public int B;}
}
