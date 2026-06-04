namespace Seamless.SourceGenerator.Linq.Fragments;

public class DefaultIfEmpty(int 番号, Enumerable source) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{hasValue}};
        private bool {{yielded}};
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            this.{{hasValue}}=true;
            current=this.{{currentValue}}={{e}}.Current;
            return true;
        }
        if(!this.{{hasValue}}&&!this.{{yielded}}) {
            this.{{yielded}}=true;
            current=this.{{currentValue}}=default!;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {source.ElementType} defaultValue";
    public override string GetEnumeratorFields => $$"""
        private bool {{hasValue}};
        private bool {{yielded}};
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            this.{{hasValue}}=true;
            current=this.{{currentValue}}={{e}}.Current;
            return true;
        }
        if(!this.{{hasValue}}&&!this.{{yielded}}) {
            this.{{yielded}}=true;
            current=this.{{currentValue}}=defaultValue;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}
