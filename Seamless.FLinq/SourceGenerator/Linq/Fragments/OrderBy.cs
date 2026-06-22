namespace Seamless.FLinq.Linq.Fragments;

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: false) { }
public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: true) { }

/*
OrderingBase は OrderBy/ThenBy 系を、全件 buffer + stable sort + buffer enumerator に変換する。

入力例:
  source.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)

生成例:
  var items = new List<SortItem>();
  foreach(var item in source) {
      items.Add(new SortItem(item, keySelector(item), originalIndex++));
  }
  items.Sort((left, right) => CompareKeysThenOriginalIndex(left, right));
  return items.Select(x => x.Value).ToList();

バリエーション:
  - descending=true なら比較結果の符号を反転する。
  - source が既に OrderingBase の場合、一次 key と二次 key を SortItem に同居させる。
  - originalIndex を最後に比較し、System.Linq と同じ安定ソートの結果にする。
*/
public abstract class OrderingBase(int 番号, Enumerable source, FuncFragment keySelector, string comparer, bool descending) : BufferedSequence(番号, source.ElementType) {
    // チェーン（ThenBy）検出用に一次ソースを公開
    internal FuncFragment OwnKeySelector => keySelector;
    internal string OwnComparer => comparer;
    internal bool OwnDescending => descending;
    internal Enumerable OwnSource => source;

    // source が OrderingBase の場合、その一次ソートを取得
    private OrderingBase? PrimaryOrdering => source as OrderingBase;
    // ソート前の元データソース（OrderingBase チェーンを遡る）
    private Enumerable UnorderedSource {
        get {
            Enumerable s = source;
            while(s is OrderingBase oo) s = oo.OwnSource;
            return s;
        }
    }

    protected override Enumerable PrimarySource => this.UnorderedSource;

    public override string StructMethod =>
        this.PrimaryOrdering is { } po
            ? JoinStaticMethods(this.FuncSupport(keySelector, nameof(keySelector)),this.FuncSupport(po.OwnKeySelector, "primaryKeySelector"))
            : this.FuncSupport(keySelector, nameof(keySelector));

    protected override string StructSources => this.UnorderedSource.GetEnumeratorStruct;

    public override string GetEnumeratorExpression =>
        this.PrimaryOrdering is { } po
            ? $"new {this.EnumeratorTypeName}({this.UnorderedSource.GetEnumeratorExpression}, {comparer}, {po.OwnComparer}{this.ClosureArgsSuffix(keySelector, po.OwnKeySelector)})"
            : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{this.ClosureArgsSuffix(keySelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        this.PrimaryOrdering is { } po
            ? $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(po.OwnKeySelector)}>? primaryComparer{this.ClosurePrimaryCtorSuffix(keySelector, po.OwnKeySelector)}"
            : $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";

    protected override string BuildExpression =>
        this.PrimaryOrdering is { } po
            ? $"Build(source, comparer, primaryComparer{this.ClosureArgsSuffix(keySelector, po.OwnKeySelector)})"
            : $"Build(source, comparer{this.ClosureArgsSuffix(keySelector)})";

    protected override string BuildMembers =>
        this.PrimaryOrdering is { } po ? this.BuildMembersCombined(po) : this.BuildMembersSingle();

    private string BuildMembersSingle() => $$"""
        private readonly struct SortItem {
            public SortItem({{source.ElementType}} value, {{FuncReturnTypeName(keySelector)}} key, int index) {
                this.Value = value;
                this.Key = key;
                this.Index = index;
            }
            public {{source.ElementType}} Value { get; }
            public {{FuncReturnTypeName(keySelector)}} Key { get; }
            public int Index { get; }
        }
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            global::System.Collections.Generic.List<SortItem> items;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                items=new global::System.Collections.Generic.List<SortItem>(__count);
            }else{
                items=new global::System.Collections.Generic.List<SortItem>();
            }
            var index = 0;
            {{this.SortLoopSingle}}
            items.Sort((left, right) => {
                var compare = comparer.Compare(left.Key, right.Key);
                if(compare != 0) return {{(descending ? "-compare" : "compare")}};
                return left.Index.CompareTo(right.Index);
            });
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>(items.Count);
            foreach(var item in items) result.Add(item.Value);
            return result;
        }
        """;

    private string SortLoopSingle => source.CanProvideSpan
        ? $$"""
            source.TryGetSpan(out var __span);
            foreach(var {{this.item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                items.Add(new SortItem({{this.item}},{{this.key}},index++));
            }
            """
        : $$"""
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                items.Add(new SortItem({{this.item}}, {{this.key}}, index++));
            }
            """;

    private string BuildMembersCombined(OrderingBase po) => $$"""
        private readonly struct SortItem {
            public SortItem({{source.ElementType}} value, {{FuncReturnTypeName(keySelector)}} key, {{FuncReturnTypeName(po.OwnKeySelector)}} primaryKey, int index) {
                this.Value = value;
                this.Key = key;
                this.PrimaryKey = primaryKey;
                this.Index = index;
            }
            public {{source.ElementType}} Value { get; }
            public {{FuncReturnTypeName(keySelector)}} Key { get; }
            public {{FuncReturnTypeName(po.OwnKeySelector)}} PrimaryKey { get; }
            public int Index { get; }
        }
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>? primaryComparer{{this.ClosureMethodParameterSuffix(keySelector, po.OwnKeySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            primaryComparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>.Default;
            global::System.Collections.Generic.List<SortItem> items;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                items=new global::System.Collections.Generic.List<SortItem>(__count);
            }else{
                items=new global::System.Collections.Generic.List<SortItem>();
            }
            var index = 0;
            {{this.SortLoopCombined(po)}}
            items.Sort((left, right) => {
                var primaryCompare = primaryComparer.Compare(left.PrimaryKey, right.PrimaryKey);
                if({{(po.OwnDescending ? "-primaryCompare" : "primaryCompare")}} != 0) return {{(po.OwnDescending ? "-primaryCompare" : "primaryCompare")}};
                var compare = comparer.Compare(left.Key, right.Key);
                if(compare != 0) return {{(descending ? "-compare" : "compare")}};
                return left.Index.CompareTo(right.Index);
            });
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>(items.Count);
            foreach(var item in items) result.Add(item.Value);
            return result;
        }
        """;

    private string SortLoopCombined(OrderingBase po) => source.CanProvideSpan
        ? $$"""
            source.TryGetSpan(out var __span);
            foreach(var {{this.item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(po.OwnKeySelector)}} {{this.a}}=default!;
                {{this.FuncAssignIndented("    ",po.OwnKeySelector,"primaryKeySelector",this.a,this.item)}}
                items.Add(new SortItem({{this.item}},{{this.key}},{{this.a}},index++));
            }
            """
        : $$"""
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(po.OwnKeySelector)}} {{this.a}} = default!;
                {{this.FuncAssignIndented("    ",po.OwnKeySelector,"primaryKeySelector",this.a,this.item)}}
                items.Add(new SortItem({{this.item}}, {{this.key}}, {{this.a}}, index++));
            }
            """;
}
