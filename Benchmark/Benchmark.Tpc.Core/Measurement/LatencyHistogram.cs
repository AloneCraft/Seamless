using System.Diagnostics;
namespace Benchmark.Tpc.Core;
/// <summary>
/// 所要時間の分布。対数の区切りの固定サイズの入れ物(約 2% の精度)に数えるので、件数が増えてもメモリは増えない。
/// </summary>
public sealed class LatencyHistogram{
    private const double Growth=1.02;
    private const int BucketCount=1600;
    private static readonly double LogGrowth=Math.Log(Growth);
    private readonly long[] buckets=new long[BucketCount];
    public long Count{get;private set;}
    public long TotalTicks{get;private set;}
    public long MaxTicks{get;private set;}
    public long AllocatedBytes{get;private set;}
    public long GcCount{get;private set;}
    public void Add(long ticks,long allocatedBytes,long gcCount){
        this.Count++;
        this.TotalTicks+=ticks;
        this.AllocatedBytes+=allocatedBytes;
        this.GcCount+=gcCount;
        if(ticks>this.MaxTicks){
            this.MaxTicks=ticks;
        }
        this.buckets[BucketOf(ticks)]++;
    }
    public void Merge(LatencyHistogram other){
        this.Count+=other.Count;
        this.TotalTicks+=other.TotalTicks;
        this.AllocatedBytes+=other.AllocatedBytes;
        this.GcCount+=other.GcCount;
        this.MaxTicks=Math.Max(this.MaxTicks,other.MaxTicks);
        for(var index=0;index<BucketCount;index++){
            this.buckets[index]+=other.buckets[index];
        }
    }
    public double MeanMicroseconds{
        get{
            if(this.Count==0){
                return 0;
            }
            return TicksToMicroseconds(this.TotalTicks)/this.Count;
        }
    }
    public double MaxMicroseconds=>TicksToMicroseconds(this.MaxTicks);
    /// <summary>百分位(0〜100)の上端の近似値(マイクロ秒)。</summary>
    public double PercentileMicroseconds(double percentile){
        if(this.Count==0){
            return 0;
        }
        var target=(long)Math.Ceiling(this.Count*percentile/100.0);
        long seen=0;
        for(var index=0;index<BucketCount;index++){
            seen+=this.buckets[index];
            if(seen>=target){
                return Math.Min(TicksToMicroseconds(UpperTicks(index)),this.MaxMicroseconds);
            }
        }
        return this.MaxMicroseconds;
    }
    private static int BucketOf(long ticks){
        if(ticks<=1){
            return 0;
        }
        var index=(int)(Math.Log(ticks)/LogGrowth);
        return Math.Clamp(index,0,BucketCount-1);
    }
    private static long UpperTicks(int index)=>(long)Math.Ceiling(Math.Pow(Growth,index+1));
    public static double TicksToMicroseconds(long ticks)=>ticks*1_000_000.0/Stopwatch.Frequency;
}
