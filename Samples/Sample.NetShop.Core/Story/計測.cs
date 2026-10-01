using System.Diagnostics;

namespace Sample.NetShop.Core;

public readonly record struct 計測結果<T>(T 値,TimeSpan 時間,long 確保バイト数);

/// <summary>処理時間と、この処理がこのスレッドで確保したメモリ量を測る。</summary>
public static class 計測{
    /// <summary>
    /// <paramref name="回数"/> 回実行し、最も速かった回の時間を返す(初回は JIT などを含むため)。
    /// 処理は同期で、途中でスレッドが変わらないこと。
    /// </summary>
    public static 計測結果<T>実行<T>(Func<T>処理,int 回数=1){
        ArgumentOutOfRangeException.ThrowIfLessThan(回数,1);
        var 最短=TimeSpan.MaxValue;
        long 確保=0;
        T 値=default!;
        for(var 回=0;回<回数;回++){
            var 確保前=GC.GetAllocatedBytesForCurrentThread();
            var 時計=Stopwatch.StartNew();
            値=処理();
            時計.Stop();
            確保=GC.GetAllocatedBytesForCurrentThread()-確保前;
            if(時計.Elapsed<最短){
                最短=時計.Elapsed;
            }
        }
        return new(値,最短,確保);
    }
}
