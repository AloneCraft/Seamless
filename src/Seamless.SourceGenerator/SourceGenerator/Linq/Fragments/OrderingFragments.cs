namespace Seamless.SourceGenerator.Linq.Fragments;

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }
public class ThenBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class ThenByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }
public class ThenBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderBy_comparer(番号, source, keySelector, comparer) { }
public class ThenByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderByDescending_comparer(番号, source, keySelector, comparer) { }

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: false) { }
public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: true) { }

public abstract class OrderingBase(int 番号, Enumerable source, FuncFragment keySelector, string comparer, bool descending) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {comparer})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}> keySelector, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression => "Build(source, comparer)";
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
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            var items = new global::System.Collections.Generic.List<SortItem>();
            using var e = source.GetEnumerator();
            var index = 0;
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                items.Add(new SortItem({{this.item}}, {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}}, index++));
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
