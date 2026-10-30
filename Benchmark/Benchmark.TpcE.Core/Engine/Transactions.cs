namespace Benchmark.TpcE.Core;
/// <summary>12 種類の取引(TPC-E 3.3)。</summary>
public enum TxnKind{
    TradeCleanup,
    DataMaintenance,
    MarketFeed,
    TradeResult,
    BrokerVolume,
    CustomerPosition,
    MarketWatch,
    SecurityDetail,
    TradeLookup,
    TradeOrder,
    TradeStatus,
    TradeUpdate,
}
/// <summary>
/// 取引の入力。運転側(CE/MEE/DM)が 1 回だけ作り、4 通りのエンジンへ同じものを渡す。
/// 取引の中で「現在時刻」を使う所は <see cref="Now"/> を使う(実時刻は使わない)。
/// </summary>
public abstract record TxnRequest{
    public abstract TxnKind Kind{get;}
    /// <summary>この取引の模擬時刻(0001-01-01 からのミリ秒)。</summary>
    public long Now{get;init;}
}
/// <summary>
/// 取引の出力。4 通りで正規化したバイト列を比べる(<see cref="CanonicalWriter"/>)。
/// 仕様の status(0=成功、負=誤り、正=警告、3.2.1.8)も出力の一部として比べる。
/// </summary>
public abstract record TxnResult{
    public int Status{get;init;}
}
/// <summary>1 つの組み合わせの取引の実行器(共有プロジェクト Shared/TpcE.Transactions が実装する)。</summary>
public interface ITpcEEngine:IDisposable{
    TpcEDatabaseBase Database{get;}
    /// <summary>初期投入の受け口。</summary>
    ILoadSink Loader{get;}
    /// <summary>取引を 1 件流す。取引の途中で例外になったら書き込みを巻き戻してから投げ直す。</summary>
    TxnResult Execute(TxnRequest request);
}
/// <summary>CE/DM が入力を作るときに使う、DB を読まずに分かる情報。</summary>
public sealed class CeContext(TpcEModel model){
    public TpcEModel Model{get;}=model;
    public TpcEScale Scale=>this.Model.Scale;
    /// <summary>今の模擬時刻。</summary>
    public long Now{get;set;}
    /// <summary>初期取引の ID の範囲 [1, LastInitialTradeId]。</summary>
    public long LastInitialTradeId{get;set;}
    /// <summary>初期取引の時刻の範囲。</summary>
    public long InitialTradeStart{get;set;}
    public long InitialTradeEnd{get;set;}
    /// <summary>次に払い出す取引 ID(Trade-Order が使う。運転側が払い出し後に進める)。</summary>
    public long NextTradeId{get;set;}
    /// <summary>DAILY_MARKET の営業日(古い順)。</summary>
    public long[] MarketDays{get;set;}=[];
}
