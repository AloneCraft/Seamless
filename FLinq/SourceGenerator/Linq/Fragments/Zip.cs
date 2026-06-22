namespace Seamless.FLinq.Linq.Fragments;

public class Zip(int 番号, Enumerable source, Enumerable second) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{this.Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    public override string GetTryGetNextBody => $$"""
        if(source.MoveNext()&&second.MoveNext()) {
            current=this.{{this.currentValue}}=(source.Current,second.Current);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"({source.ElementType} First, {second.ElementType} Second)";
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}

public class Zip3(int 番号, Enumerable source, Enumerable second, Enumerable third) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2, TEnum3>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>\n    where TEnum3 : struct, global::System.Collections.Generic.IEnumerator<{third.ElementType}>";
    public override string EnumeratorTypeName => $"{this.Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}, {third.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{third.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {third.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, TEnum3 third";
    public override string GetTryGetNextBody => $$"""
        if(source.MoveNext()&&second.MoveNext()&&third.MoveNext()) {
            current=this.{{this.currentValue}}=(source.Current,second.Current,third.Current);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"({source.ElementType} First, {second.ElementType} Second, {third.ElementType} Third)";
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();\nthird.Dispose();";
}

public class Zip_resultSelector(int 番号, Enumerable source, Enumerable second, FuncFragment resultSelector) : Enumerable(番号) {
    private string ResultSelectorFuncType => $"global::System.Func<{source.ElementType}, {second.ElementType}, {FuncReturnTypeName(resultSelector)}>";
    public override string StructMethod => this.FuncSupport(resultSelector,nameof(resultSelector));
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{this.Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}{this.ClosureArgsSuffix(resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second{this.ClosurePrimaryCtorSuffix(resultSelector)}";
    public override string GetTryGetNextBody => $$"""
        if(source.MoveNext()&&second.MoveNext()) {
            var {{this.item}}=source.Current;
            var {{this.temp}}=second.Current;
            {{this.FuncAssignCurrentIndented("    ",resultSelector,nameof(resultSelector),this.currentValue,this.item,this.temp)}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => FuncReturnTypeName(resultSelector);
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
