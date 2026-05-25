namespace Seamless.SourceGenerator.Linq.Fragments;
public class Last(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            var found = false;
            {{resultType}} result = default!;
            while(source.MoveNext()) {
                found = true;
                result = source.Current;
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Last_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => predicate.CanInlineAssign ? string.Empty : this.DefineStruct(predicate, nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"TEnum {this.e}"
        : $"TEnum {this.e}, global::System.Func<{source.ElementType}, bool> predicate";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            var found = false;
            {{resultType}} result = default!;
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                if({{this.FuncInvoke(predicate,nameof(predicate),this.item)}}) {
                    found = true;
                    result = {{this.item}};
                }
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
