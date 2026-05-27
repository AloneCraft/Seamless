namespace Seamless.SourceGenerator.Linq.Fragments;
public class SkipWhile(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(predicate)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(predicate)})"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(predicate)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(predicate)}"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{source.ElementType}, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool skipping = true;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            bool {{this.temp}} = default!;
            {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
            if(skipping && {{this.temp}}) {
                continue;
            }
            skipping = false;
            {{this.currentValue}} = {{this.item}};
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class SkipWhile_index(int 番号, Enumerable source, FuncFragment predicate) : SkipWhile(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(predicate)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, @this)"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(predicate)
            ? $"TEnum {this.e}, {ContainingTypeName} @this"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{source.ElementType}, int, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool skipping = true;
        private int {{this.a}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.a}}++;
            bool {{this.temp}} = default!;
            {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item,this.a)}}
            if(skipping && {{this.temp}}) {
                continue;
            }
            skipping = false;
            {{this.currentValue}} = {{this.item}};
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
}
