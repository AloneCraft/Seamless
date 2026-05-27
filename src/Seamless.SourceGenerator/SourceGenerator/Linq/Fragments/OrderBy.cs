namespace Seamless.SourceGenerator.Linq.Fragments;

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: false) { }
public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: true) { }

public abstract class OrderingBase(int 番号, Enumerable source, FuncFragment keySelector, string comparer, bool descending) : BufferedSequence(番号, source.ElementType) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string BuildExpression =>
        $"Build(source, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string BuildMembers => $$"""
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
            var items = new global::System.Collections.Generic.List<SortItem>();
            var index = 0;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                items.Add(new SortItem({{item}}, {{key}}, index++));
            }
            items.Sort((left, right) => {
                var compare = comparer.Compare(left.Key, right.Key);
                if(compare == 0) compare = left.Index.CompareTo(right.Index);
                return {{(descending ? "-compare" : "compare")}};
            });
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>(items.Count);
            foreach(var item in items) result.Add(item.Value);
            return result;
        }
        """;
}
