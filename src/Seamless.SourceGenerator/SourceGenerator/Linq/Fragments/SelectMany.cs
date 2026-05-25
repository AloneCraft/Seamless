namespace Seamless.SourceGenerator.Linq.Fragments;
/*
以前の loop 展開版の断片は記録として残すが、実際の実装は Enumerator ベースに統一した。
*/

public abstract class SelectManyBase(int 番号, Enumerable source) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    protected abstract string CollectionElementType { get; }
    protected abstract string CollectionSelectorPrimaryConstructorParameter { get; }
    protected abstract string CreateInnerEnumeratorExpression { get; }
    protected virtual string MoveNextOuterPrologue => string.Empty;
    protected abstract string CurrentValueAssignmentFromInner { get; }

    public abstract override string StructMethod { get; }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {this.CollectionSelectorPrimaryConstructorParameter}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{this.CollectionElementType}}>? innerEnumerator = null;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        private {{this.ElementType}} {{this.currentValue}} = default!;
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(this.innerEnumerator is not null) {
            if(this.innerEnumerator.MoveNext()) {
                var {{this.item}} = this.innerEnumerator.Current;
                {{this.CurrentValueAssignmentFromInner}}
                current = {{this.currentValue}};
                return true;
            }
            this.innerEnumerator.Dispose();
            this.innerEnumerator = null;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.MoveNextOuterPrologue}}
        {{this.outerValue}} = {{this.e}}.Current;
        this.innerEnumerator = {{this.CreateInnerEnumeratorExpression}};
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.innerEnumerator is not null) {
            this.innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;
}

public class SelectMany(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector, nameof(selector))})";
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> selector";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(selector)}({this.outerValue}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

public class SelectMany_index(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector, nameof(selector))})";
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, int, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> selector";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.index}} = -1;
        """;
    protected override string MoveNextOuterPrologue => $"{this.index}++;";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(selector)}({this.outerValue}, {this.index}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

public class SelectMany_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(collectionSelector, nameof(collectionSelector))}}
        {{this.DefineStruct(resultSelector, nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(collectionSelector, nameof(collectionSelector))}, {this.FuncDelegate(resultSelector, nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => $$"""TEnum {{this.e}}, global::System.Func<{{source.ElementType}}, global::System.Collections.Generic.IEnumerable<{{this.CollectionElementType}}>> collectionSelector, global::System.Func<{{source.ElementType}}, {{this.CollectionElementType}}, {{this.ElementType}}> resultSelector""";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> collectionSelector";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(collectionSelector)}({this.outerValue}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {nameof(resultSelector)}({this.outerValue}, {this.item});";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}

public class SelectMany_index_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(collectionSelector, nameof(collectionSelector))}}
        {{this.DefineStruct(resultSelector, nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(collectionSelector, nameof(collectionSelector))}, {this.FuncDelegate(resultSelector, nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => $$"""TEnum {{this.e}}, global::System.Func<{{source.ElementType}}, int, global::System.Collections.Generic.IEnumerable<{{this.CollectionElementType}}>> collectionSelector, global::System.Func<{{source.ElementType}}, {{this.CollectionElementType}}, {{this.ElementType}}> resultSelector""";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.index}} = -1;
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, int, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> collectionSelector";
    protected override string MoveNextOuterPrologue => $"{this.index}++;";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(collectionSelector)}({this.outerValue}, {this.index}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {nameof(resultSelector)}({this.outerValue}, {this.item});";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}
