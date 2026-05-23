namespace Seamless.SourceGenerator.Linq.Fragments;

public class Count(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            {{resultType}} count = 0;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) count++;
            return count;
        }
        """;
}

public class LongCount(int 番号,Enumerable source,string resultType):Count(番号,source,resultType);

public class Count_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => predicate.CanInlineAssign ? string.Empty : this.DefineStruct(predicate, nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, bool> predicate";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            {{resultType}} count = 0;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                if({{this.FuncInvoke(predicate,nameof(predicate),this.item)}}) count++;
            }
            return count;
        }
        """;
}

public class LongCount_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):Count_predicate(番号,source,predicate,resultType);
