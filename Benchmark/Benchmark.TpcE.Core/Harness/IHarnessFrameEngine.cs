namespace Benchmark.TpcE.Core.Harness;
/// <summary>
/// EGenTxnHarness(C++)が DB インターフェース経由で呼ぶ「フレーム」を実行する。
/// 入力と出力は EGen の構造体のバイト列そのもの(生成した <c>Benchmark.TpcE.Core.Harness</c> のラッパーで読み書きする)。
/// 取引の開始・確定・巻き戻しはエンジンが受け持つ(ハーネスは通知しない): 最初のフレームで開始し、最後のフレームで確定する。
/// </summary>
public interface IHarnessFrameEngine{
    /// <summary>フレームを 1 つ実行する。失敗は例外で知らせる(ハーネスの取引は打ち切られる)。</summary>
    void ExecuteFrame(HarnessFrame frame,Span<byte> input,Span<byte> output,IMarketSink market);
    /// <summary>取引の終わり(成功でも失敗でも)に呼ぶ。確定されないまま開いている取引があれば巻き戻す。</summary>
    void EndTransaction();
    /// <summary>取引の時刻(0001-01-01 からのミリ秒)を返す時計。既定(null)は実時計で、単調に増える。試験で既存の実装と時刻を揃えるときに差し替える。</summary>
    Func<long>? Clock{get;set;}
    /// <summary>
    /// 次に Trade-Order のフレーム 4 が使う取引 ID。0 のときは DB にある最大の取引 ID + 1 から始める。
    /// 巻き戻した注文の ID も使い切る(戻さない)。保存と復旧では、再実行ログを当てた後に読み直す。
    /// </summary>
    long NextTradeId{get;set;}
}
/// <summary>市場(MEE)へ注文を送る口。ハーネスの Trade-Order と、Market-Feed のフレームが使う。引数は EGen の TTradeRequest のバイト列。</summary>
public interface IMarketSink{
    void SendToMarket(ReadOnlySpan<byte> tradeRequest);
}
