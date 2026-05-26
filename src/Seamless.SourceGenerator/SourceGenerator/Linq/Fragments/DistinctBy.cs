namespace Seamless.SourceGenerator.Linq.Fragments;

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : DistinctBy_comparer(番号, source, keySelector, "null") { }

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : BufferedSequence(番号, source.ElementType) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{this.FuncPrimaryCtorExtra(keySelector,KeySelectorFuncType,nameof(keySelector))}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression =>
        $"Build(source{this.ComputeExtraArgs(keySelector,nameof(keySelector))}, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source{{this.ComputeExtraParams(keySelector,KeySelectorFuncType,nameof(keySelector))}}, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.StaticComputeFuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                if(seen.Add({{this.key}})) result.Add({{this.item}});
            }
            return result;
        }
        """;
}
