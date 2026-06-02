namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{resultType}} result = source.Current;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                var value = source.Current;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
