namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => direction=="greater"
        ? $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Max(new global::Seamless.Linq.Carrier.ValueEnumerable<TEnum, {source.ElementType}>({this.e}))"
        : $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Min(new global::Seamless.Linq.Carrier.ValueEnumerable<TEnum, {source.ElementType}>({this.e}))";
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
