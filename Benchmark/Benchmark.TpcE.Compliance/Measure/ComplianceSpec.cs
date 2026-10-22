namespace Benchmark.TpcE.Compliance;
/// <summary>TPC-E 仕様 1.14.0 の数値(条項番号つき)。仕様書の文章は写さず、値だけを持つ。</summary>
internal static class ComplianceSpec{
    /// <summary>6.3.2〜6.3.4 取引比率の許容範囲(%)。比率は 10 種類(Data-Maintenance と Trade-Cleanup を除く)の合計に対する割合。</summary>
    public static readonly IReadOnlyDictionary<string,(double Target,double Minimum,double Maximum)> MixRanges=new Dictionary<string,(double,double,double)>(StringComparer.Ordinal){
        ["BrokerVolume"]=(4.9,4.875,4.925),
        ["CustomerPosition"]=(13.0,12.935,13.065),
        ["MarketFeed"]=(1.0,0.995,1.005),
        ["MarketWatch"]=(18.0,17.910,18.090),
        ["SecurityDetail"]=(14.0,13.930,14.070),
        ["TradeLookup"]=(8.0,7.960,8.040),
        ["TradeOrder"]=(10.1,10.049,10.151),
        ["TradeResult"]=(10.0,9.950,10.050),
        ["TradeStatus"]=(19.0,18.905,19.095),
        ["TradeUpdate"]=(2.0,1.990,2.010),
    };
    /// <summary>6.5.1.2 90 パーセンタイルの応答時間の上限(秒)。</summary>
    public static readonly IReadOnlyDictionary<string,double> ResponseLimitSeconds=new Dictionary<string,double>(StringComparer.Ordinal){
        ["BrokerVolume"]=3,
        ["CustomerPosition"]=3,
        ["MarketFeed"]=2,
        ["MarketWatch"]=3,
        ["SecurityDetail"]=3,
        ["TradeLookup"]=3,
        ["TradeOrder"]=2,
        ["TradeResult"]=2,
        ["TradeStatus"]=1,
        ["TradeUpdate"]=3,
    };
    /// <summary>6.5.1.5 Data-Maintenance は 1 件ごとに 55 秒以内。</summary>
    public const double DataMaintenanceLimitSeconds=55;
    /// <summary>6.6.5.1 計測区間は 2 時間以上。</summary>
    public const double MinimumMeasurementSeconds=2*3600;
    /// <summary>6.6.5.3 データは確定した状態より 15 分以上古くならない(チェックポイントはその半分の 7.5 分ごと)。</summary>
    public const double MaximumCheckpointIntervalMinutes=7.5;
    /// <summary>6.5.2.2 取引の間隔(Dispatch Time)は 1 秒以下。</summary>
    public const double MaximumDispatchSeconds=1;
    /// <summary>6.7.1.2 報告できる性能は公称の 80% 以上、102% 以下。</summary>
    public const double MinimumThroughputRatio=0.80;
    public const double MaximumThroughputRatio=1.02;
    /// <summary>6.6.8 公称性能: 顧客 500 人あたり 1.00 tpsE(= 1,000 人あたり 2.00)。</summary>
    public const int CustomersPerTpse=500;
    /// <summary>CE が出す取引は全体の約 89%(Trade-Result 10% と Market-Feed 1% は MEE が出す)。</summary>
    public const double CeTransactionsPerTpse=8.9;
    public static double NominalTpse(long customers)=>(double)customers/CustomersPerTpse;
}
