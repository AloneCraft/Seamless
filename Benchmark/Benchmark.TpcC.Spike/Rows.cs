using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Collections;
namespace Benchmark.TpcC.Spike;
/// <summary>どの表にも使うキー(32 バイト、詰め物なし)。</summary>
public readonly record struct K4(long A,long B,long C,long D);
[InlineArray(8)] public struct Pad8{private byte first;}
[InlineArray(16)] public struct Pad16{private byte first;}
[InlineArray(24)] public struct Pad24{private byte first;}
[InlineArray(56)] public struct Pad56{private byte first;}
[InlineArray(64)] public struct Pad64{private byte first;}
[InlineArray(280)] public struct Pad280{private byte first;}
[InlineArray(600)] public struct Pad600{private byte first;}
/// <summary>表の行。大きさは詰め物で TPC-C の行に近づける(キー 32 + 数値 16 + 詰め物)。</summary>
public struct Row<TPad>:IRecord<K4> where TPad:unmanaged{
    public K4 K;
    public long X;
    public long Y;
    public TPad Pad;
    public readonly K4 Key=>this.K;
}
/// <summary>索引の値(24 バイト)。</summary>
public struct V24{
    public long A;
    public long B;
    public long C;
}
/// <summary>順不同の要約(件数と、各項目のハッシュの和)。</summary>
public readonly record struct Digest(long Count,ulong Sum){
    public static Digest operator +(Digest left,Digest right)=>new(left.Count+right.Count,left.Sum+right.Sum);
    public override string ToString()=>$"{this.Count}/{this.Sum:x16}";
}
public static class Hashing{
    public static ulong Of<T>(in T value) where T:unmanaged{
        return System.IO.Hashing.XxHash3.HashToUInt64(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)));
    }
    public static ulong Of<TKey,TValue>(in TKey key,in TValue value) where TKey:unmanaged where TValue:unmanaged{
        var seed=(long)Of(value);
        return System.IO.Hashing.XxHash3.HashToUInt64(MemoryMarshal.AsBytes(new ReadOnlySpan<TKey>(in key)),seed);
    }
    /// <summary>行に決まった模様を詰める(行ごとに中身が変わるようにする)。</summary>
    public static void Fill<TPad>(ref TPad pad,long seed) where TPad:unmanaged{
        var bytes=MemoryMarshal.AsBytes(new Span<TPad>(ref pad));
        var x=(ulong)seed*0x9E3779B97F4A7C15UL+1;
        for(var index=0;index<bytes.Length;index++){
            x^=x<<13;
            x^=x>>7;
            x^=x<<17;
            bytes[index]=(byte)x;
        }
    }
}
