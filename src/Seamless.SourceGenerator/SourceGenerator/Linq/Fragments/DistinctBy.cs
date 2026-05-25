namespace Seamless.SourceGenerator.Linq.Fragments;

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : DistinctBy_comparer(番号, source, keySelector, "null") { }

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign
        ? $"TEnum source, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer"
        : $"TEnum source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}> keySelector, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression => "Build(source, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                if(seen.Add({{this.key}})) result.Add({{this.item}});
            }
            return result;
        }
        """;
}
