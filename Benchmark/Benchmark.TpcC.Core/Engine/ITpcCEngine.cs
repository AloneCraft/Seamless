using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>1 つの組み合わせ(Tsavorite/Persisted × 標準 LINQ/FLinq)の取引の実行器。</summary>
public interface ITpcCEngine:IDisposable{
    TpcCMode Mode{get;}
    ITpcCDatabase Database{get;}
    /// <summary>初期データの流し込み先。<see cref="ITpcCDatabase.BeginBulkLoad"/> の間に使う。</summary>
    ITpcCLoadSink Loader{get;}
    /// <summary>取引を 1 件実行する。触る区画の取り消し記録を昇順に始め、確定か巻き戻しを全区画そろえて行う。業務上の巻き戻し以外の例外は、書き込みを戻してそのまま投げる。</summary>
    TpcCResult Execute(TpcCRequest request);
    /// <summary>全地区の D_NEXT_O_ID の合計(NOPM の測り方: HammerDB はこの増分を分で割る)。</summary>
    long SumNextOrderIds();
}
/// <summary>1 つの組み合わせの入口。</summary>
public interface ITpcCVariant{
    string Name{get;}
    string ShortName{get;}
    ITpcCEngine Open(Store store,TpcCScale scale,TpcCMode mode);
}
