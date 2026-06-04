namespace Seamless.SourceGenerator.Linq.Fragments;

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: false) { }
public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: true) { }

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

    protected override Enumerable PrimarySource => UnorderedSource;

    public override string StructMethod =>
        PrimaryOrdering is { } po
            ? JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(po.OwnKeySelector, "primaryKeySelector"))
            : FuncSupport(keySelector, nameof(keySelector));

    protected override string StructSources => UnorderedSource.GetEnumeratorStruct;

    public override string GetEnumeratorExpression =>
        PrimaryOrdering is { } po
            ? $"new {EnumeratorTypeName}({UnorderedSource.GetEnumeratorExpression}, {comparer}, {po.OwnComparer}{ClosureArgsSuffix(keySelector, po.OwnKeySelector)})"
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        PrimaryOrdering is { } po
            ? $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(po.OwnKeySelector)}>? primaryComparer{ClosurePrimaryCtorSuffix(keySelector, po.OwnKeySelector)}"
            : $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";

    protected override string BuildExpression =>
        PrimaryOrdering is { } po
            ? $"Build(source, comparer, primaryComparer{ClosureArgsSuffix(keySelector, po.OwnKeySelector)})"
            : $"Build(source, comparer{ClosureArgsSuffix(keySelector)})";

    protected override string BuildMembers =>
        PrimaryOrdering is { } po ? BuildMembersCombined(po) : BuildMembersSingle();

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
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            global::System.Collections.Generic.List<SortItem> items;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                items=new global::System.Collections.Generic.List<SortItem>(__count);
            }else{
                items=new global::System.Collections.Generic.List<SortItem>();
            }
            var index = 0;
            {{SortLoopSingle}}
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
            foreach(var {{item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                items.Add(new SortItem({{item}},{{key}},index++));
            }
            """
        : $$"""
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                items.Add(new SortItem({{item}}, {{key}}, index++));
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
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>? primaryComparer{{ClosureMethodParameterSuffix(keySelector, po.OwnKeySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            primaryComparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>.Default;
            global::System.Collections.Generic.List<SortItem> items;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                items=new global::System.Collections.Generic.List<SortItem>(__count);
            }else{
                items=new global::System.Collections.Generic.List<SortItem>();
            }
            var index = 0;
            {{SortLoopCombined(po)}}
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
            foreach(var {{item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                {{FuncReturnTypeName(po.OwnKeySelector)}} {{a}}=default!;
                {{FuncAssign(po.OwnKeySelector,"primaryKeySelector",a,item)}}
                items.Add(new SortItem({{item}},{{key}},{{a}},index++));
            }
            """
        : $$"""
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                {{FuncReturnTypeName(po.OwnKeySelector)}} {{a}} = default!;
                {{FuncAssign(po.OwnKeySelector,"primaryKeySelector",a,item)}}
                items.Add(new SortItem({{item}}, {{key}}, {{a}}, index++));
            }
            """;
}
