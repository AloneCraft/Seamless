namespace Seamless.SourceGenerator.Linq.Fragments;

public class Max(int 番号, Enumerable source, string resultType) : MinMaxBase(番号, source, resultType, "greater") { }
public class Max_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : MinMaxSelectorBase(番号, source, selector, resultType, "greater") { }

public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            if(!source.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            {{resultType}} result = ({{resultType}})(object)source.Current!;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                var value = ({{resultType}})(object)source.Current!;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
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
            if(!source.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            var {{this.item}} = source.Current;
            {{resultType}} result = ({{resultType}})(object){{this.FuncInvoke(selector,nameof(selector),this.item)}}!;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                {{this.item}} = source.Current;
                var value = ({{resultType}})(object){{this.FuncInvoke(selector,nameof(selector),this.item)}}!;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
