using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq;

internal static class CardinalityAnalysis {
    public readonly record struct IntegralRange(int Min,int Max) {
        public long Count => (long)this.Max-this.Min+1;
    }

    public static int? TryGetUpperBound(ExpressionSyntax expression,SemanticModel? semanticModel) {
        if(TryGetIntegralRange(expression,semanticModel) is { } range) {
            return range.Count>int.MaxValue
                ? int.MaxValue
                : (int)range.Count;
        }

        expression=StripTransparentExpressions(expression,semanticModel);
        if(expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.ModuloExpression)
            && TryGetPositiveDivisor(binary.Right,semanticModel,out var divisor)) {
            return divisor>=int.MaxValue/2
                ? int.MaxValue
                : checked(divisor*2-1);
        }
        return null;
    }

    public static IntegralRange? TryGetIntegralRange(ExpressionSyntax expression,SemanticModel? semanticModel) {
        expression=StripTransparentExpressions(expression,semanticModel);
        if(expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.ModuloExpression)
            && TryGetPositiveDivisor(binary.Right,semanticModel,out var divisor)) {
            var extent=divisor-1;
            return new IntegralRange(-extent,extent);
        }

        return null;
    }

    private static ExpressionSyntax StripTransparentExpressions(ExpressionSyntax expression,SemanticModel? semanticModel) {
        while(true) {
            switch(expression) {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression=parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression=cast.Expression;
                    continue;
                case MemberAccessExpressionSyntax memberAccess when AnchorAnalysis.IsAnchorAccess(memberAccess,semanticModel):
                    expression=memberAccess.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static bool TryGetPositiveDivisor(ExpressionSyntax expression,SemanticModel? semanticModel,out int divisor) {
        expression=StripTransparentExpressions(expression,semanticModel);
        object? constantValue;
        if(semanticModel is not null) {
            var constant=semanticModel.GetConstantValue(expression);
            if(!constant.HasValue || constant.Value is null) {
                divisor=0;
                return false;
            }
            constantValue=constant.Value;
        } else if(TryGetLiteralValue(expression,out var literalValue)) {
            constantValue=literalValue;
        } else {
            divisor=0;
            return false;
        }

        try {
            var value=constantValue switch {
                sbyte x => Math.Abs((long)x),
                byte x => x,
                short x => Math.Abs((long)x),
                ushort x => x,
                int x => Math.Abs((long)x),
                uint x => x>int.MaxValue ? int.MaxValue : (int)x,
                long x => Math.Abs(x),
                ulong x => x>int.MaxValue ? int.MaxValue : (long)x,
                _ => 0
            };
            if(value<=0) {
                divisor=0;
                return false;
            }
            divisor=value>int.MaxValue ? int.MaxValue : (int)value;
            return true;
        } catch(OverflowException) {
            divisor=int.MaxValue;
            return true;
        }
    }

    private static bool TryGetLiteralValue(ExpressionSyntax expression,out object? value) {
        expression=expression switch {
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.UnaryMinusExpression) => prefix,
            _ => StripTransparentExpressions(expression,null)
        };
        switch(expression) {
            case LiteralExpressionSyntax literal:
                value=literal.Token.Value;
                return true;
            case PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax literal }:
                value=literal.Token.Value switch {
                    sbyte x => -x,
                    short x => -x,
                    int x => -x,
                    long x => -x,
                    _ => null
                };
                return value is not null;
            default:
                value=null;
                return false;
        }
    }
}
