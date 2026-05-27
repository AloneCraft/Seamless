namespace Seamless.SourceGenerator.Linq.Fragments;
public class GroupJoin(int 番号, Enumerable outer, Enumerable inner, FuncFragment outerKeySelector, FuncFragment innerKeySelector, FuncFragment resultSelector, string comparer) : Enumerable(番号) {
    protected override string StructTypeParameters => "<TOuterEnum, TInnerEnum>";
    protected override string StructTypeConstraints =>
        $"where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{outer.ElementType}>\n    where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{inner.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{outer.EnumeratorTypeName}, {inner.EnumeratorTypeName}>";
    public override string StructMethod => $$"""
                                             {{FuncSupport(outerKeySelector,nameof(outerKeySelector))}}
                                             {{FuncSupport(innerKeySelector,nameof(innerKeySelector))}}
                                             {{FuncSupport(resultSelector,nameof(resultSelector))}}
                                             """;
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // --- Enumerator struct パス (ストリーミング用) ---
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({outer.GetEnumeratorExpression}, {inner.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(outerKeySelector,innerKeySelector,resultSelector)})";
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(outerKeySelector,innerKeySelector,resultSelector)}}
        private readonly global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> {{lookup}};
        private TOuterEnum {{e}};
        private int {{state}};
        private {{ElementType}} {{currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            TOuterEnum outer,
            TInnerEnum inner,
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer{{ClosureMethodParameterSuffix(outerKeySelector,innerKeySelector,resultSelector)}}) {
            {{ClosureFieldAssignments(outerKeySelector,innerKeySelector,resultSelector)}}
            {{lookup}} = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            using var innerEnumerator = inner;
            while(innerEnumerator.MoveNext()) {
                var {{item}} = innerEnumerator.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                {{FuncAssign(innerKeySelector,nameof(innerKeySelector),key,item)}}
                if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{lookup}}.Add({{key}}, {{group}});
                }
                {{group}}.Add({{item}});
            }
            {{e}} = outer;
            {{state}} = 0;
            {{currentValue}} = default!;
        }
        """;
    // selector はすべて式へインライン展開する。
    public override string GetTryGetNextBody => $$"""
        if({{state}}==1) {
            current = default!;
            return false;
        }
        if({{e}}.MoveNext()) {
            var {{outerValue}} = {{e}}.Current;
            {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
            {{FuncAssign(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
            if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
            }
            {{FuncAssign(resultSelector,nameof(resultSelector),currentValue,outerValue,group)}}
            current = {{currentValue}};
            return true;
        }
        {{state}} = 1;
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
    public override string ElementType => FuncReturnTypeName(resultSelector);

    // --- フラットループパス (末尾メソッド用) ---
    public override bool SupportsFlatLoop => true;
    public override string GetFlatLoopCode(string elementVariable,string bodyCode) => $$"""
        var {{lookup}} = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>({{comparer}} ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
        {
            using var {{e}} = {{inner.GetEnumeratorExpression}};
            while({{e}}.MoveNext()) {
                var {{item}} = {{e}}.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                {{FuncAssignFlat(innerKeySelector,nameof(innerKeySelector),key,item)}}
                if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{lookup}}.Add({{key}}, {{group}});
                }
                {{group}}.Add({{item}});
            }
        }
        {
            using var {{temp}} = {{outer.GetEnumeratorExpression}};
            while({{temp}}.MoveNext()) {
                var {{outerValue}} = {{temp}}.Current;
                {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
                {{FuncAssignFlat(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
                if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                }
                {{ElementType}} {{elementVariable}} = default!;
                {{FuncAssignFlat(resultSelector,nameof(resultSelector),elementVariable,outerValue,group)}}
                {{bodyCode}}
            }
        }
        """;
}
