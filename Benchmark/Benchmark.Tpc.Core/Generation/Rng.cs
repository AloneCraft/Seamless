namespace Benchmark.Tpc.Core;
/// <summary>
/// 決定的な乱数(xoshiro256**)。種は SplitMix64 で広げる。System.Random は版をまたいだ再現が保証されないので使わない。
/// 運転状態に乱数の位置を保存しなくて済むよう、使う側は (種, 流れの名前, 番号) から毎回作る(<see cref="For"/>)。
/// </summary>
public struct Rng{
    private ulong s0;
    private ulong s1;
    private ulong s2;
    private ulong s3;
    public Rng(ulong seed){
        var state=seed;
        this.s0=SplitMix(ref state);
        this.s1=SplitMix(ref state);
        this.s2=SplitMix(ref state);
        this.s3=SplitMix(ref state);
    }
    /// <summary>(種, 流れ, 番号) から独立した乱数列を作る。</summary>
    public static Rng For(ulong seed,string stream,long index){
        return new(Mix(Mix(seed,StableHash(stream)),(ulong)index));
    }
    /// <summary>(種, 領域, ID, 項目) から 1 つの値を決める(実体の属性を DB を読まずに再計算するため)。</summary>
    public static ulong Keyed(ulong seed,string domain,long id,int field=0){
        var state=Mix(Mix(Mix(seed,StableHash(domain)),(ulong)id),(ulong)field);
        return SplitMix(ref state);
    }
    /// <summary>[min,max] の一様な整数(Keyed 版)。</summary>
    public static long KeyedRange(ulong seed,string domain,long id,int field,long min,long max){
        var rng=new Rng(Keyed(seed,domain,id,field));
        return rng.Range(min,max);
    }
    public ulong NextUInt64(){
        var result=RotateLeft(this.s1*5,7)*9;
        var t=this.s1<<17;
        this.s2^=this.s0;
        this.s3^=this.s1;
        this.s1^=this.s2;
        this.s0^=this.s3;
        this.s2^=t;
        this.s3=RotateLeft(this.s3,45);
        return result;
    }
    /// <summary>[min,max] の一様な整数(Lemire の方法で偏りなく)。</summary>
    public long Range(long min,long max){
        if(max<min){
            throw new ArgumentException($"max ({max}) < min ({min}).");
        }
        var span=(ulong)(max-min)+1;
        if(span==0){
            return (long)this.NextUInt64();
        }
        var x=this.NextUInt64();
        var m=Math.BigMul(x,span,out var low);
        if(low<span){
            var threshold=(0-span)%span;
            while(low<threshold){
                x=this.NextUInt64();
                m=Math.BigMul(x,span,out low);
            }
        }
        return min+(long)m;
    }
    public int Range(int min,int max)=>(int)this.Range((long)min,(long)max);
    /// <summary>percent % の確率で true。</summary>
    public bool Percent(int percent)=>this.Range(0,99)<percent;
    /// <summary>TPC-C/E 流の非一様乱数 NURand(A,x,y,C) = (((Range(0,A) | Range(x,y)) + C) % (y-x+1)) + x。</summary>
    public long NURand(long a,long x,long y,long c){
        return ((this.Range(0,a)|this.Range(x,y))+c)%(y-x+1)+x;
    }
    /// <summary>重みつきで添字を選ぶ(重みは非負の整数)。</summary>
    public int Weighted(ReadOnlySpan<int> weights){
        var total=0;
        foreach(var weight in weights){
            total+=weight;
        }
        var pick=this.Range(0,total-1);
        for(var index=0;index<weights.Length;index++){
            if(pick<weights[index]){
                return index;
            }
            pick-=weights[index];
        }
        return weights.Length-1;
    }
    private static ulong RotateLeft(ulong value,int count)=>(value<<count)|(value>>(64-count));
    private static ulong SplitMix(ref ulong state){
        state+=0x9E3779B97F4A7C15UL;
        var z=state;
        z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;
        z=(z^(z>>27))*0x94D049BB133111EBUL;
        return z^(z>>31);
    }
    private static ulong Mix(ulong left,ulong right){
        var state=left^(right*0x9E3779B97F4A7C15UL);
        return SplitMix(ref state);
    }
    /// <summary>文字列の安定したハッシュ(string.GetHashCode はプロセスごとに変わるので使わない)。</summary>
    public static ulong StableHash(string text){
        var hash=14695981039346656037UL;
        foreach(var character in text){
            hash^=character;
            hash*=1099511628211UL;
        }
        return hash;
    }
}
