namespace Seamless.SourceGenerator.Linq.Fragments;

public class Average(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            long count = 0;
            double sum = 0;
            while(source.MoveNext()) {
                sum += global::System.Convert.ToDouble(source.Current);
                count++;
            }
            if(count == 0) throw global::Seamless.Linq.Enumerable.NoElements();
            return ({{resultType}})(sum / count);
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector, nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => selector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => selector.CanInlineAssign
        ? $"TEnum {this.e}"
        : $"TEnum {this.e}, global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}> selector";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            long count = 0;
            double sum = 0;
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                sum += global::System.Convert.ToDouble({{this.FuncInvoke(selector,nameof(selector),this.item)}});
                count++;
            }
            if(count == 0) throw global::Seamless.Linq.Enumerable.NoElements();
            return ({{resultType}})(sum / count);
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
