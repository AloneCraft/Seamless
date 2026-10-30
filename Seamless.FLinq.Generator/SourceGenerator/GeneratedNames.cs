using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

/// <summary>
/// 生成コードの変数名・フィールド名・引数名を割り当てる。
/// 人が書いたコードのように読めるよう、まず「自然な名前」(item・key・count・利用者のラムダの引数名など)を試し、
/// 利用者のコードの識別子や、すでに割り当てた名前とぶつかるときだけ <c>名前2</c>・<c>名前3</c> のように番号を足す。
/// <para>
/// 衝突の相手は2種類ある。
/// <list type="bullet">
///   <item><description>メソッド全体の範囲(<see cref="Allocate"/>): メソッド直下に置く名前(結果の変数・巻き上げた値など)。利用者のメソッドに現れる全ての識別子
///   (ローカル・引数・ラムダの引数・単純名で参照するフィールドやプロパティ)と衝突させない。メソッド直下のローカルは、あとの文のラムダの引数と同じ名前だと CS0136 になるため。</description></item>
///   <item><description>ループなど入れ子のブロックの範囲(<see cref="AllocateLocal"/>): ブロックの中だけの名前(ラムダの引数の別名・ループの変数など)。
///   ラムダの中だけで宣言される名前(ラムダの引数など)とは衝突しないので、ラムダ自身の引数名(p・row)をそのまま使える。
///   ただし、単純名で参照されているメンバーを隠さないよう、参照される名前とは衝突させない。</description></item>
/// </list>
/// </para>
/// 生成するローカルが、利用者のコードが単純名で参照しているメンバーを隠すと、意味が黙って変わってしまうため、宣言だけでなく参照も数える。
/// </summary>
internal sealed class GeneratedNames(HashSet<string> methodScope,HashSet<string> blockScope) {
    [ThreadStatic]
    private static GeneratedNames? current;

    private readonly HashSet<string> methodScope = methodScope;
    private readonly HashSet<string> blockScope = blockScope;
    private readonly Dictionary<string,string> stable = new(StringComparer.Ordinal);

    /// <summary>いま生成しているメソッドの割り当て役。メソッドの生成の外では null(その場合、呼び出し側は従来の機械的な名前に戻る)。</summary>
    internal static GeneratedNames? Current => current;

    /// <summary>メソッドの生成の間だけ、割り当て役を有効にする。入れ子にしても、抜けると元に戻る。</summary>
    internal static IDisposable Begin(GenerationState state) {
        var previous = current;
        current=new GeneratedNames(state.UsedIdentifiers,state.BlockScopeIdentifiers);
        return new Scope(previous);
    }

    private sealed class Scope(GeneratedNames? previous):IDisposable {
        public void Dispose() => current=previous;
    }

    /// <summary>
    /// 断片のテンプレートが、固定の名前のローカルとして書いているもの(Sum の sum、走査の value・index など)。
    /// 自然な名前を割り当てるとき、これらとぶつかると CS0128 / CS0136 になる(結果の変数が sum だと、Sum の展開の中の sum と衝突する)。
    /// テンプレートの固定の名前を全部は数えきれないので、よく使われるものを先に使用済みにして避ける。
    /// </summary>
    private static readonly string[] TemplateNames = [
        "sum","count","list","array","set","dictionary","lookup","min","max","first","last","found","flag","result","results","current","source","second",
        "span","index","value","values","item","items","key","keys","temp","buffer","length","start","end","comparer","destination","offset","default",
        "i","j","k","n","v","e","c","x","s","t","a","b","left","right","next","previous","hasValue","state","enumerator","consumer","total","acc","seed",
    ];

    /// <summary>テンプレートが使う固定の名前を、使用済みにする(割り当てた名前と衝突させないため)。</summary>
    internal static void ReserveTemplateNames(HashSet<string> methodScope,HashSet<string> blockScope) {
        foreach(var name in TemplateNames) {
            methodScope.Add(name);
            blockScope.Add(name);
        }
    }

    /// <summary>メソッド直下の名前。使用済みでない名前を新しく割り当てる。空いていればそのまま、使用済みなら 2・3… を足す。</summary>
    public string Allocate(string preferred) => this.AllocateIn(this.methodScope,preferred);

    /// <summary>ブロックの中だけの名前。ラムダの中だけで宣言される名前とは衝突しない。</summary>
    public string AllocateLocal(string preferred) => this.AllocateIn(this.blockScope,preferred);

    /// <summary>
    /// 同じ <paramref name="key"/> には何度尋ねても同じ名前を返す。断片の描画は同じ名前を何度も参照する(列挙子の中・ループの中など)ため、
    /// 最初の呼び出しで割り当てた名前を覚えておく。
    /// </summary>
    public string StableLocal(string key,string preferred) {
        if(this.stable.TryGetValue(key,out var existing)) {
            return existing;
        }
        var name = this.AllocateLocal(preferred);
        this.stable.Add(key,name);
        return name;
    }

    private string AllocateIn(HashSet<string> scope,string preferred) {
        var baseName = ToIdentifier(preferred);
        for(var number = 1;;number++) {
            var candidate = number==1 ? baseName : baseName+number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(scope.Contains(candidate)) {
                continue;
            }
            // 割り当てた名前は、どちらの範囲でも使用済みにする(あとで同じ名前を別の用途に割り当てない)。
            this.methodScope.Add(candidate);
            this.blockScope.Add(candidate);
            return candidate;
        }
    }

    /// <summary>識別子として使える形にする。キーワードや不正な文字を含む名前は、使える名前に直す。</summary>
    internal static string ToIdentifier(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return "value";
        }
        var builder = new System.Text.StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(char.IsLetterOrDigit(c)||c=='_' ? c : '_');
        }
        if(builder.Length==0||char.IsDigit(builder[0])) {
            builder.Insert(0,'_');
        }
        var text = builder.ToString();
        return SyntaxFacts.GetKeywordKind(text)!=SyntaxKind.None||IsReservedContextual(text)
            ? text+"Value"
            : text;
    }

    // 文脈キーワードのうち、ローカル名として使うと紛らわしいもの(var・dynamic など)。
    private static bool IsReservedContextual(string text) =>
        text is "var" or "dynamic" or "nameof" or "async" or "await" or "yield" or "when" or "value";

    /// <summary>
    /// 利用者のメソッドの識別子を集める。<paramref name="methodScope"/> は、メソッド全体で数える名前(ラムダの中・宣言・参照の全て)。
    /// <paramref name="blockScope"/> は、ブロックの中だけの名前の衝突相手で、ラムダの中だけで宣言される名前(ラムダの引数・ラムダの中のローカル)を除く。
    /// 参照(単純名)は、宣言されていなければ(フィールド・プロパティなど)どちらにも入れる。
    /// </summary>
    internal static void CollectUserIdentifiers(SyntaxNode root,IEnumerable<string> parameterNames,HashSet<string> methodScope,HashSet<string> blockScope) {
        var methodParameters = parameterNames.ToArray();
        var outsideDeclared = new HashSet<string>(methodParameters,StringComparer.Ordinal);
        var insideDeclared = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach(var name in methodParameters) {
            methodScope.Add(name);
        }
        foreach(var token in root.DescendantTokens()) {
            if(!token.IsKind(SyntaxKind.IdentifierToken)) {
                continue;
            }
            var text = token.ValueText;
            methodScope.Add(text);
            if(IsDeclaration(token)) {
                (IsInsideLambda(token.Parent) ? insideDeclared : outsideDeclared).Add(text);
            } else {
                referenced.Add(text);
            }
        }
        foreach(var name in outsideDeclared) {
            blockScope.Add(name);
        }
        foreach(var name in referenced) {
            if(!insideDeclared.Contains(name)) {
                blockScope.Add(name);
            }
        }
    }

    private static bool IsDeclaration(SyntaxToken token) =>
        token.Parent switch {
            VariableDeclaratorSyntax declarator => declarator.Identifier==token,
            ParameterSyntax parameter => parameter.Identifier==token,
            ForEachStatementSyntax forEach => forEach.Identifier==token,
            CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier==token,
            SingleVariableDesignationSyntax designation => designation.Identifier==token,
            LocalFunctionStatementSyntax localFunction => localFunction.Identifier==token,
            TypeParameterSyntax typeParameter => typeParameter.Identifier==token,
            _ => false,
        };

    private static bool IsInsideLambda(SyntaxNode? node) {
        for(var current = node;current is not null;current=current.Parent) {
            if(current is AnonymousFunctionExpressionSyntax) {
                return true;
            }
        }
        return false;
    }
}
