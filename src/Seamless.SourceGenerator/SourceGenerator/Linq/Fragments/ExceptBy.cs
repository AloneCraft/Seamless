namespace Seamless.SourceGenerator.Linq.Fragments;

public class ExceptBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : ExceptBy_comparer(番号, source, second, keySelector, "null") { }

public class ExceptBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : SetByBase(番号, source, second, keySelector, comparer, "except") { }

public abstract class SetByBase(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer, string operation) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> {{lookup}}=new(comparer);
        private byte {{state}};
        """;
    public override string GetTryGetNextBody => operation switch {
        "except" => $$"""
            if(this.{{state}}==0) {
                while(second.MoveNext()) this.{{lookup}}.Add(({{FuncReturnTypeName(keySelector)}})(object)second.Current!);
                this.{{state}}=1;
            }
            while(source.MoveNext()) {
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Add({{key}})) {
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        "intersect" => $$"""
            if(this.{{state}}==0) {
                while(second.MoveNext()) this.{{lookup}}.Add(({{FuncReturnTypeName(keySelector)}})(object)second.Current!);
                this.{{state}}=1;
            }
            while(source.MoveNext()) {
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Remove({{key}})) {
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        _ => $$"""
            while(source.MoveNext()) {
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Add({{key}})) {
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            if(this.{{state}}==0) {
                this.{{state}}=1;
                while(second.MoveNext()) {
                    var {{item}}=({{source.ElementType}})(object)second.Current!;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(this.{{lookup}}.Add({{key}})) {
                        current=this.{{currentValue}}={{item}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """,
    };
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
