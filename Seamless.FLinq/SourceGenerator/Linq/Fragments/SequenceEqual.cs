namespace Seamless.FLinq.Linq.Fragments;

public class SequenceEqual(int 番号,Enumerable source,Enumerable second,string resultType)
    :SequenceEqual_comparer(番号,source,second,"null",resultType);

public class SequenceEqual_comparer(int 番号, Enumerable source, Enumerable second, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => StructTypeParametersWithOuter("TEnum1", "TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Seamless.System.Carrier.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>, global::Seamless.System.Carrier.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => EnumeratorTypeNameWithOuter(source.EnumeratorTypeName, second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => "Compute(source, second, comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(comparer=="null"
            && source is RangeRoot leftRange
            && second is RangeRoot rightRange
            && source.ElementType=="int"
            && second.ElementType=="int") {
            return $$"""
                {{destType}} {{destVar}};
                {
                    var __startL{{番号}}={{leftRange.StartExpression}};
                    var __countL{{番号}}={{leftRange.CountExpression}};
                    var __startR{{番号}}={{rightRange.StartExpression}};
                    var __countR{{番号}}={{rightRange.CountExpression}};
                    {{destVar}}=__startL{{番号}}==__startR{{番号}} && __countL{{番号}}==__countR{{番号}};
                }
                """;
        }
        if(source.DirectEnumerableExpression is not { } sourceExpression
            || second.DirectEnumerableExpression is not { } secondExpression) {
            return null;
        }
        var leftVar=$"__srcL{this.result}";
        var rightVar=$"__srcR{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{leftVar}}={{sourceExpression}};
                var {{rightVar}}={{secondExpression}};
                {{destVar}}=global::System.Linq.Enumerable.SequenceEqual({{leftVar}}, {{rightVar}}, {{comparer}});
            }
            """;
    }
    public override string InlineSupportingMembers => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    protected override string ComputeMembers => source.CanProvideSpan && second.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                source.TryGetSpan(out var __spanL);
                second.TryGetSpan(out var __spanR);
                if(__spanL.Length!=__spanR.Length) return false;
                for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                    if(!comparer.Equals(__spanL[__i],({{source.ElementType}})(object)__spanR[__i]!)) return false;
                }
                return true;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(true){
                    var hasLeft=source.MoveNext();
                    var hasRight=second.MoveNext();
                    if(hasLeft!=hasRight) return false;
                    if(!hasLeft) return true;
                    if(!comparer.Equals(source.Current,({{source.ElementType}})(object)second.Current!)) return false;
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
