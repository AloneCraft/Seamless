namespace Benchmark.TpcE.Core;
// Broker-Volume(TPC-E 3.3.1)の入出力。
/// <summary>Broker-Volume の入力(3.3.1.1)。</summary>
public sealed record BrokerVolumeInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.BrokerVolume;
    /// <summary>集計するブローカーの名前(broker_list。互いに異なる 20〜40 件。ブローカーがそれより少ない規模では全員まで)。</summary>
    public string[] BrokerList{get;init;}=[];
    /// <summary>集計する部門の名前(sector_name = SC_NAME)。</summary>
    public string SectorName{get;init;}="";
}
/// <summary>ブローカー 1 人ぶんの集計(broker_name と volume。volume は発動待ちの指値注文の TR_QTY×TR_BID_PRICE の合計)。</summary>
public readonly record struct BrokerVolumeEntry(string BrokerName,decimal Volume);
/// <summary>Broker-Volume の集計の途中の 1 行(注文 1 件の、ブローカー名と金額)。</summary>
public readonly record struct BrokerTradeAmount(string BrokerName,decimal Amount);
/// <summary>Broker-Volume の出力(3.3.1.1・3.3.1.3)。</summary>
public sealed record BrokerVolumeOutput:TxnResult{
    /// <summary>返した件数(list_len)。</summary>
    public int ListLength{get;init;}
    /// <summary>ブローカーごとの合計。合計の降順(同額はブローカー名の Ordinal 昇順)。</summary>
    public BrokerVolumeEntry[] Entries{get;init;}=[];
}
