namespace Seamless.SourceGenerator.Linq.Fragments;

public class ExceptBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : ExceptBy_comparer(番号, source, second, keySelector, "null") { }

public class ExceptBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "except") { }

public abstract class SetByBase(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer, string operation) : BufferedSequence(番号, source.ElementType) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string BuildExpression =>
        $"Build(source, second, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var keys = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            {{BuildSecondKeyLoad}}
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                {{BuildSourceDecision}}
            }
            {{BuildSecondIteration}}
            return result;
        }
        """;
    private string BuildSecondKeyLoad => operation switch {
        "union" => string.Empty,
        _ => $$"""
            while(second.MoveNext()) keys.Add(({{FuncReturnTypeName(keySelector)}})(object)second.Current!);
            """,
    };
    private string BuildSecondIteration => operation switch {
        "union" => $$"""
            while(second.MoveNext()) {
                var {{item}} = second.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(keys.Add({{key}})) result.Add({{item}});
            }
            """,
        _ => string.Empty,
    };
    private string BuildSourceDecision => operation switch {
        "except" => $"if(!keys.Contains({key}) && keys.Add({key})) result.Add({item});",
        "intersect" => $"if(keys.Remove({key})) result.Add({item});",
        _ => $$"""
            if(keys.Add({{key}})) result.Add({{item}});
            """,
    };
}
