namespace Seamless.SourceGenerator.Linq.Fragments;

public class Zip(int 番号, Enumerable source, Enumerable second) : BufferedSequence(番号, $"({source.ElementType} First, {second.ElementType} Second)") {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<({{source.ElementType}} First, {{second.ElementType}} Second)> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second) {
            var result = new global::System.Collections.Generic.List<({{source.ElementType}} First, {{second.ElementType}} Second)>();
            using var left = source.GetEnumerator();
            using var right = second.GetEnumerator();
            while(left.MoveNext() && right.MoveNext()) {
                result.Add((left.Current, right.Current));
            }
            return result;
        }
        """;
}

public class Zip_resultSelector(int 番号, Enumerable source, Enumerable second, FuncFragment resultSelector) : BufferedSequence(番号, FuncReturnTypeName(resultSelector)) {
    public override string StructMethod => resultSelector.CanInlineAssign ? string.Empty : this.DefineStruct(resultSelector, nameof(resultSelector));
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => resultSelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {this.FuncDelegate(resultSelector,nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => resultSelector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Func<{source.ElementType}, {second.ElementType}, {FuncReturnTypeName(resultSelector)}> resultSelector";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second) {
            var result = new global::System.Collections.Generic.List<{{FuncReturnTypeName(resultSelector)}}>();
            using var left = source.GetEnumerator();
            using var right = second.GetEnumerator();
            while(left.MoveNext() && right.MoveNext()) {
                var {{this.item}} = left.Current;
                var {{this.temp}} = right.Current;
                result.Add({{this.FuncInvoke(resultSelector,nameof(resultSelector),this.item,this.temp)}});
            }
            return result;
        }
        """;
}
