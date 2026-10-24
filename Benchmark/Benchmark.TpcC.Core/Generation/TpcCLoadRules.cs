namespace Benchmark.TpcC.Core;
/// <summary>取引の中身の形。<see cref="Spec"/> は TPC-C 5.11 どおり、<see cref="HammerDbMssql"/> は HammerDB 6.1 の SQL Server 版の規則(省略や癖も含む)。</summary>
public enum TpcCMode{
    Spec,
    HammerDbMssql,
}
/// <summary>
/// 初期データの作り方の規則(形ごとに違う点)。
/// <see cref="Spec"/> は 4.3.3.1 どおり。<see cref="HammerDbMssql"/> は HammerDB 6.1 の規則で、
/// <b>C4(HammerDB の規則の書き起こし)で確定するまでは仮の値</b>(Work/TpcC/docs/QUIRKS.md の確定後に直す)。
/// </summary>
public sealed record TpcCLoadRules{
    public TpcCMode Mode{get;init;}=TpcCMode.Spec;
    /// <summary>C_CREDIT が "BC" の割合(%)。仕様は 10。</summary>
    public int BadCreditPercent{get;init;}=10;
    /// <summary>W_TAX・D_TAX の範囲(万分率)。仕様は 0.0000〜0.2000。</summary>
    public int TaxMin{get;init;}
    public int TaxMax{get;init;}=2000;
    /// <summary>I_DATA・S_DATA に 10% 混ぜる印。仕様は大文字の "ORIGINAL"。</summary>
    public string OriginalMarker{get;init;}="ORIGINAL";
    /// <summary>C_LAST の NURand(255,0,999,C) の C(読み込み時)。null なら種から [0,255] で選ぶ。</summary>
    public int? LastNameCLoad{get;init;}
    /// <summary>初期データの日時(1970-01-01 からの秒)。既定は 2026-01-01T00:00:00Z。</summary>
    public long LoadTime{get;init;}=1_767_225_600;
    public static TpcCLoadRules For(TpcCMode mode){
        switch(mode){
            case TpcCMode.Spec:
                return new TpcCLoadRules();
            case TpcCMode.HammerDbMssql:
                // 仮: HammerDB の読み込みは BC 50%、税 0.10〜0.20、小文字の 'original'、C_LAST の C は 123 固定(事前調査の値。C4 で確定する)。
                return new TpcCLoadRules{Mode=TpcCMode.HammerDbMssql,BadCreditPercent=50,TaxMin=1000,TaxMax=2000,OriginalMarker="original",LastNameCLoad=123};
            default:
                throw new ArgumentOutOfRangeException(nameof(mode),mode,null);
        }
    }
}
