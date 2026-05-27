namespace Seamless.SourceGenerator.Linq.Fragments;

public class SequenceEqual(int 番号,Enumerable source,Enumerable second,string resultType)
    :SequenceEqual_comparer(番号,source,second,"null",resultType);

public class SequenceEqual_comparer(int 番号, Enumerable source, Enumerable second, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => "Compute(source, second, comparer)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            comparer ??= global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            while(true) {
                var hasLeft = source.MoveNext();
                var hasRight = second.MoveNext();
                if(hasLeft != hasRight) return false;
                if(!hasLeft) return true;
                if(!comparer.Equals(source.Current, ({{source.ElementType}})(object)second.Current!)) return false;
            }
        }
        """;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
