using UI;
using Target = 複数実装計測;

namespace Benchmark.Headless.Tests;

/// <summary>--multi-run が FLinq の行から手書き理想版の型を決める規則の検査。</summary>
public sealed class 複数実装計測 {
    [Fact,Trait("Category","Unit")]
    public void 理想アセンブリ名() {
        {
            //メモリ上4種と最適化タブには理想版がある
            Assert.Equal("Benchmark.System.Linq.Enumerable.List.Ideal",Target.理想アセンブリ名(ベンチマークソースタブ一覧.SystemリストID));
            Assert.Equal("Benchmark.Optimization.Ideal",Target.理想アセンブリ名(ベンチマークソースタブ一覧.最適化述語押し下げID));
        }
        {
            //保存層のタブには理想版が無い
            Assert.Null(Target.理想アセンブリ名(ベンチマークソースタブ一覧.Seamless永続化ID));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 理想型名() {
        var 配列 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 範囲 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System範囲);
        var 押し下げ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.最適化述語押し下げ);
        {
            //単一演算子は FLinq を Ideal に置き換える
            Assert.Equal("Benchmark.System.Linq.Enumerable.Ideal.IdealClassInt32",Target.理想型名(配列,"Benchmark.System.Linq.Enumerable.FLinq.FLinqClassInt32"));
            Assert.Equal("Benchmark.System.Linq.Enumerable.Range.Ideal.IdealInt32",Target.理想型名(範囲,"Benchmark.System.Linq.Enumerable.Range.FLinq.FLinqInt32"));
        }
        {
            //複合連鎖は型名が同じ
            Assert.Equal("Benchmark.Optimization.PredicatePushdown.ListInt32",Target.理想型名(押し下げ,"Benchmark.Optimization.PredicatePushdown.ListInt32"));
        }
        {
            //保存層の行には理想版が無い
            Assert.Null(Target.理想型名(押し下げ,"Benchmark.Optimization.PredicatePushdown.PersistedInt32"));
        }
    }
}
