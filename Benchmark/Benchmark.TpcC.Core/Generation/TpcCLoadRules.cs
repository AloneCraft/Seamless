namespace Benchmark.TpcC.Core;
/// <summary>取引の中身の形。<see cref="Spec"/> は TPC-C 5.11 どおり、<see cref="HammerDbMssql"/> は HammerDB 6.1 の SQL Server 版の規則(省略や癖も含む)。</summary>
public enum TpcCMode{
    Spec,
    HammerDbMssql,
}
/// <summary>
/// 初期データの作り方の規則(形ごとに違う点)。
/// <see cref="Spec"/> は 4.3.3.1 どおり。<see cref="HammerDbMssql"/> は HammerDB 6.1 の読み込みの規則で、出典は
/// <c>Work/TpcC/docs/QUIRKS.md</c> の Q-006(C_LAST の C=123)・Q-022(小文字の original)・Q-023(税・割引の刻み)・Q-024(BC 50%)・Q-025(注文の顧客が順列にならない)・Q-027(初期の明細金額が逆)。
/// C_DATA・I_DATA・S_DATA は HammerDB の SQL Server 版では固定長(char(500)・char(50)・char(50))で、行の大きさが一定なので、<see cref="PadDataColumns"/> で空白に詰める。
/// 次の癖は NOPM の仕事量に効かないデータの不備なので再現しない: Q-010(日時の出どころと秒の取り方)、Q-021(郵便番号のゼロ埋めなし)、Q-022 の「印を付ける行を重複ありで選ぶ」(期待値 9.5%)と
/// 「9 文字を 8 文字に置き換えて 1 文字短くなる」、Q-026(bcp の形で未配達の注文に配送業者が入り、日時が空になる。再現すると一貫性の条件 5・7 が初めから外れる)、
/// Q-049(C_STATE が英数 2 文字)。名前・住所などの文字列は固定長に詰めない(索引の鍵と照合が崩れるため。SQL Server の char は末尾の空白を無視して比べる)。
/// </summary>
public sealed record TpcCLoadRules{
    public TpcCMode Mode{get;init;}=TpcCMode.Spec;
    /// <summary>C_CREDIT が "BC" の割合(%)。仕様は 10。</summary>
    public int BadCreditPercent{get;init;}=10;
    /// <summary>W_TAX・D_TAX の範囲(万分率)。仕様は 0.0000〜0.2000。</summary>
    public int TaxMin{get;init;}
    public int TaxMax{get;init;}=2000;
    /// <summary>W_TAX・D_TAX の刻み(万分率)。仕様は 1(0.0001 刻み)、HammerDB は 100(0.01 刻み)。</summary>
    public int TaxStep{get;init;}=1;
    /// <summary>C_DISCOUNT の刻み(万分率、範囲は 0.0000〜0.5000)。仕様は 1、HammerDB は 100。</summary>
    public int DiscountStep{get;init;}=1;
    /// <summary>I_DATA・S_DATA に 10% 混ぜる印。仕様は大文字の "ORIGINAL"。</summary>
    public string OriginalMarker{get;init;}="ORIGINAL";
    /// <summary>C_LAST の NURand(255,0,999,C) の C(読み込み時)。null なら種から [0,255] で選ぶ。</summary>
    public int? LastNameCLoad{get;init;}
    /// <summary>初期の注文の O_C_ID が顧客 ID の順列か。false(HammerDB)は、注文 i の顧客を i..顧客数 から一様に選ぶ(顧客の約半数が初期の注文を持たない)。</summary>
    public bool OrderCustomerIsPermutation{get;init;}=true;
    /// <summary>初期の明細の金額が仕様と逆か。true(HammerDB)は、配達済みの明細が 0.10〜100.00、未配達が 0。仕様は配達済みが 0.00、未配達が 0.01〜9,999.99。</summary>
    public bool InvertedInitialLineAmounts{get;init;}
    /// <summary>C_DATA を 500 文字、I_DATA・S_DATA を 50 文字に空白で詰める(HammerDB の固定長の列)。Payment が書き換える C_DATA も同じ長さを保つ。</summary>
    public bool PadDataColumns{get;init;}
    /// <summary>初期データの日時(1970-01-01 からの秒)。既定は 2026-01-01T00:00:00Z。</summary>
    public long LoadTime{get;init;}=1_767_225_600;
    public static TpcCLoadRules For(TpcCMode mode){
        switch(mode){
            case TpcCMode.Spec:
                return new TpcCLoadRules();
            case TpcCMode.HammerDbMssql:
                return new TpcCLoadRules{
                    Mode=TpcCMode.HammerDbMssql,
                    BadCreditPercent=50,
                    TaxMin=1000,
                    TaxMax=2000,
                    TaxStep=100,
                    DiscountStep=100,
                    OriginalMarker="original",
                    LastNameCLoad=123,
                    OrderCustomerIsPermutation=false,
                    InvertedInitialLineAmounts=true,
                    PadDataColumns=true,
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(mode),mode,null);
        }
    }
}
