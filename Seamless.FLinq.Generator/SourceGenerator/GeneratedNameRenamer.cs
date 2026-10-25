using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

/// <summary>
/// 生成したコードの中の、機械的な名前(<c>__it2_1</c>・<c>__pipelineItem__r3_0</c> など)を、人が書いたような名前(<c>item</c>・<c>pipelineItem</c>)に付け替える。
/// <para>
/// 約1000か所の文字列に埋め込まれた名前を1つずつ直すより、1つのメソッドの生成が終わった後で、
/// 「このメソッドの中で宣言された <c>__</c> で始まる名前」だけを、全体で一貫して付け替えるほうが安全なので、この形にしている。
/// </para>
/// 付け替える対象は、ローカル変数・フィールド・引数・ラベル(宣言が生成コードの中にあるもの)だけ。
/// 他の場所で宣言される名前(UnsafeAccessor の <c>__Method_…</c> などのメソッド名)は、宣言の種類が違うので触らない。
/// 新しい名前は、役割(元の名前から番号の部分を除いたもの)に由来し、他のどの識別子とも重ならないように、必要なときだけ番号を足す。
/// 重ならない相手は、付け替えない全ての識別子(利用者のコード・メンバー名・API 名を含む)なので、元のコードの名前の見え方(隠蔽)は変わらない。
/// </summary>
internal static class GeneratedNameRenamer {
    // 元の名前が短い記号のものは、役割が分かる名前にする(元の名前から番号を除いた形で照合する)。
    private static readonly Dictionary<string,string> Roles = new(StringComparer.Ordinal) {
        ["e"] = "enumerator",
        ["it"] = "item",
        ["tmp"] = "temp",
        ["r"] = "result",
        ["lk"] = "lookup",
        ["k"] = "key",
        ["g"] = "grouping",
        ["hv"] = "hasValue",
        ["ord"] = "ordered",
        ["st"] = "state",
        ["yd"] = "yielded",
        ["gi"] = "groupIndex",
        ["ov"] = "outer",
        ["cnt"] = "count",
        ["ex"] = "existing",
        ["ie"] = "innerEnumerator",
        ["a"] = "index",
        ["idx"] = "index",
        ["cmp"] = "comparer",
        ["ordcmp"] = "orderComparer",
        ["smr"] = "selectManyResult",
        ["acc"] = "aggregate",
        ["resItem"] = "groupResult",
        ["hasCnt"] = "hasCount",
        ["kv"] = "entry",
    };

    // ローカル名にすると紛らわしい・使えない名前(クエリ式のキーワードなど)。
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) {
        "group","from","where","select","into","orderby","join","let","on","equals","by","ascending","descending",
        "field","record","required","scoped","file","init","managed","unmanaged","notnull","args","global","partial","get","set","add","remove",
    };

    /// <summary>
    /// 補助の構造体の群(<paramref name="helpers"/>)と、メソッドの本体(<paramref name="body"/>)の中の機械的な名前を付け替える。
    /// <paramref name="userIdentifiers"/> に含まれる名前は利用者のコードの名前なので付け替えない。
    /// </summary>
    internal static void Rename(ref string helpers,ref string body,HashSet<string> userIdentifiers,IReadOnlyDictionary<string,string>? preferredLocals=null) {
        var helperTokens = Tokenize(helpers,0);
        var bodyTokens = Tokenize(body,int.MaxValue/2);

        // 宣言が生成コードの中にある `__` で始まる名前を、出てきた順に集める。
        var declared = new List<string>();
        var declaredSet = new HashSet<string>(StringComparer.Ordinal);
        Collect(helperTokens,declared,declaredSet,userIdentifiers);
        Collect(bodyTokens,declared,declaredSet,userIdentifiers);
        if(declared.Count==0) {
            return;
        }

        // 付け替えない識別子は全て、新しい名前の衝突相手になる。
        var forbidden = new HashSet<string>(userIdentifiers,StringComparer.Ordinal);
        // 名前ごとに、出てくる範囲(型の宣言、または本体)を数える。
        var unitsOf = new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
        foreach(var entry in helperTokens.Concat(bodyTokens)) {
            if(!entry.Token.IsKind(SyntaxKind.IdentifierToken)) {
                continue;
            }
            var text = entry.Token.ValueText;
            if(!declaredSet.Contains(text)) {
                forbidden.Add(text);
                continue;
            }
            if(!unitsOf.TryGetValue(text,out var units)) {
                units=[];
                unitsOf[text]=units;
            }
            units.Add(entry.Unit);
        }

        // 元のラムダ名が展開後に消えている場合だけ再利用できる。
        // 実際に残っている識別子は、宣言だけでなく参照も含めて保護する。
        var preserved=new HashSet<string>(helperTokens.Concat(bodyTokens)
            .Where(entry=>entry.Token.IsKind(SyntaxKind.IdentifierToken)&&!declaredSet.Contains(entry.Token.ValueText))
            .Select(entry=>entry.Token.ValueText),StringComparer.Ordinal);
        if(preferredLocals is not null){
            foreach(var preferred in preferredLocals.Values){
                if(!preserved.Contains(preferred)){
                    forbidden.Remove(preferred);
                }
            }
        }
        string PreferredRole(string name){
            if(preferredLocals is not null&&preferredLocals.TryGetValue(name,out var preferred)&&!preserved.Contains(preferred)){
                return GeneratedNames.ToIdentifier(preferred);
            }
            return RoleOf(name);
        }

        // 複数の範囲に出る名前(範囲をまたいで参照される)は、全体で1つの名前にする。
        // 1つの範囲(構造体の中など)だけに出る名前は、範囲ごとに独立に付ける(別の構造体で item を使い回せるので、番号が増えすぎない)。
        var shared = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var name in declared) {
            if(unitsOf[name].Count>1) {
                shared[name]=Allocate(PreferredRole(name),forbidden);
            }
        }
        var local = new Dictionary<(int Unit,string Name),string>();
        var takenInUnit = new Dictionary<int,HashSet<string>>();
        foreach(var entry in helperTokens.Concat(bodyTokens)) {
            if(!entry.Token.IsKind(SyntaxKind.IdentifierToken)) {
                continue;
            }
            var text = entry.Token.ValueText;
            if(!declaredSet.Contains(text)||shared.ContainsKey(text)||local.ContainsKey((entry.Unit,text))) {
                continue;
            }
            if(!takenInUnit.TryGetValue(entry.Unit,out var taken)) {
                taken=new HashSet<string>(StringComparer.Ordinal);
                takenInUnit[entry.Unit]=taken;
            }
            var role = PreferredRole(text);
            var candidate = role;
            for(var number = 2;forbidden.Contains(candidate)||taken.Contains(candidate);number++) {
                candidate=role+number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            taken.Add(candidate);
            local[(entry.Unit,text)]=candidate;
        }

        helpers=Rewrite(helperTokens,shared,local);
        body=Rewrite(bodyTokens,shared,local);
    }

    private static string Allocate(string role,HashSet<string> forbidden) {
        var candidate = role;
        for(var number = 2;forbidden.Contains(candidate);number++) {
            candidate=role+number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        forbidden.Add(candidate);
        return candidate;
    }

    private readonly record struct Entry(SyntaxToken Token,int Unit);

    private static List<Entry> Tokenize(string text,int unitBase) {
        var tree = CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(LanguageVersion.Latest));
        return tree.GetRoot().DescendantTokens(descendIntoTrivia:false)
            .Select(token => new Entry(token,UnitOf(token,unitBase)))
            .ToList();
    }

    // 範囲は、いちばん内側の型の宣言(構造体など)。型の外(メソッドの本体)は1つの範囲にする。
    private static int UnitOf(SyntaxToken token,int unitBase) {
        for(var node = token.Parent;node is not null;node=node.Parent) {
            if(node is TypeDeclarationSyntax type&&token.SpanStart>=type.OpenBraceToken.SpanStart) {
                return unitBase+type.SpanStart+1;
            }
        }
        return unitBase;
    }

    private static void Collect(List<Entry> tokens,List<string> declared,HashSet<string> declaredSet,HashSet<string> userIdentifiers) {
        foreach(var entry in tokens) {
            var token = entry.Token;
            if(!token.IsKind(SyntaxKind.IdentifierToken)) {
                continue;
            }
            var text = token.ValueText;
            if(!text.StartsWith("__",StringComparison.Ordinal)||text.Length<=2||token.Text.Length>0&&token.Text[0]=='@') {
                continue;
            }
            if(userIdentifiers.Contains(text)||declaredSet.Contains(text)||IsContractName(text)||!IsRenamableDeclaration(token)) {
                continue;
            }
            declaredSet.Add(text);
            declared.Add(text);
        }
    }

    // 名前そのものが、生成コードの種類を表す目印になっているもの(Tsavorite / Persisted のネイティブ走査の状態など)は変えない。
    private static bool IsContractName(string text) =>
        text.StartsWith("__Tsavorite",StringComparison.Ordinal)||text.StartsWith("__Persisted",StringComparison.Ordinal);

    // メソッド名・型名・プロパティ名などは、他の場所から参照されるので対象にしない。
    private static bool IsRenamableDeclaration(SyntaxToken token) =>
        token.Parent switch {
            VariableDeclaratorSyntax declarator => declarator.Identifier==token,
            ParameterSyntax parameter => parameter.Identifier==token,
            ForEachStatementSyntax forEach => forEach.Identifier==token,
            CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier==token,
            SingleVariableDesignationSyntax designation => designation.Identifier==token,
            LabeledStatementSyntax labeled => labeled.Identifier==token,
            LocalFunctionStatementSyntax function => function.Identifier==token,
            _ => false,
        };

    private static string Rewrite(List<Entry> tokens,Dictionary<string,string> shared,Dictionary<(int Unit,string Name),string> local) {
        var builder = new StringBuilder();
        foreach(var entry in tokens) {
            var token = entry.Token;
            builder.Append(token.LeadingTrivia.ToFullString());
            var plain = token.Text.Length==0||token.Text[0]!='@';
            if(plain&&token.IsKind(SyntaxKind.IdentifierToken)
                &&(shared.TryGetValue(token.ValueText,out var replacement)||local.TryGetValue((entry.Unit,token.ValueText),out replacement))) {
                builder.Append(replacement);
            } else {
                builder.Append(token.Text);
            }
            builder.Append(token.TrailingTrivia.ToFullString());
        }
        return builder.ToString();
    }

    /// <summary>元の名前から、番号と区切り(<c>__r2_0</c> など)を除いた役割の名前を作る。</summary>
    internal static string RoleOf(string name) {
        var text = name.TrimStart('_');
        var separator = text.IndexOf("__",StringComparison.Ordinal);
        if(separator>=0) {
            text=text.Substring(0,separator);
        }
        text=Regex.Replace(text,@"[_\d]+$",string.Empty);
        if(text.Length==0) {
            return "value";
        }
        if(Roles.TryGetValue(text,out var role)) {
            text=role;
        }
        if(char.IsUpper(text[0])) {
            text=char.ToLowerInvariant(text[0])+text.Substring(1);
        }
        var identifier = GeneratedNames.ToIdentifier(text);
        return Reserved.Contains(identifier) ? identifier+"Value" : identifier;
    }
}
