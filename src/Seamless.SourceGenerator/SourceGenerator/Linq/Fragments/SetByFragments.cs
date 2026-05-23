namespace Seamless.SourceGenerator.Linq.Fragments;

public class ExceptBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : ExceptBy_comparer(番号, source, second, keySelector, "null") { }
public class IntersectBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : IntersectBy_comparer(番号, source, second, keySelector, "null") { }
public class UnionBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : UnionBy_comparer(番号, source, second, keySelector, "null") { }

public class ExceptBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "except") { }
public class IntersectBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "intersect") { }
public class UnionBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "union") { }

public abstract class SetByBase(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer, string operation) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}> keySelector, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression => "Build(source, second, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var keys = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            {{this.BuildSecondKeyLoad}}
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                {{this.BuildSourceDecision}}
            }
            return result;
        }
        """;
    private string BuildSecondKeyLoad => operation switch {
        "union" => string.Empty,
        _ => $$"""
            using(var secondEnumerator = second.GetEnumerator()) {
                while(secondEnumerator.MoveNext()) keys.Add(({{FuncReturnTypeName(keySelector)}})(object)secondEnumerator.Current!);
            }
            """,
    };
    private string BuildSourceDecision => operation switch {
        "except" => $"if(!keys.Contains({this.key}) && keys.Add({this.key})) result.Add({this.item});",
        "intersect" => $"if(keys.Remove({this.key})) result.Add({this.item});",
        _ => $$"""
            if(keys.Add({{this.key}})) result.Add({{this.item}});
            """,
    };
}
