using Seamless.Collections.Tsavorite;
namespace Benchmark.TpcE.Core;
/// <summary>
/// 規模。件数の式は TPC-E 2.6.1(1,000 顧客=1 ロード単位あたり 会社 500、銘柄 685、口座 5,000、ブローカー 10、1 日あたり取引 57,600)。
/// 1,000 未満の顧客数も許す(仕様外の縮小。比率はそのまま当てて端数を丸める)。
/// </summary>
public sealed record TpcEScale{
    public int Customers{get;init;}=1000;
    /// <summary>初期取引の営業日数(仕様は 300)。</summary>
    public int InitialTradeDays{get;init;}=1;
    /// <summary>DAILY_MARKET の営業日数(仕様は 1,305 = 2000-01-03〜2004-12-31 の平日)。</summary>
    public int MarketDays{get;init;}=260;
    /// <summary>NEWS_ITEM.NI_ITEM の大きさ(仕様の上限は 100,000 バイト)。</summary>
    public int NewsItemBytes{get;init;}=1024;
    /// <summary>実行中に流す取引の見込み(索引と予算の見積もりに使う)。</summary>
    public long ExpectedRunTrades{get;init;}=10_000;
    public long Accounts=>(long)this.Customers*5;
    public long Companies=>Math.Max(1,(long)this.Customers*500/1000);
    public long Securities=>Math.Max(1,(long)this.Customers*685/1000);
    public long Brokers=>Math.Max(1,(long)this.Customers/100);
    /// <summary>初期取引の件数(1 営業日 = 8 時間 × 3,600 秒 × 顧客数 / 500)。</summary>
    public long InitialTrades=>(long)this.InitialTradeDays*TpcESpec.SecondsPerBusinessDay*this.Customers/TpcESpec.ScaleFactor;
    public long TotalTrades=>this.InitialTrades+this.ExpectedRunTrades;
}
/// <summary>
/// 表ごとの Tsavorite の詳細設定。メモリ上のログを表の大きさに合わせて小さくし、あふれた分はディスクへ出す
/// (表が 33+索引 36 個あるので、既定の「最大 64MB/表」のままではストアのメモリ上限を超える)。
/// </summary>
public static class TpcESizing{
    private const long MinimumLog=1L<<18;
    private const long MaximumLog=1L<<24;
    public static TsavoriteTuning TableTuning(string name,long expectedRows,TpcEScale scale){
        var rowBytes=RowBytes(name,scale);
        var pageSize=TsavoriteTuning.Default.PageSize;
        if(name=="news_item"){
            pageSize=PowerOfTwoAtLeast(Math.Max(pageSize,2L*rowBytes));
        }
        var log=Math.Clamp(PowerOfTwoAtLeast(Math.Max(1,expectedRows)*rowBytes),Math.Max(MinimumLog,2*pageSize),Math.Max(MaximumLog,2*pageSize));
        return TsavoriteTuning.Default with{PageSize=pageSize,LogMemorySize=log};
    }
    public static TsavoriteTuning IndexTuning(string name,TpcEScale scale){
        // 索引のチャンクは 1 個 600 バイト程度。取引の索引(X6〜X8, X10, X11)だけ大きくする。
        long log=MinimumLog;
        if(name.StartsWith("x06",StringComparison.Ordinal)||name.StartsWith("x07",StringComparison.Ordinal)||name.StartsWith("x08",StringComparison.Ordinal)||name.StartsWith("x10",StringComparison.Ordinal)||name.StartsWith("x11",StringComparison.Ordinal)){
            log=Math.Clamp(PowerOfTwoAtLeast(scale.TotalTrades/8*640),MinimumLog,MaximumLog);
        }
        return TsavoriteTuning.Default with{LogMemorySize=log};
    }
    /// <summary>ストア 1 つのメモリ上限の既定(表と索引のログの合計に、索引の分と余裕を足す)。</summary>
    public static long DefaultMemoryBytes(TpcEScale scale){
        return 512L*1024*1024;
    }
    /// <summary>Persisted の表の初期容量(レコード数、データの大きさ)。更新と削除も追記なので、見込みの 2 倍で確保する。</summary>
    public static (long Capacity,long DataCapacity) PersistedCapacity(string name,long expectedRows,TpcEScale scale){
        var rows=Math.Max(16,expectedRows*2);
        return (rows,Math.Max(4096,rows*(RowBytes(name,scale)+32)));
    }
    /// <summary>Persisted の索引の辞書の初期容量。取引の索引(X6〜X8・X10・X11)は取引数に比例し、他は顧客・会社・銘柄に比例する。</summary>
    public static (long Capacity,long DataCapacity) PersistedIndexCapacity(string name,TpcEScale scale){
        var isChunks=name.EndsWith(".c",StringComparison.Ordinal);
        long owners=Math.Max(64,scale.Customers+scale.Accounts+scale.Securities);
        if(name.StartsWith("x06",StringComparison.Ordinal)||name.StartsWith("x10",StringComparison.Ordinal)||name.StartsWith("x11",StringComparison.Ordinal)){
            owners=Math.Max(owners,scale.TotalTrades/2);
        }
        var rows=owners*2;
        long entryBytes=40;
        if(isChunks){
            entryBytes=700;
        }
        return (rows,Math.Max(4096,rows*entryBytes));
    }
    private static long RowBytes(string name,TpcEScale scale){
        return name switch{
            "news_item"=>scale.NewsItemBytes+512,
            "customer"=>400,
            "company"=>400,
            "security"=>240,
            "trade"=>200,
            "address"=>200,
            "zip_code"=>120,
            _=>96,
        };
    }
    private static long PowerOfTwoAtLeast(long value){
        var result=1L;
        while(result<value){
            result<<=1;
        }
        return result;
    }
}
