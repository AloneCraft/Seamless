namespace Seamless.SourceGenerator.Linq.Fragments;

public class Zip(int 番号, Enumerable source, Enumerable second) : BufferedSequence(番号, $"({source.ElementType} First, {second.ElementType} Second)") {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<({{source.ElementType}} First, {{second.ElementType}} Second)> Build(TEnum1 source, TEnum2 second) {
            var result = new global::System.Collections.Generic.List<({{source.ElementType}} First, {{second.ElementType}} Second)>();
            while(source.MoveNext() && second.MoveNext()) {
                result.Add((source.Current, second.Current));
            }
            return result;
        }
        """;
}

public class Zip_resultSelector(int 番号, Enumerable source, Enumerable second, FuncFragment resultSelector) : BufferedSequence(番号, FuncReturnTypeName(resultSelector)) {
    private string ResultSelectorFuncType => $"global::System.Func<{source.ElementType}, {second.ElementType}, {FuncReturnTypeName(resultSelector)}>";
    public override string StructMethod => FuncSupport(resultSelector,nameof(resultSelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}{ClosureArgsSuffix(resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second{ClosurePrimaryCtorSuffix(resultSelector)}";
    protected override string BuildExpression => $"Build(source, second{ClosureArgsSuffix(resultSelector)})";
    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}> Build(TEnum1 source, TEnum2 second{{ClosureMethodParameterSuffix(resultSelector)}}) {
            var result = new global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}>();
            while(source.MoveNext() && second.MoveNext()) {
                var {{item}} = source.Current;
                var {{temp}} = second.Current;
                result.Add({{FuncInvokeField(resultSelector,nameof(resultSelector),item,temp)}});
            }
            return result;
        }
        """;
}
