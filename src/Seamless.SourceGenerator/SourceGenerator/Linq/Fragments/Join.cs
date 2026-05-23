namespace Seamless.SourceGenerator.Linq.Fragments;
public class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号) {
    public override string StructMethod => $$"""
                                             {{this.DefineStruct(outerKeySelector,nameof(outerKeySelector))}}
                                             {{this.DefineStruct(innerKeySelector,nameof(innerKeySelector))}}
                                             {{this.DefineStruct(resultSelector,nameof(resultSelector))}}
                                             """;
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({outer.GetEnumerableExpression}, {inner.GetEnumerableExpression}, {this.FuncDelegate(outerKeySelector,nameof(outerKeySelector))}, {this.FuncDelegate(innerKeySelector,nameof(innerKeySelector))}, {this.FuncDelegate(resultSelector,nameof(resultSelector))}, {comparer})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{outer.ElementType}}, {{FuncReturnTypeName(outerKeySelector)}}> {{nameof(outerKeySelector)}};
        private readonly global::System.Func<{{inner.ElementType}}, {{FuncReturnTypeName(innerKeySelector)}}> {{nameof(innerKeySelector)}};
        private readonly global::System.Func<{{outer.ElementType}}, {{inner.ElementType}}, {{this.ElementType}}> {{nameof(resultSelector)}};
        private readonly global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}> {{this.lookup}};
        private readonly global::System.Collections.Generic.IEnumerator<{{outer.ElementType}}> {{this.e}};
        private global::System.Collections.Generic.List<{{inner.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{outer.ElementType}} {{this.outerValue}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            global::System.Collections.Generic.IEnumerable<{{outer.ElementType}}> outer,
            global::System.Collections.Generic.IEnumerable<{{inner.ElementType}}> inner,
            global::System.Func<{{outer.ElementType}}, {{FuncReturnTypeName(outerKeySelector)}}> outerKeySelector,
            global::System.Func<{{inner.ElementType}}, {{FuncReturnTypeName(innerKeySelector)}}> innerKeySelector,
            global::System.Func<{{outer.ElementType}}, {{inner.ElementType}}, {{this.ElementType}}> resultSelector,
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer) {
            this.{{nameof(outerKeySelector)}} = outerKeySelector;
            this.{{nameof(innerKeySelector)}} = innerKeySelector;
            this.{{nameof(resultSelector)}} = resultSelector;
            {{this.lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            foreach(var {{this.item}} in inner) {
                {{this.lookup}}.Add({{nameof(innerKeySelector)}}({{this.item}}), {{this.item}});
            }
            {{this.e}} = outer.GetEnumerator();
            {{this.group}} = null;
            {{this.groupIndex}} = 0;
            {{this.state}} = 0;
            {{this.outerValue}} = default!;
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if({{this.state}}==1 && {{this.group}} is not null) {
            if({{this.groupIndex}}<{{this.group}}.Count) {
                var {{this.item}} = {{this.group}}[{{this.groupIndex}}++];
                {{this.currentValue}} = {{nameof(resultSelector)}}({{this.outerValue}}, {{this.item}});
                current = {{this.currentValue}};
                return true;
            }
            {{this.group}} = null;
            {{this.state}} = 0;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.outerValue}} = {{this.e}}.Current;
        {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>({{this.lookup}}[{{nameof(outerKeySelector)}}({{this.outerValue}})]);
        {{this.groupIndex}} = 0;
        {{this.state}} = 1;
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string ElementType => FuncReturnTypeName(resultSelector);
}
