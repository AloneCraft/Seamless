using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class FuncFragment {
    // helper method 定義の `=> ...;` 末尾部分。
    public string DefineLambda=string.Empty;
    // delegate 戻り値型名。
    public string ReturnTypeName="void";
    // delegate 引数型名一覧。
    public string[] ParameterTypeNames=Array.Empty<string>();
    // delegate 引数名一覧。
    public string[] ParameterNames=Array.Empty<string>();
    // 対応する Func<...> の型。
    public Type Type;
    // helper struct 内へ static method を生成できるかどうか。
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
    public (string Name, string TypeName)[] CapturedLocals = Array.Empty<(string Name, string TypeName)>();
    // ラムダ本体内のフィールド・static メソッドアクセス用に生成した UnsafeAccessor extern 宣言コード。
    // Fragment.CollectAllUnsafeAccessorCodes() で収集して補助クラスに出力される。
    public string[] BodyUnsafeAccessorCodes = Array.Empty<string>();
    // ラムダ本体を展開するために必要な補助 struct 群。
    // 例: keySelector 内の Count()/Any() を scalar Fragment として展開する場合。
    public string SupportingMemberSources = string.Empty;
    // 式位置ではなく文として展開する必要があるラムダかどうか。
    // LINQ scalar を内包するラムダは enumerator 生成・TryGetNext 呼び出しが必要になる。
    public bool RequiresStatementInlining;
    // reflection 用 Type から global:: fully-qualified name を作る。
    public static string MetadataTypeName(Type type) {
        var fullName = type.FullName ?? type.Name;
        return "global::"+fullName.Replace("+",".");
    }
    // helper struct に置く `public static TResult selector(T arg)` 本体を組み立てる。
    public string DefineMethod(string methodName) {
        var invoke = this.Type.GetMethod("Invoke");
        var parameters = invoke?.GetParameters() ?? Array.Empty<ParameterInfo>();
        var returnType = !string.IsNullOrEmpty(this.ReturnTypeName)
            ? this.ReturnTypeName
            : invoke?.ReturnType.FullName ?? "void";
        var parametersList = new System.Text.StringBuilder();
        var count = Math.Max(this.ParameterTypeNames.Length,parameters.Length);
        for(var index = 0; index<count; index++) {
            if(index>0) {
                parametersList.Append(", ");
            }
            var paramType = index<this.ParameterTypeNames.Length
                ? this.ParameterTypeNames[index]
                : MetadataTypeName(parameters[index].ParameterType);
            var paramName = index<this.ParameterNames.Length&&!string.IsNullOrEmpty(this.ParameterNames[index])
                ? this.ParameterNames[index]
                : parameters.Length>index&&parameters[index].Name is not null ? parameters[index].Name! : $"arg{index}";
            parametersList.Append($"{paramType} {paramName}");
        }

        return $"public static {returnType} {methodName}({parametersList}){this.DefineLambda}";
    }

    // helper struct を作れないラムダでも、生成メソッド本文の中なら delegate 式としてそのまま書ける。
    public string RenderDelegateExpression() {
        var parameterList = string.Join(
            ", ",
            global::System.Linq.Enumerable.Range(0,this.ParameterNames.Length).Select(index => $"{this.ParameterTypeNames[index]} {this.ParameterNames[index]}"));
        var lambdaSuffix = this.DefineLambda.TrimEnd().TrimEnd(';');
        return $"({parameterList}){lambdaSuffix}";
    }

    // 代入文として inline 展開する具体実装を派生型に任せる。
    public virtual string RenderInlineAssign(string destination,int number,params string[] arguments) {
        throw new InvalidOperationException("Inline assign is not supported.");
    }

    // 条件式や Add 引数など、式位置へ inline 展開する具体実装を派生型に任せる。
    public virtual string RenderInlineExpression(params string[] arguments) {
        throw new InvalidOperationException("Inline expression is not supported.");
    }

    // 代入文向け inline 展開の共通処理。
    protected string RenderInlineAssignCore(string destination,int number,params string[] arguments) {
        var builder = new StringBuilder();
        var rewrittenBody=this.InlineBody;
        for(var index = 0; index<arguments.Length; index++) {
            var tempName=$"__arg{number}_{SanitizeLocalName(destination)}_{index}";
            // 代入文に展開するときは、引数式の多重評価を避けるために一度ローカルへ退避する。
            // 例えば selector(GetValue()) をそのまま展開すると GetValue() が複数回評価されうる。
            builder.AppendLine($"var {tempName}={arguments[index]};");
            if(index<this.ParameterNames.Length && !string.IsNullOrWhiteSpace(this.ParameterNames[index])) {
                rewrittenBody=Regex.Replace(
                    rewrittenBody,
                    $@"\b{Regex.Escape(this.ParameterNames[index])}\b",
                    tempName,
                    RegexOptions.CultureInvariant);
            }
        }

        builder.Append($"{destination}={rewrittenBody};");
        return builder.ToString();
    }

    // 式位置向け inline 展開の共通処理。
    protected string RenderInlineExpressionCore(params string[] arguments) {
        var rewrittenBody=this.InlineBody;
        for(var index = 0; index<arguments.Length; index++) {
            if(index<this.ParameterNames.Length && !string.IsNullOrWhiteSpace(this.ParameterNames[index])) {
                // 条件式や Add 引数のような「式の中」に埋め込む場合は、
                // helper メソッド呼び出し `StructX_selector.selector(value)` を残さず、
                // ラムダ引数名を実引数へ直接置換して式そのものを作る。
                // こうしておくと生成コードを読んだときに元の LINQ の意味が追いやすく、
                // JIT にも普通の式として見せられる。
                rewrittenBody=Regex.Replace(
                    rewrittenBody,
                    $@"\b{Regex.Escape(this.ParameterNames[index])}\b",
                    $"({arguments[index]})",
                    RegexOptions.CultureInvariant);
            }
        }

        return $"({rewrittenBody})";
    }

    protected static string ReplaceParameterReferences(string text,string[] parameterNames,string[] arguments) {
        var rewritten = text;
        for(var index = 0; index<parameterNames.Length && index<arguments.Length; index++) {
            if(string.IsNullOrWhiteSpace(parameterNames[index])) {
                continue;
            }

            rewritten = Regex.Replace(
                rewritten,
                $@"\b{Regex.Escape(parameterNames[index])}\b",
                $"({arguments[index]})",
                RegexOptions.CultureInvariant);
        }

        return rewritten;
    }

    protected static string SanitizeLocalName(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.Length==0 ? "value" : builder.ToString();
    }
}
