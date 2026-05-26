namespace Seamless.SourceGenerator.Linq.Fragments;

public class ExceptBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : ExceptBy_comparer(番号, source, second, keySelector, "null") { }

public class ExceptBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "except") { }

public abstract class SetByBase(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer, string operation) : BufferedSequence(番号, source.ElementType) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second{this.FuncPrimaryCtorExtra(keySelector,KeySelectorFuncType,nameof(keySelector))}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression =>
        $"Build(source, second{this.ComputeExtraArgs(keySelector,nameof(keySelector))}, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum1 source, TEnum2 second{{this.ComputeExtraParams(keySelector,KeySelectorFuncType,nameof(keySelector))}}, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var keys = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            {{this.BuildSecondKeyLoad}}
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.StaticComputeFuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                {{this.BuildSourceDecision}}
            }
            return result;
        }
        """;
    private string BuildSecondKeyLoad => operation switch {
        "union" => string.Empty,
        _ => $$"""
            while(second.MoveNext()) keys.Add(({{FuncReturnTypeName(keySelector)}})(object)second.Current!);
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
