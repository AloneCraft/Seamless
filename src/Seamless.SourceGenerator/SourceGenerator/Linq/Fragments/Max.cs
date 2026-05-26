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
            {{resultType}} result = source.Current;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                var value = source.Current;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.FuncPrimaryCtorExtra(selector,SelectorFuncType,nameof(selector))}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ComputeExtraArgs(selector,nameof(selector))})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{this.ComputeExtraParams(selector,SelectorFuncType,nameof(selector))}}) {
            if(!source.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            var {{this.item}} = source.Current;
            {{resultType}} result = default!;
            {{this.StaticComputeFuncAssign(selector,nameof(selector),"result",this.item)}}
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                {{this.item}} = source.Current;
                {{resultType}} value = default!;
                {{this.StaticComputeFuncAssign(selector,nameof(selector),"value",this.item)}}
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
