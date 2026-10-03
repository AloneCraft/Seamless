using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

internal static class RangePredicateOptimizer {
    public static string RenderAny(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                {{destVar}}={{count}}!=0;
            }
            """;
    }

    public static string RenderAny(RepeatRoot repeat,int number,string destVar,string destType) {
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{count}}={{repeat.CountExpression}};
                {{destVar}}={{count}}!=0;
            }
            """;
    }

    public static string? RenderFirst(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}={{start}};
            }
            """;
    }

    public static string? RenderSingle(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                if({{count}}>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}=({{destType}}){{start}};
            }
            """;
    }

    public static string? RenderSingleOrDefault(RangeRoot range,int number,string destVar,string destType,string defaultValue = "default!") {
        var start = $"__start{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                if({{count}}>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}={{count}}==0 ? {{defaultValue}} : ({{destType}}){{start}};
            }
            """;
    }

    public static string? RenderSingle(RepeatRoot repeat,int number,string destVar,string destType) {
        var value = $"__value{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{value}}={{repeat.ValueExpression}};
                var {{count}}={{repeat.CountExpression}};
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                if({{count}}>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}={{value}};
            }
            """;
    }

    public static string? RenderSingleOrDefault(RepeatRoot repeat,int number,string destVar,string destType,string defaultValue = "default!") {
        var value = $"__value{number}";
        var count = $"__count{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{value}}={{repeat.ValueExpression}};
                var {{count}}={{repeat.CountExpression}};
                if({{count}}>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}={{count}}==0 ? {{defaultValue}} : {{value}};
            }
            """;
    }

    public static string? RenderSingle(RangeRoot range,FuncFragment predicate,int number,string destVar,string destType) {
        if(!TryExtractComparison(predicate,out var comparison)
            ||comparison.Operator!=ComparisonOperator.Equal) {
            return null;
        }

        var start = $"__start{number}";
        var count = $"__count{number}";
        var last = $"__last{number}";
        var value = $"__cmp{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                var {{last}}=(long){{start}} + {{count}} - 1L;
                {{comparison.HoistedDeclarations}}
                var {{value}}=(long)({{comparison.ValueExpression}});
                if(!({{count}}>0 && {{value}}>=(long){{start}} && {{value}}<={{last}})) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}=({{destType}})(int){{value}};
            }
            """;
    }

    public static string? RenderSingleOrDefault(RangeRoot range,FuncFragment predicate,int number,string destVar,string destType,string defaultValue = "default!") {
        if(!TryExtractComparison(predicate,out var comparison)
            ||comparison.Operator!=ComparisonOperator.Equal) {
            return null;
        }

        var start = $"__start{number}";
        var count = $"__count{number}";
        var last = $"__last{number}";
        var value = $"__cmp{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                var {{last}}=(long){{start}} + {{count}} - 1L;
                {{comparison.HoistedDeclarations}}
                var {{value}}=(long)({{comparison.ValueExpression}});
                {{destVar}}={{count}}>0 && {{value}}>=(long){{start}} && {{value}}<={{last}} ? ({{destType}})(int){{value}} : {{defaultValue}};
            }
            """;
    }

    public static string? RenderMinMax(RangeRoot range,int number,string destVar,string destType,bool max) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var value = max
            ? $"(long){start} + {count} - 1L"
            : $"(long){start}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}=({{destType}})(int)({{value}});
            }
            """;
    }

    public static string? RenderAll(RangeRoot range,FuncFragment predicate,int number,string destVar,string destType) {
        if(!TryExtractComparison(predicate,out var comparison)) {
            return null;
        }

        var start = $"__start{number}";
        var count = $"__count{number}";
        var last = $"__last{number}";
        var value = $"__cmp{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                var {{last}}=(long){{start}} + {{count}} - 1L;
                {{comparison.HoistedDeclarations}}
                var {{value}}=(long)({{comparison.ValueExpression}});
                {{destVar}}={{AllCondition(comparison.Operator,start,count,last,value)}};
            }
            """;
    }

    public static string? RenderAny(RangeRoot range,FuncFragment predicate,int number,string destVar,string destType) {
        if(TryExtractComparison(predicate,out var comparison)) {
            var start = $"__start{number}";
            var count = $"__count{number}";
            var last = $"__last{number}";
            var value = $"__cmp{number}";
            return $$"""
                {{destType}} {{destVar}};
                {
                    {{range.RenderArgumentPreparation(start,count)}}
                    var {{last}}=(long){{start}} + {{count}} - 1L;
                    {{comparison.HoistedDeclarations}}
                    var {{value}}=(long)({{comparison.ValueExpression}});
                    {{destVar}}={{AnyCondition(comparison.Operator,start,count,last,value)}};
                }
                """;
        }

        if(TryExtractParity(predicate,out var wantEven)) {
            var start = $"__start{number}";
            var count = $"__count{number}";
            var parity = wantEven ? "0" : "1";
            return $$"""
                {{destType}} {{destVar}};
                {
                    {{range.RenderArgumentPreparation(start,count)}}
                    {{destVar}}={{count}}>1 || ({{count}}==1 && ({{start}} & 1)=={{parity}});
                }
                """;
        }

        return null;
    }

    public static string? RenderCount(RangeRoot range,FuncFragment predicate,int number,string destVar,string destType) {
        if(TryExtractParity(predicate,out var wantEven)) {
            var start = $"__start{number}";
            var count = $"__count{number}";
            var matched = $"__matched{number}";
            var parity = wantEven ? "0" : "1";
            return $$"""
                {{destType}} {{destVar}};
                {
                    {{range.RenderArgumentPreparation(start,count)}}
                    var {{matched}}={{count}} / 2;
                    if(({{count}} & 1)!=0 && ({{start}} & 1)=={{parity}}) {{matched}}++;
                    {{destVar}}=({{destType}}){{matched}};
                }
                """;
        }

        if(TryExtractComparison(predicate,out var comparison)) {
            var start = $"__start{number}";
            var count = $"__count{number}";
            var last = $"__last{number}";
            var value = $"__cmp{number}";
            var matched = $"__matched{number}";
            return $$"""
                {{destType}} {{destVar}};
                {
                    {{range.RenderArgumentPreparation(start,count)}}
                    var {{last}}=(long){{start}} + {{count}} - 1L;
                    {{comparison.HoistedDeclarations}}
                    var {{value}}=(long)({{comparison.ValueExpression}});
                    long {{matched}}={{CountExpression(comparison.Operator,start,count,last,value)}};
                    {{destVar}}=({{destType}}){{matched}};
                }
                """;
        }

        return null;
    }

    private static string AllCondition(ComparisonOperator op,string start,string count,string last,string value) =>
        op switch {
            ComparisonOperator.GreaterThan => $"{count}==0 || (long){start}>{value}",
            ComparisonOperator.GreaterThanOrEqual => $"{count}==0 || (long){start}>={value}",
            ComparisonOperator.LessThan => $"{count}==0 || {last}<{value}",
            ComparisonOperator.LessThanOrEqual => $"{count}==0 || {last}<={value}",
            ComparisonOperator.Equal => $"{count}==0 || ({count}==1 && (long){start}=={value})",
            ComparisonOperator.NotEqual => $"!({count}>0 && {value}>=(long){start} && {value}<={last})",
            _ => throw new InvalidOperationException()
        };

    private static string AnyCondition(ComparisonOperator op,string start,string count,string last,string value) =>
        op switch {
            ComparisonOperator.GreaterThan => $"{count}>0 && {last}>{value}",
            ComparisonOperator.GreaterThanOrEqual => $"{count}>0 && {last}>={value}",
            ComparisonOperator.LessThan => $"{count}>0 && (long){start}<{value}",
            ComparisonOperator.LessThanOrEqual => $"{count}>0 && (long){start}<={value}",
            ComparisonOperator.Equal => $"{count}>0 && {value}>=(long){start} && {value}<={last}",
            ComparisonOperator.NotEqual => $"{count}>0 && ({count}>1 || (long){start}!={value})",
            _ => throw new InvalidOperationException()
        };

    private static string CountExpression(ComparisonOperator op,string start,string count,string last,string value) =>
        op switch {
            ComparisonOperator.GreaterThan => $"{count}<=0 || {last}<={value} ? 0L : ((long){start}>{value} ? (long){count} : {last}-{value})",
            ComparisonOperator.GreaterThanOrEqual => $"{count}<=0 || {last}<{value} ? 0L : ((long){start}>={value} ? (long){count} : {last}-{value}+1L)",
            ComparisonOperator.LessThan => $"{count}<=0 || (long){start}>={value} ? 0L : ({last}<{value} ? (long){count} : {value}-(long){start})",
            ComparisonOperator.LessThanOrEqual => $"{count}<=0 || (long){start}>{value} ? 0L : ({last}<={value} ? (long){count} : {value}-(long){start}+1L)",
            ComparisonOperator.Equal => $"{count}>0 && {value}>=(long){start} && {value}<={last} ? 1L : 0L",
            ComparisonOperator.NotEqual => $"{count} - ({count}>0 && {value}>=(long){start} && {value}<={last} ? 1L : 0L)",
            _ => throw new InvalidOperationException()
        };

    private static bool TryExtractComparison(FuncFragment predicate,out RangeComparison comparison) {
        comparison=default;
        if(!CanInspect(predicate)
            ||predicate.ParameterNames.Length!=1
            ||TryParseExpression(predicate.InlineBody) is not BinaryExpressionSyntax binary
            ||!TryGetComparisonOperator(binary.Kind(),out var op)) {
            return false;
        }

        var parameterName = predicate.ParameterNames[0];
        if(IsParameter(binary.Left,parameterName)&&!ContainsParameter(binary.Right,parameterName)) {
            comparison=new RangeComparison(op,binary.Right.ToString(),ClosureRenderSupport.HoistedLocalDeclarations(predicate));
            return true;
        }

        if(IsParameter(binary.Right,parameterName)&&!ContainsParameter(binary.Left,parameterName)) {
            comparison=new RangeComparison(Invert(op),binary.Left.ToString(),ClosureRenderSupport.HoistedLocalDeclarations(predicate));
            return true;
        }

        return false;
    }

    // 述語が x の偶奇(添字つきなら添字の偶奇)だけを見る形か。onIndex は添字(第2引数)の偶奇を見るとき true。
    public static bool TryExtractParity(FuncFragment predicate,bool onIndex,out bool wantEven) {
        wantEven=false;
        if(!onIndex) {
            return TryExtractParity(predicate,out wantEven);
        }
        if(!CanInspect(predicate)
            ||predicate.ParameterNames.Length!=2
            ||TryParseExpression(predicate.InlineBody) is not BinaryExpressionSyntax binary
            ||binary.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            ||ContainsParameter(binary,predicate.ParameterNames[0])) {
            return false;
        }
        var indexName = predicate.ParameterNames[1];
        if(TryExtractBitAndOne(binary.Left,indexName)&&TryGetSmallInteger(binary.Right,out var right)) {
            return TryMapParity(binary.Kind(),right,out wantEven);
        }
        if(TryExtractBitAndOne(binary.Right,indexName)&&TryGetSmallInteger(binary.Left,out var left)) {
            return TryMapParity(binary.Kind(),left,out wantEven);
        }
        return false;
    }

    private static bool TryExtractParity(FuncFragment predicate,out bool wantEven) {
        wantEven=false;
        if(!CanInspect(predicate)
            ||predicate.ParameterNames.Length!=1
            ||TryParseExpression(predicate.InlineBody) is not BinaryExpressionSyntax binary
            ||binary.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)) {
            return false;
        }

        var parameterName = predicate.ParameterNames[0];
        if(TryExtractBitAndOne(binary.Left,parameterName)&&TryGetSmallInteger(binary.Right,out var right)) {
            return TryMapParity(binary.Kind(),right,out wantEven);
        }

        if(TryExtractBitAndOne(binary.Right,parameterName)&&TryGetSmallInteger(binary.Left,out var left)) {
            return TryMapParity(binary.Kind(),left,out wantEven);
        }

        return false;
    }

    private static bool TryMapParity(SyntaxKind equalityKind,int value,out bool wantEven) {
        wantEven=false;
        if(value is not (0 or 1)) {
            return false;
        }

        wantEven=equalityKind==SyntaxKind.EqualsExpression
            ? value==0
            : value!=0;
        return true;
    }

    private static bool TryExtractBitAndOne(ExpressionSyntax expression,string parameterName) {
        expression=Unwrap(expression);
        if(expression is not BinaryExpressionSyntax binary||binary.Kind()!=SyntaxKind.BitwiseAndExpression) {
            return false;
        }

        return (IsParameter(binary.Left,parameterName)&&TryGetSmallInteger(binary.Right,out var right)&&right==1)
            ||(IsParameter(binary.Right,parameterName)&&TryGetSmallInteger(binary.Left,out var left)&&left==1);
    }

    private static bool CanInspect(FuncFragment predicate) =>
        predicate.CanInlineAssign
        &&!predicate.RequiresStatementInlining
        &&!string.IsNullOrWhiteSpace(predicate.InlineBody);

    private static ExpressionSyntax? TryParseExpression(string expression) {
        var parsed = SyntaxFactory.ParseExpression(expression);
        return parsed.ContainsDiagnostics ? null : Unwrap(parsed);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsParameter(ExpressionSyntax expression,string parameterName) =>
        Unwrap(expression) is IdentifierNameSyntax identifier
        &&string.Equals(identifier.Identifier.ValueText,parameterName,StringComparison.Ordinal);

    private static bool ContainsParameter(ExpressionSyntax expression,string parameterName) =>
        expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => string.Equals(identifier.Identifier.ValueText,parameterName,StringComparison.Ordinal));

    private static bool TryGetSmallInteger(ExpressionSyntax expression,out int value) {
        expression=Unwrap(expression);
        if(expression is LiteralExpressionSyntax literal&&literal.Token.Value is int intValue) {
            value=intValue;
            return true;
        }

        value=0;
        return false;
    }

    private static bool TryGetComparisonOperator(SyntaxKind kind,out ComparisonOperator op) {
        op=kind switch {
            SyntaxKind.GreaterThanExpression => ComparisonOperator.GreaterThan,
            SyntaxKind.GreaterThanOrEqualExpression => ComparisonOperator.GreaterThanOrEqual,
            SyntaxKind.LessThanExpression => ComparisonOperator.LessThan,
            SyntaxKind.LessThanOrEqualExpression => ComparisonOperator.LessThanOrEqual,
            SyntaxKind.EqualsExpression => ComparisonOperator.Equal,
            SyntaxKind.NotEqualsExpression => ComparisonOperator.NotEqual,
            _ => default,
        };
        return kind is SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression;
    }

    private static ComparisonOperator Invert(ComparisonOperator op) =>
        op switch {
            ComparisonOperator.GreaterThan => ComparisonOperator.LessThan,
            ComparisonOperator.GreaterThanOrEqual => ComparisonOperator.LessThanOrEqual,
            ComparisonOperator.LessThan => ComparisonOperator.GreaterThan,
            ComparisonOperator.LessThanOrEqual => ComparisonOperator.GreaterThanOrEqual,
            ComparisonOperator.Equal => ComparisonOperator.Equal,
            ComparisonOperator.NotEqual => ComparisonOperator.NotEqual,
            _ => throw new InvalidOperationException()
        };

    // HoistedDeclarations: 比較の値がループの外へ巻き上げた変数(__hoist*)を参照するときの宣言。値の式より前に出す。
    private readonly record struct RangeComparison(ComparisonOperator Operator,string ValueExpression,string HoistedDeclarations);

    private enum ComparisonOperator {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual,
    }
}
