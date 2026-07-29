using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Benchmark.Dynamic.Core;

public readonly record struct Seed256(ulong A,ulong B,ulong C,ulong D){
    private static readonly byte[] Unsigned64Domain=Encoding.UTF8.GetBytes("Benchmark.Dynamic.Unsigned64Seed.v1");

    public static Seed256 FromBytes(ReadOnlySpan<byte> bytes){
        if(bytes.Length!=32){
            throw new ArgumentException("A Seed256 requires exactly 32 bytes.",nameof(bytes));
        }
        return new(
            BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]));
    }

    public static Seed256 FromString(string value){
        ArgumentNullException.ThrowIfNull(value);
        return FromBytes(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static Seed256 FromUInt64(ulong value){
        Span<byte> payload=stackalloc byte[Unsigned64Domain.Length+sizeof(ulong)];
        Unsigned64Domain.CopyTo(payload);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[Unsigned64Domain.Length..],value);
        return FromBytes(SHA256.HashData(payload));
    }

    public static Seed256 Parse(string text){
        if(!TryParse(text,out var seed)){
            throw new FormatException("Seed must be an unsigned 64-bit decimal, a 0x-prefixed unsigned 64-bit hexadecimal, or a 64-character Seed256 hexadecimal value.");
        }
        return seed;
    }

    public static bool TryParse(string? text,out Seed256 seed){
        seed=default;
        if(string.IsNullOrWhiteSpace(text)){
            return false;
        }
        var trimmed=text.Trim();
        if(trimmed.StartsWith("0x",StringComparison.OrdinalIgnoreCase)){
            if(trimmed.Length<=2||trimmed.Length>18
                ||!ulong.TryParse(trimmed.AsSpan(2),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var hexadecimalValue)){
                return false;
            }
            seed=FromUInt64(hexadecimalValue);
            return true;
        }
        if(trimmed.Length==64){
            try{
                seed=FromBytes(Convert.FromHexString(trimmed));
                return true;
            }catch(FormatException){
                return false;
            }
        }
        if(ulong.TryParse(trimmed,NumberStyles.None,CultureInfo.InvariantCulture,out var decimalValue)){
            seed=FromUInt64(decimalValue);
            return true;
        }
        return false;
    }

    public byte[] ToByteArray(){
        var bytes=new byte[32];
        this.WriteBytes(bytes);
        return bytes;
    }

    public void WriteBytes(Span<byte> destination){
        if(destination.Length<32){
            throw new ArgumentException("The destination must contain at least 32 bytes.",nameof(destination));
        }
        BinaryPrimitives.WriteUInt64LittleEndian(destination,this.A);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..],this.B);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..],this.C);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[24..],this.D);
    }

    public override string ToString(){
        Span<byte> bytes=stackalloc byte[32];
        this.WriteBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }

    public string ToCanonicalString()=>this.ToString();
}

public static class SeedDerivation{
    private static readonly byte[] DomainPrefix=Encoding.UTF8.GetBytes("Benchmark.Dynamic.Seed.v1");

    public static Seed256 Derive(Seed256 parent,string domain,params long[] components){
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(components);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(DomainPrefix);
        Span<byte> seedBytes=stackalloc byte[32];
        parent.WriteBytes(seedBytes);
        hash.AppendData(seedBytes);
        var domainBytes=Encoding.UTF8.GetBytes(domain);
        Span<byte> lengthBytes=stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,domainBytes.Length);
        hash.AppendData(lengthBytes);
        hash.AppendData(domainBytes);
        Span<byte> componentBytes=stackalloc byte[sizeof(long)];
        foreach(var component in components){
            BinaryPrimitives.WriteInt64LittleEndian(componentBytes,component);
            hash.AppendData(componentBytes);
        }
        return Seed256.FromBytes(hash.GetHashAndReset());
    }
}

public sealed class DeterministicPrng{
    private readonly Seed256 seed;
    private ulong state0;
    private ulong state1;
    private ulong state2;
    private ulong state3;

    public DeterministicPrng(Seed256 seed){
        this.seed=seed;
        this.state0=seed.A;
        this.state1=seed.B;
        this.state2=seed.C;
        this.state3=seed.D;
        if((this.state0|this.state1|this.state2|this.state3)==0){
            this.state0=0x9E3779B97F4A7C15UL;
        }
    }

    public Seed256 Seed=>this.seed;

    public DeterministicPrng Fork(string domain,params long[] components){
        return new(SeedDerivation.Derive(this.seed,domain,components));
    }

    public ulong NextUInt64(){
        var result=RotateLeft(this.state1*5,7)*9;
        var temporary=this.state1<<17;
        this.state2^=this.state0;
        this.state3^=this.state1;
        this.state1^=this.state2;
        this.state0^=this.state3;
        this.state2^=temporary;
        this.state3=RotateLeft(this.state3,45);
        return result;
    }

    public int NextInt32(int exclusiveMaximum){
        if(exclusiveMaximum<1){
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }
        var bound=(uint)exclusiveMaximum;
        var threshold=unchecked(0U-bound)%bound;
        uint value;
        do{
            value=(uint)(this.NextUInt64()>>32);
        }while(value<threshold);
        return (int)(value%bound);
    }

    public int NextInt32(int inclusiveMinimum,int exclusiveMaximum){
        if(exclusiveMaximum<=inclusiveMinimum){
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }
        return inclusiveMinimum+this.NextInt32(exclusiveMaximum-inclusiveMinimum);
    }

    public bool NextBoolean()=>this.NextInt32(2)==1;

    public T Choose<T>(IReadOnlyList<T> values){
        ArgumentNullException.ThrowIfNull(values);
        if(values.Count==0){
            throw new ArgumentException("At least one value is required.",nameof(values));
        }
        return values[this.NextInt32(values.Count)];
    }

    public void Shuffle<T>(Span<T> values){
        for(var index=values.Length-1;index>0;index--){
            var other=this.NextInt32(index+1);
            (values[index],values[other])=(values[other],values[index]);
        }
    }

    private static ulong RotateLeft(ulong value,int offset){
        return (value<<offset)|(value>>(64-offset));
    }
}
