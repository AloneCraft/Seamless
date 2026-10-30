using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
/// <summary>保存先と LINQ の種類の組み合わせ。各組み合わせのアセンブリが固有名の入口クラスで実装する。</summary>
public interface ITpcEVariant{
    /// <summary>例: tsavorite-flinq。</summary>
    string Name{get;}
    /// <summary>例: TF。</summary>
    string ShortName{get;}
    /// <summary>ストアに DB を開いてエンジンを作る(保存済みなら登録時にその状態へ戻る)。</summary>
    ITpcEEngine Open(Store store,TpcEScale scale);
}
