using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

/*
RangeArithmeticOptimizer は Enumerable.Range(start,count) の集計(Sum/Average/Min/Max/Aggregate)を、
要素を数え上げずに和の公式と単調性で求めるコード片を作る。

前提(FLinq の契約): .Anchor の無い選択子・畳み込み関数は純粋で例外を投げないので、評価を省いてよい。
演算子が定める結果と例外(空なら InvalidOperationException、int の Sum の桁あふれなら OverflowException)は System.Linq と揃える。

- 和: count*start + count*(count-1)/2。count<2^31 なので long に収まる。
- int の Sum は checked の逐次加算と同じく、途中の和が int を外れたら桁あふれする。途中の和は負の要素を足し終えたところが最小、
  最後が最大なので、その2つだけを確かめればよい。
- double の逐次加算は、途中の和がすべて 2^53 以下の整数なら丸めが起きず、和の公式と同じ値になる。超えるときは数え上げる。
- 選択子は x の1次式(x*k+b。x、x*c、c*x、x+c、x-c、単項マイナス、括弧)と、x の型変換 (long)x・(double)x だけを扱う。
  1次式の値が区間の両端で int に収まれば、途中も収まり(1次式なので両端が最小と最大)、単調なので最大・最小は端の値になる。
*/
internal static class RangeArithmeticOptimizer {
    // 選択子 x => x*Scale+Offset(int の演算)。Conversion は (long)x・(double)x のときの変換先(1次式は x そのもの)。
    internal readonly record struct LinearSelector(long Scale,long Offset,string? Conversion);

    public static bool TryParseLinear(FuncFragment selector,out LinearSelector linear) {
        linear=default;
        if(selector.ContainsAnchor
            ||selector.RequiresStatementInlining
            ||selector.CanInvokeAsDelegate
            ||!selector.CanInlineAssign
            ||selector.ParameterNames.Length!=1) {
            return false;
        }
        var body = SyntaxFactory.ParseExpression(selector.InlineBody);
        if(body.ContainsDiagnostics) {
            return false;
        }
        var parameter = selector.ParameterNames[0];
        body=Unparenthesize(body);
        if(body is CastExpressionSyntax cast
            &&cast.Type is PredefinedTypeSyntax { Keyword.ValueText: "long" or "double" } conversion
            &&Unparenthesize(cast.Expression) is IdentifierNameSyntax castOperand
            &&castOperand.Identifier.ValueText==parameter) {
            linear=new LinearSelector(1,0,conversion.Keyword.ValueText);
            return true;
        }
        if(selector.ReturnTypeName!="int"||!TryParseAffine(body,parameter,out var scale,out var offset)||scale==0&&offset==0&&!ReferencesParameter(body,parameter)) {
            return false;
        }
        linear=new LinearSelector(scale,offset,null);
        return true;
    }

    // 1次式 scale*x+offset。係数は |値|≦2^31 に限る(実行時の端点計算を long で桁あふれさせない)。
    private static bool TryParseAffine(ExpressionSyntax expression,string parameter,out long scale,out long offset) {
        scale=0;
        offset=0;
        expression=Unparenthesize(expression);
        switch(expression) {
            case IdentifierNameSyntax identifier when identifier.Identifier.ValueText==parameter:
                scale=1;
                return true;
            case LiteralExpressionSyntax literal when literal.Token.Value is int value:
                offset=value;
                return true;
            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                if(!TryParseAffine(unary.Operand,parameter,out scale,out offset)) {
                    return false;
                }
                scale=-scale;
                offset=-offset;
                return InRange(scale)&&InRange(offset);
            case BinaryExpressionSyntax binary:
                if(!TryParseAffine(binary.Left,parameter,out var leftScale,out var leftOffset)
                    ||!TryParseAffine(binary.Right,parameter,out var rightScale,out var rightOffset)) {
                    return false;
                }
                switch(binary.Kind()) {
                    case SyntaxKind.AddExpression:
                        scale=leftScale+rightScale;
                        offset=leftOffset+rightOffset;
                        break;
                    case SyntaxKind.SubtractExpression:
                        scale=leftScale-rightScale;
                        offset=leftOffset-rightOffset;
                        break;
                    case SyntaxKind.MultiplyExpression when leftScale==0:
                        scale=leftOffset*rightScale;
                        offset=leftOffset*rightOffset;
                        break;
                    case SyntaxKind.MultiplyExpression when rightScale==0:
                        scale=leftScale*rightOffset;
                        offset=leftOffset*rightOffset;
                        break;
                    default:
                        return false;
                }
                return InRange(scale)&&InRange(offset);
            default:
                return false;
        }
    }

    private static bool InRange(long value) => value>=-(1L<<31)&&value<=1L<<31;

    private static bool ReferencesParameter(ExpressionSyntax expression,string parameter) =>
        expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier => identifier.Identifier.ValueText==parameter);

    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    // 畳み込み関数が (a,x) => a+x(または x+a)か。
    public static bool IsAccumulatorPlusElement(FuncFragment func) {
        if(func.ContainsAnchor
            ||func.RequiresStatementInlining
            ||func.CanInvokeAsDelegate
            ||!func.CanInlineAssign
            ||func.ParameterNames.Length!=2
            ||func.ReturnTypeName is not ("int" or "long")) {
            return false;
        }
        var body = SyntaxFactory.ParseExpression(func.InlineBody);
        if(body.ContainsDiagnostics
            ||Unparenthesize(body) is not BinaryExpressionSyntax binary
            ||!binary.IsKind(SyntaxKind.AddExpression)
            ||Unparenthesize(binary.Left) is not IdentifierNameSyntax left
            ||Unparenthesize(binary.Right) is not IdentifierNameSyntax right) {
            return false;
        }
        var accumulator = func.ParameterNames[0];
        var element = func.ParameterNames[1];
        return left.Identifier.ValueText==accumulator&&right.Identifier.ValueText==element
            ||left.Identifier.ValueText==element&&right.Identifier.ValueText==accumulator;
    }

    // start・count を一度だけ評価し、Range と同じ引数検査をする。sum は long の和。
    private static string Prepare(RangeRoot range,string start,string count,string sum) => $$"""
        {{range.RenderArgumentPreparation(start,count)}}
        var {{sum}}=(long){{count}}*{{start}}+(long){{count}}*({{count}}-1)/2;
        """;

    // int の Sum(checked の逐次加算と同じ桁あふれ)。
    public static string RenderInt32Sum(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        var negative = $"__negative{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                var {{negative}}={{start}}<0 ? global::System.Math.Min((long){{count}},-(long){{start}}) : 0L;
                if({{negative}}*{{start}}+{{negative}}*({{negative}}-1)/2<int.MinValue||{{sum}}>int.MaxValue) throw new global::System.OverflowException();
                {{destVar}}=({{destType}})(int){{sum}};
            }
            """;
    }

    // long の和(int の要素を long へ広げた和は桁あふれしない)。
    public static string RenderInt64Sum(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                {{destVar}}=({{destType}}){{sum}};
            }
            """;
    }

    // double の逐次加算の和。途中の和が 2^53 以下の整数なら和の公式と同じ値。超えるなら数え上げる。
    public static string RenderDoubleSum(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        var index = $"__index{number}";
        var running = $"__running{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                if({{ExactInDouble(start,count)}}){
                    {{destVar}}=({{destType}})(double){{sum}};
                }else{
                    var {{running}}=0d;
                    for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {{running}}+=(double)({{start}}+{{index}});
                    {{destVar}}=({{destType}}){{running}};
                }
            }
            """;
    }

    // 平均。int・long の要素は long で足すので正確。double の要素は逐次加算(2^53 以下なら和の公式と同じ)。
    public static string RenderAverage(RangeRoot range,int number,string destVar,string destType,bool doubleElements) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        var index = $"__index{number}";
        var running = $"__running{number}";
        var body = doubleElements
            ? $$"""
                if({{ExactInDouble(start,count)}}){
                    {{destVar}}=({{destType}})((double){{sum}}/{{count}});
                }else{
                    var {{running}}=0d;
                    for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {{running}}+=(double)({{start}}+{{index}});
                    {{destVar}}=({{destType}})({{running}}/{{count}});
                }
                """
            : $"{destVar}=({destType})((double){sum}/{count});";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{IndentBlock(body,"    ")}}
            }
            """;
    }

    // |途中の和| ≦ count*max(|start|,|end|) が 2^53 以下なら、double の逐次加算は丸めない。
    private static string ExactInDouble(string start,string count) =>
        $"(long){count}*global::System.Math.Max(global::System.Math.Abs((long){start}),global::System.Math.Abs((long){start}+{count}-1))<=(1L<<53)";

    // 1次式の選択子の最大・最小。両端の値が int に収まれば端の値、収まらなければ数え上げる(selectorAssign は「先 = 選択子(引数)」の文)。
    public static string RenderLinearMinMax(RangeRoot range,LinearSelector linear,int number,string destVar,string destType,string valueType,bool max,Func<string,string,string> selectorAssign) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var first = $"__first{number}";
        var last = $"__last{number}";
        var index = $"__index{number}";
        var value = $"__value{number}";
        var best = $"__best{number}";
        // 増加なら最大は終点・最小は始点、減少なら逆。
        var pickLast = max==(linear.Scale>=0);
        var comparison = max ? ">" : "<";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(range.RenderArgumentPreparation(start,count),"    ")}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                var {{first}}={{linear.Scale}}L*{{start}}+{{linear.Offset}}L;
                var {{last}}={{linear.Scale}}L*((long){{start}}+{{count}}-1)+{{linear.Offset}}L;
                if({{first}}>=int.MinValue&&{{first}}<=int.MaxValue&&{{last}}>=int.MinValue&&{{last}}<=int.MaxValue){
                    {{destVar}}=({{destType}})({{valueType}})(int){{(pickLast ? last : first)}};
                }else{
                    {{valueType}} {{best}}=default!;
                    {{IndentBlock(selectorAssign(best,start),"        ")}}
                    for(var {{index}}=1;{{index}}<{{count}};{{index}}++){
                        {{valueType}} {{value}}=default!;
                        {{IndentBlock(selectorAssign(value,$"{start}+{index}"),"            ")}}
                        if({{value}}{{comparison}}{{best}}) {{best}}={{value}};
                    }
                    {{destVar}}=({{destType}}){{best}};
                }
            }
            """;
    }

    // Aggregate(seed,(a,x)=>a+x) の畳み込み結果を accumulator へ入れる(int・long の加算は2の補数で結合的なので、逐次の和と同じ)。
    // 評価順は従来どおり Range の引数、seed の順。
    public static string RenderSeededSum(RangeRoot range,int number,string accumulator,string accumulatorType,string seed) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        var seedName = $"__seed{number}";
        return $$"""
            {{accumulatorType}} {{accumulator}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                {{accumulatorType}} {{seedName}}={{seed}};
                {{accumulator}}=unchecked(({{accumulatorType}})({{seedName}}+{{sum}}));
            }
            """;
    }

    // Aggregate((a,x)=>a+x)(seed なし)。空なら例外、そうでなければ要素の和。
    public static string RenderSeedlessSum(RangeRoot range,int number,string destVar,string destType) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var sum = $"__sum{number}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{IndentBlock(Prepare(range,start,count,sum),"    ")}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}=unchecked(({{destType}})(int){{sum}});
            }
            """;
    }

    private static string IndentBlock(string text,string indent) => GroupBySupport.IndentBlock(text,indent).TrimStart();
}
