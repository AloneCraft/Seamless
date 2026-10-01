namespace Benchmark.Audit.Tests;

/// <summary>監査する不変条件の種類。</summary>
public enum AuditInvariant {
    /// <summary>ベンチの `X_Impl` が FLinq の置き換えメソッドへ差し替わっていない。</summary>
    置き換え漏れ,
    /// <summary>置き換え後のコードが、要素を列挙する System.Linq/ZLinq を呼んでいる。</summary>
    外部LINQ,
    /// <summary>FLinq(全最適化)の結果が理想版と異なる(FLinq 側の結果誤り)。</summary>
    結果不一致,
    /// <summary>配列・List・Range 起点で、ループ内のインターフェース呼び出し・デリゲート呼び出し・ボックス化が理想版より多い。</summary>
    理想形との差,
}

/// <summary>許可リストの1項目。DeclaringType は入れ子を + で区切った完全名。</summary>
public sealed record KnownException(AuditInvariant Invariant,string Assembly,string DeclaringType,string Method,string Reason);

/// <summary>
/// 不変条件の既知の違反。直したら必ずここから消す(違反していない項目が残っているとテストが失敗する)。
/// </summary>
public static class KnownExceptions {
    private const string Array = "Benchmark.System.Linq.Enumerable.FLinq";
    private const string List = "Benchmark.System.Linq.Enumerable.List.FLinq";
    private const string IEnumerable = "Benchmark.System.Linq.Enumerable.IEnumerable.FLinq";
    private const string Optimization = "Benchmark.Optimization.All";
    private const string Base = "Shared.Linq.Base`1";
    private const string Range = "Benchmark.System.Linq.Enumerable.Range.FLinq";
    private const string RangeType = "Shared.Linq.Range_start_count";

    public static IReadOnlyList<KnownException> All { get; } = [
        new(AuditInvariant.置き換え漏れ,Array,Base,"ElementAtOrDefault_index","配列の ElementAtOrDefault(int) が傍受されず System.Linq のまま(原因未調査)"),
        new(AuditInvariant.置き換え漏れ,List,Base,"ElementAtOrDefault_index","List の ElementAtOrDefault(int) が傍受されず System.Linq のまま(原因未調査)"),
        new(AuditInvariant.置き換え漏れ,IEnumerable,Base,"ElementAt_index","ベンチ定義が [LinqIntercept(false)] の ElementAt_index_Core を経由しており、FLinq 列が System.Linq を測っている"),
        new(AuditInvariant.外部LINQ,List,Base,"ToArray","List の ToArray を List<T>.ToArray との衝突回避で静的形 Enumerable.ToArray(src) に書いたため、生成器が不透明な入口として System.Linq の ToArray を呼ぶ"),
        new(AuditInvariant.外部LINQ,IEnumerable,Base,"Single_predicate","[LinqIntercept(false)] の srcOne のコレクション式 [FromInt(0)] が展開時に Enumerable.Repeat(…,1) へ書き換わる"),
        new(AuditInvariant.理想形との差,Array,Base,"ToLookup_elementSelector","結果の ILookup をインターフェース型の変数で受けて foreach するため、グループごとにインターフェース呼び出しになる"),
        new(AuditInvariant.理想形との差,Array,Base,"ToLookup_keySelector","結果の ILookup をインターフェース型の変数で受けて foreach するため、グループごとにインターフェース呼び出しになる"),
        new(AuditInvariant.理想形との差,List,Base,"ToLookup_elementSelector","結果の ILookup をインターフェース型の変数で受けて foreach するため、グループごとにインターフェース呼び出しになる"),
        new(AuditInvariant.理想形との差,List,Base,"ToLookup_keySelector","結果の ILookup をインターフェース型の変数で受けて foreach するため、グループごとにインターフェース呼び出しになる"),
        new(AuditInvariant.理想形との差,Range,RangeType,"ToLookup_elementSelector","同上(Range)"),
        new(AuditInvariant.理想形との差,Range,RangeType,"ToLookup_keySelector","同上(Range)"),
        new(AuditInvariant.理想形との差,Optimization,"Shared.Linq.Optimization.ProjectionMerge.Array`1+SelectMany_collection","Select_member","ラムダ内 Enumerable.Repeat(x,1) の反復子をインターフェース越しに列挙する"),
        new(AuditInvariant.理想形との差,Optimization,"Shared.Linq.Optimization.ProjectionMerge.List`1","SelectMany_collection_Select_member","同上(List)"),
        new(AuditInvariant.理想形との差,Optimization,"Shared.Linq.Optimization.ProjectionMerge.Range","SelectMany_collection_Select_member","同上(Range)"),
        new(AuditInvariant.結果不一致,Optimization,"Benchmark.Optimization.PredicatePushdown.ArrayInt32Order","Where_predicate","Order().Where(p) で、ソースが昇順のときの並べ替え省略の高速経路が後続の Where を適用せず全件を流す(シードなしで再現)。高速経路に入れないときはソース式を2回評価する"),
        new(AuditInvariant.結果不一致,Optimization,"Benchmark.Optimization.PredicatePushdown.ArrayClassInt32Order","Where_predicate","同上(ClassInt32)"),
        new(AuditInvariant.結果不一致,Optimization,"Benchmark.Optimization.PredicatePushdown.ListInt32","Order_Where_predicate","同上(List)"),
        new(AuditInvariant.結果不一致,Optimization,"Benchmark.Optimization.PredicatePushdown.ListClassInt32","Order_Where_predicate","同上(List・ClassInt32)"),
        new(AuditInvariant.外部LINQ,Optimization,"Shared.Linq.Optimization.ProjectionMerge.List`1","SelectMany_collection_Select_member","SelectMany のラムダ内 Enumerable.Repeat(x,1) が要素ごとに System.Linq の反復子を作る"),
        new(AuditInvariant.外部LINQ,Optimization,"Shared.Linq.Optimization.ProjectionMerge.Range","SelectMany_collection_Select_member","同上(Range)"),
        new(AuditInvariant.外部LINQ,Optimization,"Shared.Linq.Optimization.ProjectionMerge.Array`1+SelectMany_collection","Select_member","同上(Array)"),
        new(AuditInvariant.外部LINQ,Optimization,"Shared.Linq.Optimization.ProjectionMerge.IEnumerable`1+SelectMany_collection","Select_member","同上(IEnumerable)"),
    ];
}
