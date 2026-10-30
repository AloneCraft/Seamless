namespace Benchmark.TpcE.Core;
/// <summary>
/// TPC-E 1.14.0 の仕様値。条項番号は仕様書の番号。推測で決めた値は名前の説明に「推測」と書き、TpcEAssumptions に一覧を出す。
/// </summary>
public static class TpcESpec{
    /// <summary>1 tpsE あたりの顧客数(2.6.1.4、6.6.8.3)。</summary>
    public const int ScaleFactor=500;
    /// <summary>1 営業日の秒数(8 時間、2.6.1.5・6.6.1.6)。</summary>
    public const int SecondsPerBusinessDay=8*3600;
    /// <summary>1 ロード単位(2.6.1.2)。</summary>
    public const int LoadUnit=1000;

    // 取引比率(6.3.1)。百分率の 10 倍の整数(合計 1,000)。Market-Feed と Trade-Result は MEE が起こすので CE の比率には入れない。
    public const int MixBrokerVolume=49;
    public const int MixCustomerPosition=130;
    public const int MixMarketWatch=180;
    public const int MixSecurityDetail=140;
    public const int MixTradeLookup=80;
    public const int MixTradeOrder=101;
    public const int MixTradeStatus=190;
    public const int MixTradeUpdate=20;

    /// <summary>Data-Maintenance の間隔(6.3.3)。</summary>
    public const long DataMaintenanceIntervalMilliseconds=60_000;

    // フレームの件数上限(3.2.1.2)。
    public const int MinBrokerListLength=20;
    public const int MaxBrokerListLength=40;
    public const int MaxAccountLength=10;
    public const int MaxHistoryLength=30;
    public const int MaxFeedLength=20;
    public const int MinDayLength=5;
    public const int MaxDayLength=20;
    public const int MaxFinancialLength=20;
    public const int MaxNewsLength=2;
    public const int MaxCompetitorLength=3;
    public const int TradeLookupMaxRows=20;
    public const int TradeLookupMaxTradeHistoryRows=3;
    public const int MaxTradeStatusLength=50;
    public const int TradeUpdateMaxRows=20;
    public const int TradeUpdateMaxTradeHistoryRows=3;

    // 入力値の比率(6.4.1)。百分率。
    public const int CustomerPositionByTaxIdPercent=50;
    public const int CustomerPositionGetHistoryPercent=50;
    public const int MarketWatchByWatchListPercent=60;
    public const int MarketWatchByAccountPercent=35;
    public const int MarketWatchByIndustryPercent=5;
    /// <summary>Security-Detail の access_lob_flag=1 の割合(1%)。千分率。</summary>
    public const int SecurityDetailAccessLobPerMille=10;
    public const int TradeLookupFrame1Percent=30;
    public const int TradeLookupFrame2Percent=30;
    public const int TradeLookupFrame3Percent=30;
    public const int TradeLookupFrame4Percent=10;
    public const int TradeOrderThirdPartyPercent=10;
    public const int TradeOrderByCompanyNamePercent=40;
    public const int TradeOrderMarginPercent=8;
    public const int TradeOrderLifoPercent=35;
    /// <summary>Trade-Order の巻き戻し(101 件に 1 件、6.4.1 の注記)。</summary>
    public const int TradeOrderRollbackOneIn=101;
    public const int TradeUpdateFrame1Percent=33;
    public const int TradeUpdateFrame2Percent=33;
    public const int TradeUpdateFrame3Percent=34;

    // 顧客の層(1.4.2)。
    public const int Tier1Percent=20;
    public const int Tier2Percent=60;
    public const int Tier3Percent=20;
}
