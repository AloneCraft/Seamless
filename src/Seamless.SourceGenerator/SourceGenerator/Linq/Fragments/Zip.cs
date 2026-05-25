namespace Seamless.SourceGenerator.Linq.Fragments;

public class Zip(int 番号, Enumerable source, Enumerable second) : BufferedSequence(番号, $"({source.ElementType} First, {second.ElementType} Second)") {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
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
    public override string StructMethod => resultSelector.CanInlineAssign ? string.Empty : this.DefineStruct(resultSelector, nameof(resultSelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => resultSelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {this.FuncDelegate(resultSelector,nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => resultSelector.CanInlineAssign
        ? $"TEnum1 source, TEnum2 second"
        : $"TEnum1 source, TEnum2 second, global::System.Func<{source.ElementType}, {second.ElementType}, {FuncReturnTypeName(resultSelector)}> resultSelector";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}> Build(TEnum1 source, TEnum2 second) {
            var result = new global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}>();
            while(source.MoveNext() && second.MoveNext()) {
                var {{this.item}} = source.Current;
                var {{this.temp}} = second.Current;
                result.Add({{this.FuncInvoke(resultSelector,nameof(resultSelector),this.item,this.temp)}});
            }
            return result;
        }
        """;
}
