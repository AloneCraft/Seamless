namespace Seamless.SourceGenerator.Linq.Fragments;
public class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号) {
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
        private readonly global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}> {{lookup}};
        private TOuterEnum {{e}};
        private global::System.Collections.Generic.List<{{inner.ElementType}}>? {{group}};
        private int {{groupIndex}};
        private int {{state}};
        private {{outer.ElementType}} {{outerValue}};
        private {{ElementType}} {{currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            TOuterEnum outer,
            TInnerEnum inner,
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer{{ClosureMethodParameterSuffix(outerKeySelector,innerKeySelector,resultSelector)}}) {
            {{ClosureFieldAssignments(outerKeySelector,innerKeySelector,resultSelector)}}
            {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            using var innerEnumerator = inner;
            while(innerEnumerator.MoveNext()) {
                var {{item}} = innerEnumerator.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                {{FuncAssign(innerKeySelector,nameof(innerKeySelector),key,item)}}
                {{lookup}}.Add({{key}}, {{item}});
            }
            {{e}} = outer;
            {{group}} = null;
            {{groupIndex}} = 0;
            {{state}} = 0;
            {{outerValue}} = default!;
            {{currentValue}} = default!;
        }
        """;
    // selector はすべて式へインライン展開する。
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if({{state}}==1 && {{group}} is not null) {
            if({{groupIndex}}<{{group}}.Count) {
                var {{item}} = {{group}}[{{groupIndex}}++];
                {{FuncAssign(resultSelector,nameof(resultSelector),currentValue,outerValue,item)}}
                current = {{currentValue}};
                return true;
            }
            {{group}} = null;
            {{state}} = 0;
        }
        if(!{{e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{outerValue}} = {{e}}.Current;
        {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
        {{FuncAssign(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
        {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>({{lookup}}[{{key}}]);
        {{groupIndex}} = 0;
        {{state}} = 1;
        goto {{Start}};
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
    public override string ElementType => FuncReturnTypeName(resultSelector);

    // --- フラットループパス (末尾メソッド用) ---
    // 末尾メソッドのスコープに展開されるため @this が使用可能 → 全ラムダをインライン化できる。
    public override bool SupportsFlatLoop => true;
    public override string GetFlatLoopCode(string elementVariable,string bodyCode) => $$"""
        var {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}>({{comparer}} ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
        {
            using var {{e}} = {{inner.GetEnumeratorExpression}};
            while({{e}}.MoveNext()) {
                var {{item}} = {{e}}.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                {{FuncAssignFlat(innerKeySelector,nameof(innerKeySelector),key,item)}}
                {{lookup}}.Add({{key}}, {{item}});
            }
        }
        {
            using var {{temp}} = {{outer.GetEnumeratorExpression}};
            while({{temp}}.MoveNext()) {
                var {{outerValue}} = {{temp}}.Current;
                {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
                {{FuncAssignFlat(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
                var {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>({{lookup}}[{{key}}]);
                for(var {{groupIndex}} = 0; {{groupIndex}} < {{group}}.Count; {{groupIndex}}++) {
                    {{ElementType}} {{elementVariable}} = default!;
                    {{FuncAssignFlat(resultSelector,nameof(resultSelector),elementVariable,outerValue,$"{group}[{groupIndex}]")}}
                    {{bodyCode}}
                }
            }
        }
        """;
}
