using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using Seamless.FLinq;

namespace Seamless.FLinq.Linq.Fragments;

public readonly record struct HoistedLocal(string Name,string TypeName,string ExpressionText);

/*
FuncFragment は predicate/selector/accumulator などの lambda を Fragment 内でどう呼ぶかを表す。

入力例:
  x => x.Price * rate
  (x, index) => index % 2 == 0

生成されるコード例:
  // inline 可能な lambda
  destination = item.Price * this.rate;

  // 匿名型戻り値など型名を書けない lambda
  destination = this.selectorDelegate.Invoke(item);

主な分岐:
  - CanInlineAssign: lambda 本体を代入文や式として直接展開する。
  - RequiresStatementInlining: lambda 内 LINQ など複数文が必要な場合、一時変数経由で展開する。
  - CanInvokeAsDelegate: 匿名型や delegate 保持が必要な場合、Enumerator のフィールドから Invoke する。
  - HoistedLocals: struct 内メンバーとして保持した一時値を this. 接頭辞付きに書き換える。
*/
public abstract class FuncFragment {
    private string? hoistedLocalReceiver;

    // delegate 戻り値型名。
    public string ReturnTypeName="void";
    // delegate 引数型名一覧。
    public string[] ParameterTypeNames=[];
    // delegate 引数名一覧。
    public string[] ParameterNames=[];
    // closure なしで struct 内へ直接式を展開できるかどうか。
    public bool CanGenerateMethod=true;
    // 代入文として展開できるかどうか。
    // ループ本体で `current = selector(item);` のような形を作るとき、
    // いったん helper メソッドを呼ばずにラムダ本体をその場へ埋め込むために使う。
    public bool CanInlineAssign;
    // static ラムダの本体を保持する。
    // これをそのまま使うのではなく、実引数へ置換して
    // `result0.Add(p + 1)` や `if ((p * 10) > 20)` のような式へ落とす。
    public string InlineBody=string.Empty;
    // ラムダがキャプチャしているローカル変数・外側パラメータ一覧（ラムダ引数は除く）。
    // struct closure 時に primary ctor へそのまま渡して backing field 化する。
    public (string Name, string TypeName)[] CapturedLocals = [];
    public HoistedLocal[] HoistedLocals = [];
    // ラムダ本体内のフィールド・static メソッドアクセス用に生成した UnsafeAccessor extern 宣言コード。
    // Fragment.CollectAllUnsafeAccessorCodes() で収集して補助クラスに出力される。
    public string[] BodyUnsafeAccessorCodes = [];
    // ラムダ本体を展開するために必要な補助 struct 群。
    // 例: keySelector 内の Count()/Any() を scalar Fragment として展開する場合。
    public virtual string SupportingMemberSources { get; set; } = string.Empty;
    // 式位置ではなく文として展開する必要があるラムダかどうか。
    // LINQ scalar を内包するラムダは enumerator 生成・TryGetNext 呼び出しが必要になる。
    public bool RequiresStatementInlining;
    // Anchor を含む式は生成コード上では identity だが、評価回数・評価順を変え得る最適化では境界として扱う。
    public bool ContainsAnchor;
    // keySelector の値域上限を静的に推定できる場合の容量ヒント。
    public int? CardinalityUpperBound;
    public int? IntegralRangeMin;
    public int? IntegralRangeMax;
    // 代入文として inline 展開する具体実装を派生型に任せる。
    public virtual string RenderInlineAssign(string destination,int number,params string[] arguments) {
        throw new InvalidOperationException("Inline assign is not supported.");
    }

    // 条件式や Add 引数など、式位置へ inline 展開する具体実装を派生型に任せる。
    public virtual string RenderInlineExpression(params string[] arguments) {
        throw new InvalidOperationException("Inline expression is not supported.");
    }

    public string RenderInlineAssignWithHoistedReceiver(string receiver,string destination,int number,params string[] arguments) {
        var previousReceiver=hoistedLocalReceiver;
        hoistedLocalReceiver=receiver;
        try {
            return RenderInlineAssign(destination,number,arguments);
        } finally {
            hoistedLocalReceiver=previousReceiver;
        }
    }

    public string RenderInlineExpressionWithHoistedReceiver(string receiver,params string[] arguments) {
        var previousReceiver=hoistedLocalReceiver;
        hoistedLocalReceiver=receiver;
        try {
            return RenderInlineExpression(arguments);
        } finally {
            hoistedLocalReceiver=previousReceiver;
        }
    }

    // 匿名型ラムダなど型名を書けない場合にデリゲートとして保持するかどうか。
    public virtual bool CanInvokeAsDelegate => false;
    // 匿名型戻り値など、生成 struct 側で TResult 型パラメータが必要かどうか。
    public virtual bool RequiresResultTypeParameter => false;
    // デリゲートの型名（"global::System.Func<TSource, TResult>" 等）。
    public virtual string? DelegateTypeName => null;
    // Enumerator struct に生成するデリゲートフィールド名。
    public virtual string? DelegateFieldName => null;
    // TryGetNext 等で使うデリゲート呼び出し式（"this._del0.Invoke(item0)" 等）。
    public virtual string? DelegateInvokeExpr => null;
    // Enumerator struct のコンストラクタに渡すデリゲート引数（ラムダ式テキスト）。
    public virtual string? DelegateConstructorArg => null;
    public virtual string RenderAsLambda() {
        if(DelegateConstructorArg is { } delegateConstructorArg) {
            return delegateConstructorArg;
        }
        if(RequiresStatementInlining || string.IsNullOrWhiteSpace(InlineBody)) {
            throw new InvalidOperationException("Lambda expression rendering is not supported.");
        }
        var parameters = ParameterNames.Length==1
            ? ParameterNames[0]
            : $"({string.Join(", ",ParameterNames)})";
        return $"{parameters}=>{InlineBody}";
    }

    // 代入文向け inline 展開の共通処理。
    protected string RenderInlineAssignCore(string destination,int number,params string[] arguments) {
        var builder = new StringBuilder();
        var rewrittenBody= InlineBody;
        for(var index = 0; index<arguments.Length; index++) {
            var arg = arguments[index];
            string replacement;
            if(IsSimpleExpression(arg)) {
                // 単純な識別子・メンバーアクセスは副作用なし → 一時変数不要で直接展開する。
                replacement = arg;
            } else {
                // メソッド呼び出し等の副作用のある式は多重評価を避けるため一時変数へ退避する。
                // 例えば selector(GetValue()) をそのまま展開すると GetValue() が複数回評価されうる。
                var tempName=$"__arg{number}_{SanitizeLocalName(destination)}_{index}";
                builder.AppendLine($"var {tempName}={arg};");
                replacement = tempName;
            }
            if(index<ParameterNames.Length && !string.IsNullOrWhiteSpace(ParameterNames[index])) {
                rewrittenBody=ReplaceParameterReference(rewrittenBody,ParameterNames[index],replacement);
            }
        }

        rewrittenBody=QualifyHoistedLocalReferences(rewrittenBody);
        builder.Append($"{destination}={rewrittenBody};");
        ContainsAnchor |= AnchorAnalysis.LooksLikeAnchorText(rewrittenBody);
        return builder.ToString();
    }

    // 識別子・ドット区切りメンバーアクセスだけからなる式は副作用がなく一時変数不要。
    private static bool IsSimpleExpression(string expr) =>
        Regex.IsMatch(expr, @"^[@\w\.]+$");

    // 式位置向け inline 展開の共通処理。
    protected string RenderInlineExpressionCore(params string[] arguments) {
        var rewrittenBody= InlineBody;
        for(var index = 0; index<arguments.Length; index++) {
            if(index<ParameterNames.Length && !string.IsNullOrWhiteSpace(ParameterNames[index])) {
                // 条件式や Add 引数のような「式の中」に埋め込む場合は、
                // helper メソッド呼び出し `StructX_selector.selector(value)` を残さず、
                // ラムダ引数名を実引数へ直接置換して式そのものを作る。
                // こうしておくと生成コードを読んだときに元の LINQ の意味が追いやすく、
                // JIT にも普通の式として見せられる。
                rewrittenBody=ReplaceParameterReference(rewrittenBody,ParameterNames[index],FormatInlineArgument(arguments[index]));
            }
        }

        rewrittenBody=QualifyHoistedLocalReferences(rewrittenBody);
        ContainsAnchor |= AnchorAnalysis.LooksLikeAnchorText(rewrittenBody);
        return CanOmitOuterParentheses(rewrittenBody) ? rewrittenBody : $"({rewrittenBody})";
    }

    private string QualifyHoistedLocalReferences(string text) {
        if(string.IsNullOrEmpty(hoistedLocalReceiver) || HoistedLocals.Length==0) {
            return text;
        }

        var rewritten=text;
        foreach(var hoistedLocal in HoistedLocals) {
            rewritten=Regex.Replace(
                rewritten,
                $@"(?<![\w\.]){Regex.Escape(hoistedLocal.Name)}\b",
                $"{hoistedLocalReceiver}{hoistedLocal.Name}",
                RegexOptions.CultureInvariant);
        }

        return rewritten;
    }

    protected static string ReplaceParameterReferences(string text,string[] parameterNames,string[] arguments) {
        var rewritten = text;
        for(var index = 0; index<parameterNames.Length && index<arguments.Length; index++) {
            if(string.IsNullOrWhiteSpace(parameterNames[index])) {
                continue;
            }

            rewritten=ReplaceParameterReference(rewritten,parameterNames[index],FormatInlineArgument(arguments[index]));
        }

        return rewritten;
    }

    private static string FormatInlineArgument(string argument) =>
        IsSimpleExpression(argument) ? argument : $"({argument})";

    public virtual bool IsIdentity => false;

    public bool TryGetSmallIntegralRange(int maxCount,out int min,out int max) {
        if(IntegralRangeMin is { } explicitMin && IntegralRangeMax is { } explicitMax) {
            if(IsSmallRange(explicitMin,explicitMax,maxCount)) {
                min=explicitMin;
                max=explicitMax;
                return true;
            }
            min=0;
            max=0;
            return false;
        }

        if(!string.IsNullOrWhiteSpace(InlineBody)) {
            var expression=SyntaxFactory.ParseExpression(InlineBody);
            if(CardinalityAnalysis.TryGetIntegralRange(expression,null) is { } parsedRange
                && IsSmallRange(parsedRange.Min,parsedRange.Max,maxCount)) {
                min=parsedRange.Min;
                max=parsedRange.Max;
                return true;
            }
        }

        min=0;
        max=0;
        return false;
    }

    private static bool CanOmitOuterParentheses(string expression) {
        var parsed = SyntaxFactory.ParseExpression(expression);
        return parsed is not AssignmentExpressionSyntax
            and not LambdaExpressionSyntax
            and not AnonymousMethodExpressionSyntax
            and not QueryExpressionSyntax;
    }

    private static bool IsSmallRange(int min,int max,int maxCount) =>
        min<=max && (long)max-min+1<=maxCount;

    private static string ReplaceParameterReference(string text,string parameterName,string replacement) =>
        Regex.Replace(
            text,
            $@"\b{Regex.Escape(parameterName)}\b(?!\s*:)",
            replacement,
            RegexOptions.CultureInvariant);

    protected static string SanitizeLocalName(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.Length==0 ? "value" : builder.ToString();
    }
}
