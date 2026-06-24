using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Seamless.SourceGenerator.GeneratorHelpers;

using Seamless.FLinq.Linq.Fragments;
using Enumerable = Seamless.FLinq.Linq.Fragments.Enumerable;

namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {

    /// <summary>
    /// 2 つの補助静的メソッド文字列を改行で連結する。空文字列・空白のみの文字列は無視する。
    /// Fragment 構築中に積み上がる <c>staticMethods</c> を逐次マージするために使う。
    /// </summary>
    private static string 静的メソッドコード連結(string left,string right) {
        return string.Join("\n",new[] { left,right }.Where(static text=>!string.IsNullOrWhiteSpace(text)));
    }
    private static string OpenGenericTypeName(INamedTypeSymbol namedType) {
        var definitionName = namedType.OriginalDefinition.ToDisplayString(完全修飾形式);
        var typeArgumentStart = definitionName.IndexOf('<',StringComparison.Ordinal);
        return typeArgumentStart<0 ? definitionName : definitionName[..typeArgumentStart];
    }
    /// <summary>
    /// 変数宣言部（<c>T x = expr</c>）のテキストを生成する。
    /// <see cref="ローカル変数宣言生成"/> と同様だが、文末セミコロンを付けない点が異なる。
    /// <c>using (…)</c> や <c>for (…; …; …)</c> の初期化部で使う。
    /// </summary>
    private static string 変数宣言生成(
        VariableDeclarationSyntax declaration,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var variables = declaration.Variables.Select(variable=>{
            if(variable.Initializer?.Value is null) {
                return variable.ToString();
            }
            return $"{variable.Identifier.ValueText}={RenderExpressionForGeneratedMethod(variable.Initializer.Value,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
        });
        string typeName;
        if(declaration.Type is IdentifierNameSyntax { IsVar: true }) {
            typeName="var";
        } else {
            var typeSymbol=semanticModel.GetTypeInfo(declaration.Type).Type;
            typeName=typeSymbol is not null
                ? typeSymbol.ToDisplayString(完全修飾形式)
                : NormalizeExpressionText(declaration.Type.ToString());
        }
        return $"{typeName} {string.Join(", ",variables)}";
    }
    /// <summary>
    /// 文のテキストを <paramref name="replacementMap"/>（LINQ 式テキスト → 一時変数名）で置換し、
    /// <c>SystemEnumerable.</c> を <c>global::System.Linq.Enumerable.</c> に、
    /// <c>this.</c> を <c>@this.</c> に変換して返す。
    /// キーは長い順に置換することで部分一致による誤置換を防ぐ。
    /// </summary>
    private static string RenderStatementForGeneratedMethod(
        StatementSyntax statement,
        IReadOnlyDictionary<string,string>? replacementMap) {
        // replacementMap のキーは NormalizeExpressionText 済みなので text も正規化して照合する。
        var text = NormalizeExpressionText(statement.ToString());
        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
                text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            }
        }
        return text
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);
    }
    private static string RenderStatementForGeneratedMethod(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var text = NormalizeGeneratedExpressionText(statement.ToString());
        var processedExpressions = new HashSet<string>(StringComparer.Ordinal);
        foreach(var expression in OutermostStatementExpressions(statement)
            .OrderByDescending(static expression=>NormalizeGeneratedExpressionText(expression.ToString()).Length)) {
            if(!ReferenceEquals(expression.SyntaxTree,semanticModel.SyntaxTree)) {
                continue;
            }

            var expressionText = NormalizeGeneratedExpressionText(expression.ToString());
            if(!processedExpressions.Add(expressionText)) {
                continue;
            }

            var renderedExpression = RenderExpressionForGeneratedMethod(
                expression,
                semanticModel,
                containingType,
                unsafeAccessorCollector,
                replacementMap);
            text=ReplaceExpressionFragment(text,expressionText,renderedExpression);
        }

        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
                text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            }
        }

        return text
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);
    }

    private static IEnumerable<ExpressionSyntax> OutermostStatementExpressions(StatementSyntax statement) {
        foreach(var expression in statement.DescendantNodes().OfType<ExpressionSyntax>()) {
            if(expression is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax
                || IsInsideNestedFunction(expression,statement)
                || HasExpressionAncestorBefore(expression,statement)) {
                continue;
            }

            yield return expression;
        }
    }

    private static bool HasExpressionAncestorBefore(SyntaxNode node,SyntaxNode root) {
        for(var current = node.Parent; current is not null && !ReferenceEquals(current,root); current=current.Parent) {
            if(current is ExpressionSyntax) {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideNestedFunction(SyntaxNode node,SyntaxNode root) {
        for(var current = node.Parent; current is not null && !ReferenceEquals(current,root); current=current.Parent) {
            if(current is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax or LocalFunctionStatementSyntax) {
                return true;
            }
        }

        return false;
    }
    /// <summary>Fragment 識別番号をメソッドごとに 0 からリセットする。</summary>
    private static int フラグメント番号初期値取得()=>0;

    /// <summary>
    /// <c>Struct0</c>, <c>Struct1</c> のような補助型をメソッド単位の補助クラス
    /// <c>{methodName}_補助0_Struct0</c> 等にリネームし、クラス直下での名前衝突を回避する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>補助メソッド群が空なら何もしない。</description></item>
    ///   <item><description>正規表現 <c>\bStruct\d+(_suffix)?\b</c> で <c>staticMethods</c> と <c>body</c> の両方を一括置換する。</description></item>
    ///   <item><description><see cref="補助Struct関数へAggressiveInlining付与"/> で <c>Compute</c>/<c>Build</c> メソッドに <c>AggressiveInlining</c> を付与する。</description></item>
    /// </list>
    /// </summary>
    private static void 補助メンバーをメソッド専用クラスへ退避(string methodName,ref string staticMethods,ref string body) {
        if(string.IsNullOrWhiteSpace(staticMethods)) {
            return;
        }
        var prefix = $"{SanitizeIdentifier(methodName)}_補助0_";
        const string pattern = @"\bStruct(?<番号>\d+)(?<接尾辞>_[A-Za-z_][A-Za-z0-9_]*)?\b";
        staticMethods = System.Text.RegularExpressions.Regex.Replace(
            staticMethods,
            pattern,
            $"{prefix}Struct${{番号}}${{接尾辞}}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        body = System.Text.RegularExpressions.Regex.Replace(
            body,
            pattern,
            $"{prefix}Struct${{番号}}${{接尾辞}}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        staticMethods=補助Struct関数へAggressiveInlining付与(staticMethods);
    }
    /// <summary>
    /// 生成済み補助コード内の <c>private static … Compute(…)</c> / <c>private static … Build(…)</c>
    /// メソッドに <c>[MethodImpl(AggressiveInlining)]</c> 属性が付いていなければ挿入する。
    /// 正規表現で前行を確認し、既に属性がある場合は重複を避ける。
    /// </summary>
    private static string 補助Struct関数へAggressiveInlining付与(string source) {
        const string attribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        return System.Text.RegularExpressions.Regex.Replace(
            source,
            @"(?m)^(?<indent>\s*)private static (?<declaration>.+\b(?:Compute|Build)\s*\()",
            match=>{
                var index = match.Index;
                var previousLineEnd = index>1 ? source.LastIndexOf('\n',index-2) : -1;
                var previousLine = source.Substring(previousLineEnd+1,index-(previousLineEnd+1)).Trim();
                if(previousLine==attribute) {
                    return match.Value;
                }
                return $"{match.Groups["indent"].Value}{attribute}\n{match.Value}";
            },
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
    // インターセプト対象メソッドで宣言・使用されているすべての識別子名を収集する。
    // 生成コードの変数名がユーザーコードと衝突しないようにするための前処理。
    // ラムダ/匿名メソッドの内部はラムダスコープであるため再帰を止め収集しない。
    private static HashSet<string> CollectMethodBodyIdentifiers(MethodDeclarationSyntax methodSyntax, IMethodSymbol method) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in method.Parameters)
            names.Add(p.Name);
        if(methodSyntax.Body is not null)
            CollectMethodScopeIdentifiers(methodSyntax.Body, names);
        return names;
    }
    /// <summary>
    /// ノード以下の識別子をすべて <paramref name="names"/> に収集する再帰ヘルパー。
    /// ラムダ／匿名メソッドに入ったら再帰を止める（別スコープなので生成変数の衝突に使わない）。
    /// 対象: 変数宣言子・foreach 変数・catch 宣言・パターンの単変数指定。
    /// </summary>
    private static void CollectMethodScopeIdentifiers(SyntaxNode node, HashSet<string> names) {
        // ラムダ/匿名メソッドは別スコープなので子孫まで降りない
        if(node is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax) return;
        switch(node) {
            case VariableDeclaratorSyntax v:
                names.Add(v.Identifier.ValueText); break;
            case ForEachStatementSyntax fe:
                names.Add(fe.Identifier.ValueText); break;
            case CatchDeclarationSyntax cd when cd.Identifier.ValueText is { Length: > 0 } cid:
                names.Add(cid); break;
            case SingleVariableDesignationSyntax svd:
                names.Add(svd.Identifier.ValueText); break;
        }
        foreach(var child in node.ChildNodes())
            CollectMethodScopeIdentifiers(child, names);
    }
    /// <summary>
    /// <paramref name="usedNames"/> に衝突しない一意な識別子を返し、使用済みセットに追加する。
    /// <br/>衝突した場合は <c>baseName_</c>、<c>baseName__</c>... と <c>_</c> を増やして試す。
    /// 選んだ名前を <paramref name="usedNames"/> に追加するため、同一パス内での二重取得を防ぐ。
    /// </summary>
    private static string GetUniqueIdentifier(string baseName, HashSet<string> usedNames) {
        if(usedNames.Add(baseName)) return baseName;
        var suffix = "_";
        while(true) {
            var candidate = $"{baseName}{suffix}";
            if(usedNames.Add(candidate)) return candidate;
            suffix += "_";
        }
    }

    /// <summary>
    /// 文字列を C# 識別子として使えるようにサニタイズする。
    /// 非英数字を <c>_</c> に置換し、先頭が識別子開始文字でない場合は先頭に <c>_</c> を挿入する。
    /// 空または空白のみの場合は <c>"Aggregate"</c> にフォールバックする。
    /// </summary>
    private static string SanitizeIdentifier(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return "Aggregate";
        }

        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            //不正な文字列は_に置換する
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if(builder.Length==0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0])) {
            builder.Insert(0,'_');
        }

        return builder.ToString();
    }

    /// <summary>グローバル名前空間は空文字列、それ以外は表示文字列を返す。</summary>
    private static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    /// <summary>
    /// <paramref name="value"/> の各行先頭に <paramref name="indent"/> を付けて
    /// <paramref name="builder"/> に追記する。空行はインデントなしで出力する。
    /// CRLF・LF のどちらにも対応する。
    /// </summary>
    private static void インデント付き追記(StringBuilder builder,string value,string indent) {
        foreach(var line in value.Replace("\r\n","\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    /// <summary>
    /// 文字列から空白のみの行を除去して返す。
    /// データベーススキーマの生成ソース出力でコンパクト化のために使う。
    /// </summary>
    private static string 空行除去(string source) {
        var lines = source.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }

    /// <summary>改行を空白に置換してトリミングする。置換マップのキー正規化に使う。</summary>
    private static string NormalizeExpressionText(string value) {
        return value.Replace("\r\n"," ").Replace('\n',' ').Trim();
    }

    /// <summary>
    /// <see cref="NormalizeExpressionText"/> に加え、
    /// <c>SystemEnumerable.</c> → <c>global::System.Linq.Enumerable.</c> および <c>this.</c> → <c>@this.</c> の変換も行う。
    /// 生成コードへ埋め込む式テキストの標準化に使う。
    /// </summary>
    private static string NormalizeGeneratedExpressionText(string value) =>
        NormalizeExpressionText(value)
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);

    private static string RenderArgumentForGeneratedMethod(
        ArgumentSyntax argument,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var prefix = RefKind接頭辞取得(argument.RefKindKeyword.Kind());
        return $"{prefix}{RenderExpressionForGeneratedMethod(argument.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
    }

    private static string RenderTypeArgumentList(IMethodSymbol method) {
        if(method.TypeArguments.Length==0) {
            return string.Empty;
        }

        return $"<{string.Join(", ",method.TypeArguments.Select(static type=>type.ToDisplayString(完全修飾形式)))}>";
    }

    /// <summary>
    /// コンパイル時定数値を C# リテラルテキストに変換する。
    /// 浮動小数は <c>"R"</c> 書式でラウンドトリップ保証、文字列は逐語的文字列リテラル (<c>@"…"</c>) に変換する。
    /// </summary>
    private static string RenderConstantValue(object? value)=>value switch {
        null=>"null",
        string text=>"@\"" + text.Replace("\"","\"\"",StringComparison.Ordinal) + "\"",
        char ch=>"'" + ch.ToString().Replace("\\","\\\\",StringComparison.Ordinal).Replace("'","\\'",StringComparison.Ordinal) + "'",
        bool boolean=>boolean ? "true" : "false",
        float single=>single.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "f",
        double number=>number.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "d",
        decimal number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
        long number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
        ulong number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
        uint number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
        _=>Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture) ?? "default!",
    };

    /// <summary>
    /// <paramref name="type"/> 自身、またはその基底クラスチェーン上に <paramref name="target"/> が存在するかを判定する。
    /// 基底クラスで宣言された static const フィールドの参照をインライン展開するために使用する。
    /// </summary>
    private static bool IsSameOrBaseType(INamedTypeSymbol type,INamedTypeSymbol target) {
        for(var t=type;t is not null;t=t.BaseType) {
            if(SymbolEqualityComparer.Default.Equals(t,target)) return true;
        }
        return false;
    }

    private static string RefKind接頭辞取得(SyntaxKind syntaxKind)=>syntaxKind switch {
        SyntaxKind.RefKeyword=>"ref ",
        SyntaxKind.OutKeyword=>"out ",
        SyntaxKind.InKeyword=>"in ",
        _=>string.Empty,
    };

    /// <summary>
    /// 式の構文ノードを生成コードとして出力可能なテキストに変換する。
    /// <br/>アルゴリズム（ノード種別ごとの処理を 1 回の <c>DescendantNodesAndSelf()</c> 走査でまとめて行う）:
    /// <list type="number">
    ///   <item><description>テキストを <see cref="NormalizeGeneratedExpressionText"/> で正規化し、<paramref name="replacementMap"/> で LINQ 一時変数に置換する（長いキーを先に処理して部分一致誤置換を防ぐ）。</description></item>
    ///   <item><description><b>呼び出し式</b>: <c>nameof(…)</c> は評価済み文字列リテラルに変換。
    ///     非拡張メソッドは <see cref="TryRenderOrdinaryMethodInvocationExpression"/> でインライン化または UnsafeAccessor 化。
    ///     非 System.Linq 拡張メソッドは <see cref="TryRenderExtensionMethodInvocationExpression"/> でインライン化。
    ///     失敗した場合は <see cref="RenderInlineFailureInvocationExpression"/> で例外式に置換。</description></item>
    ///   <item><description><b>識別子</b>: 非 public 静的フィールドはコンスタントなら値展開し、それ以外は UnsafeAccessor 経由の参照に変換する。</description></item>
    ///   <item><description><b>メンバーアクセス（メソッドグループ）</b>: 非 public メソッドへのデリゲート参照を UnsafeAccessor ラムダに変換する。</description></item>
    ///   <item><description><b>オブジェクト生成</b>: 型名を完全修飾名に置換して生成 namespace での参照エラーを防ぐ。</description></item>
    ///   <item><description><b>メンバーアクセス（フィールド）</b>: 非 public フィールドを UnsafeAccessor 経由の参照に変換する。</description></item>
    ///   <item><description><b>メンバーアクセス（プロパティ）</b>: 非 public インスタンスプロパティの getter を <c>get_X</c> UnsafeAccessor 呼び出しに変換する。</description></item>
    ///   <item><description><b>パラメータ型</b>: ネストしたラムダのパラメータ型名を完全修飾名に置換する。</description></item>
    /// </list>
    /// </summary>
    private static string RenderExpressionForGeneratedMethod(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        if(expression is ThisExpressionSyntax) {
            return "@this";
        }
        if(expression is BaseExpressionSyntax) {
            return "@this";
        }

        var text = NormalizeGeneratedExpressionText(expression.ToString());

        text=ApplyExpressionReplacementMap(text,replacementMap);

        // 仕様メモ:
        // - 拡張メソッド呼び出し（ReducedFrom != null, Enumerable以外）は本文を式へインライン化する。
        // - 普通メソッド呼び出し（非拡張）はインライン化しない。生成コードから呼べない非 public は UnsafeAccessor で元呼び出しを維持する。
        // - インライン化対象を元呼び出しへ黙って戻す経路は作らず、未対応は生成コード上の例外式にする。
        // 最適化: DescendantNodesAndSelf() を 1 回だけ実行し、すべてのノード種別を一括収集する。
        var allNodes = expression.DescendantNodesAndSelf().ToList();
        var allInvocations = new List<InvocationExpressionSyntax>(allNodes.Count);
        var allIdentifiers = new List<IdentifierNameSyntax>(allNodes.Count);
        var allMemberAccesses = new List<MemberAccessExpressionSyntax>(allNodes.Count);
        var allObjectCreations = new List<ObjectCreationExpressionSyntax>(allNodes.Count);
        var allParameters = new List<ParameterSyntax>(allNodes.Count);
        foreach(var node in allNodes) {
            switch(node) {
                case InvocationExpressionSyntax inv: allInvocations.Add(inv); break;
                case IdentifierNameSyntax id: allIdentifiers.Add(id); break;
                case MemberAccessExpressionSyntax ma: allMemberAccesses.Add(ma); break;
                case ObjectCreationExpressionSyntax oc: allObjectCreations.Add(oc); break;
                case ParameterSyntax param: allParameters.Add(param); break;
            }
        }

        var processedInvocations = new HashSet<InvocationExpressionSyntax>(ReferenceEqualityComparer.Instance);
        var processedInvocationTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach(var invocation in allInvocations) {
            if(processedInvocations.Contains(invocation)) continue;
            if(IsExpressionReplaced(invocation,replacementMap)) {
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
                continue;
            }

            // nameof() は生成 namespace ではスコープ外になる識別子を参照しうるため、文字列リテラルへ評価する。
            if(invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }
                && invocation.ArgumentList.Arguments.Count==1) {
                var nameofArg = invocation.ArgumentList.Arguments[0].Expression;
                var nameofSymbol = semanticModel.GetSymbolInfo(nameofArg).Symbol;
                var nameString = nameofSymbol?.Name
                    ?? (nameofArg is MemberAccessExpressionSyntax maMember ? maMember.Name.Identifier.Text : nameofArg.ToString());
                var nameofText = NormalizeGeneratedExpressionText(invocation.ToString());
                text=text.Replace(nameofText,$"\"{nameString}\"",StringComparison.Ordinal);
                continue;
            }

            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || (method.ReducedFrom is null && method.MethodKind!=MethodKind.Ordinary)) {
                continue;
            }

            // text は先頭で this. → @this. 変換済みなので invocationText も同様に変換する。
            var invocationText = ApplyExpressionReplacementMap(
                NormalizeGeneratedExpressionText(invocation.ToString()),
                replacementMap);

            // 同じ invocationText を2回以上処理すると二重置換が発生するため、1回だけ処理する。
            if(!processedInvocationTexts.Add(invocationText)) {
                continue;
            }

            if(method.ReducedFrom is null) {
                if(TryRenderOrdinaryMethodInvocationExpression(method,invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var ordinaryInvocationText)) {
                text=ReplaceExpressionFragment(text,invocationText,ordinaryInvocationText);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
                continue;
            }

                text=ReplaceExpressionFragment(text,invocationText,RenderInlineFailureInvocationExpression(method));
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            } else if(method.ReducedFrom is not null
                && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() != "System.Linq.Enumerable"
                && invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess) {
                if(TryRenderExtensionMethodInvocationExpression(method,invocation,extensionMemberAccess,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var inlineExtensionText)) {
                    text=ReplaceExpressionFragment(text,invocationText,inlineExtensionText);
                } else {
                    text=ReplaceExpressionFragment(text,invocationText,RenderInlineFailureInvocationExpression(method));
                }
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            }
        }

        foreach(var identifier in allIdentifiers) {
            if(IsMemberAccessName(identifier) || IsInsideNameof(identifier)) {
                continue;
            }

            var identifierText = NormalizeExpressionText(identifier.ToString());
            if(semanticModel.GetSymbolInfo(identifier).Symbol is IFieldSymbol instanceField
                && !instanceField.IsStatic
                && IsSameOrBaseType(containingType,instanceField.ContainingType)) {
                var instanceMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(instanceField.Name));
                var instanceFieldExpression = CanAccessFromGeneratedCode(instanceField)
                    ? $"@this.{instanceField.Name}"
                    : RenderFieldForwardExpression(instanceField,instanceMemberAccess,semanticModel,unsafeAccessorCollector);
                text=ReplaceIdentifierReference(text,identifierText,instanceFieldExpression);
                continue;
            }

            if(semanticModel.GetSymbolInfo(identifier).Symbol is IPropertySymbol instanceProperty
                && !instanceProperty.IsStatic
                && instanceProperty.GetMethod is not null
                && IsSameOrBaseType(containingType,instanceProperty.ContainingType)) {
                var propertyExpression = CanAccessFromGeneratedCode(instanceProperty)
                    ? $"@this.{instanceProperty.Name}"
                    : RenderPropertyForwardExpression(instanceProperty,"@this",unsafeAccessorCollector);
                text=ReplaceIdentifierReference(text,identifierText,propertyExpression);
                continue;
            }

            if(identifier.Parent is ArgumentSyntax argument
                && argument.Expression==identifier
                && semanticModel.GetSymbolInfo(identifier).Symbol is IMethodSymbol methodGroup
                && methodGroup.MethodKind==MethodKind.Ordinary
                && SymbolEqualityComparer.Default.Equals(methodGroup.ContainingType,containingType)) {
                var methodGroupText=NormalizeExpressionText(identifier.ToString());
                string replacement;
                if(semanticModel.GetTypeInfo(identifier).ConvertedType is INamedTypeSymbol delegateType
                    && IsInlineDelegateType(delegateType)) {
                    replacement=RenderDelegateCreateExpression(
                        methodGroup,
                        delegateType,
                        methodGroup.IsStatic ? null : "@this",
                        unsafeAccessorCollector);
                } else {
                    replacement=$"@this.{methodGroup.Name}";
                    if(methodGroup.IsStatic) {
                        replacement=$"{methodGroup.ContainingType.ToDisplayString(完全修飾形式)}.{methodGroup.Name}";
                    }
                }
                text=System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(methodGroupText)}\b",
                    replacement,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                continue;
            }

            if(semanticModel.GetSymbolInfo(identifier).Symbol is not IFieldSymbol field
                || !field.IsStatic
                || !IsSameOrBaseType(containingType,field.ContainingType)
                || field.DeclaredAccessibility==Accessibility.Public) {
                continue;
            }

            if(field.HasConstantValue) {
                text=System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(identifierText)}\b",
                    RenderConstantValue(field.ConstantValue),
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                continue;
            }

            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(field.ContainingType.ToDisplayString(完全修飾形式)),
                SyntaxFactory.IdentifierName(field.Name));
            var fieldExpression = RenderFieldForwardExpression(field,memberAccess,semanticModel,unsafeAccessorCollector);
            text=System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(identifierText)}\b",
                fieldExpression,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IMethodSymbol method
                || method.MethodKind!=MethodKind.Ordinary
                || method.DeclaredAccessibility==Accessibility.Public
                || semanticModel.GetTypeInfo(memberAccess).ConvertedType is not INamedTypeSymbol delegateType
                || !IsInlineDelegateType(delegateType)) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var receiver = method.IsStatic
                ? null
                : NormalizeExpressionText(memberAccess.Expression.ToString()).Replace("this","@this",StringComparison.Ordinal);
            text=text.Replace(
                memberText,
                RenderDelegateCreateExpression(method,delegateType,receiver,unsafeAccessorCollector),
                StringComparison.Ordinal);
        }

        foreach(var objectCreation in allObjectCreations) {
            var typeSymbol = semanticModel.GetTypeInfo(objectCreation.Type).Type;
            if(typeSymbol is null) {
                continue;
            }

            var originalTypeText = NormalizeExpressionText(objectCreation.Type.ToString());
            var qualifiedTypeText = typeSymbol.ToDisplayString(完全修飾形式);
            if(originalTypeText==qualifiedTypeText || originalTypeText.StartsWith("global::",StringComparison.Ordinal)) {
                continue;
            }

            text=ReplaceTypeNameForGeneratedMethod(text,originalTypeText,qualifiedTypeText);
        }

        foreach(var memberAccess in allMemberAccesses.OrderByDescending(static memberAccess => memberAccess.ToString().Length)) {
            if(!AnchorAnalysis.IsAnchorAccess(memberAccess,semanticModel)) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var receiverText = RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
            text=text.Replace(memberText,receiverText,StringComparison.Ordinal);
            text=text.Replace($"{receiverText}.Anchor",receiverText,StringComparison.Ordinal);
        }

        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IFieldSymbol field
                || field.DeclaredAccessibility==Accessibility.Public) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            text=text.Replace(
                memberText,
                RenderFieldForwardExpression(field,memberAccess,semanticModel,unsafeAccessorCollector),
                StringComparison.Ordinal);
        }

        // private インスタンスプロパティへのアクセスを UnsafeAccessor (Method "get_X") 経由に変換する。
        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol property
                || property.IsStatic
                || property.DeclaredAccessibility==Accessibility.Public
                || !SymbolEqualityComparer.Default.Equals(property.ContainingType,containingType)
                || property.GetMethod is null) {
                continue;
            }
            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var accessorName = $"__Property_{SanitizeIdentifier(property.ContainingType.Name)}_{SanitizeIdentifier(property.Name)}";
            var targetTypeName = property.ContainingType.ToDisplayString(完全修飾形式);
            var propTypeName = property.Type.ToDisplayString(完全修飾形式);
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_{property.Name}\")]\nprivate static extern {propTypeName} {accessorName}({targetTypeName} @this);";
            unsafeAccessorCollector?.Add(accessorCode);
            var receiverText = NormalizeExpressionText(memberAccess.Expression.ToString())
                .Replace("this","@this",StringComparison.Ordinal);
            text=text.Replace(memberText,$"{accessorName}({receiverText})",StringComparison.Ordinal);
        }
        foreach(var param in allParameters) {
            if(param.Type is null) continue;
            var typeSymbol=semanticModel.GetTypeInfo(param.Type).Type;
            if(typeSymbol is null) continue;
            var qualifiedName=typeSymbol.ToDisplayString(完全修飾形式);
            var originalName=NormalizeExpressionText(param.Type.ToString());
            if(originalName==qualifiedName||originalName.StartsWith("global::",StringComparison.Ordinal)) continue;
            text=ReplaceTypeNameForGeneratedMethod(text,originalName,qualifiedName);
        }
        return text;
    }
    /// <summary>呼び出し式が明示的な型引数リストを持つかどうか（例: <c>Cast&lt;int&gt;(…)</c>）。</summary>
    private static bool InvocationHasExplicitTypeArguments(InvocationExpressionSyntax invocation) =>
        invocation.Expression is GenericNameSyntax
        || invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax };

    /// <summary>
    /// キーを長い順に置換する。値が他のキーの一部分（フィールドアクセサ名に元の識別子が
    /// そのまま埋め込まれる等）を含むことがあるため、語境界 (<c>\b</c>) を必須にして
    /// 既に置換済みの箇所を再置換してしまう事故（二重ラップ）を防ぐ。
    /// </summary>
    private static string ApplyExpressionReplacementMap(string text,IReadOnlyDictionary<string,string>? replacementMap) {
        if(replacementMap is null) {
            return text;
        }

        foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
            var normalizedKey = NormalizeGeneratedExpressionText(pair.Key);
            // \b は両端が単語文字であることを要求するため、`...Aggregate(x=>x)` のように
            // キーが括弧などの非単語文字で終わる（メソッド呼び出し式は常にこの形）場合、
            // 直後が `;`/`,`/`)`/文字列終端などの非単語文字だと \b が一致せず置換が無発火になる。
            // 単語文字との境界のみを要求する (?<!\w) / (?!\w) に置き換えて両端の非単語文字同士は許容する。
            text=System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"(?<!\w){System.Text.RegularExpressions.Regex.Escape(normalizedKey)}(?!\w)",
                pair.Value.Replace("$","$$",StringComparison.Ordinal),
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        return text;
    }

    private static bool IsExpressionReplaced(ExpressionSyntax expression,IReadOnlyDictionary<string,string>? replacementMap) =>
        replacementMap is not null
        && (replacementMap.ContainsKey(NormalizeExpressionText(expression.ToString()))
            || replacementMap.ContainsKey(NormalizeGeneratedExpressionText(expression.ToString())));

    /// <summary>
    /// 通常メソッド呼び出し（非拡張）を生成コード用にレンダリングする。
    /// <br/>public/internal メソッドは完全修飾名で直接呼び出し、
    /// 非 public メソッドは <c>UnsafeAccessor</c> 経由のアクセサ宣言を生成して呼び出す。
    /// static / インスタンスでアクセサの生成方法が異なる（<c>StaticMethod</c> / <c>Method</c>）。
    /// ジェネリックかつ非 public のメソッドは <c>UnsafeAccessor</c> で型引数を渡せないため失敗を返す。
    /// </summary>
    private static bool TryRenderOrdinaryMethodInvocationExpression(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string expressionText) {
        expressionText=string.Empty;
        if(method.IsGenericMethod
            && method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)) {
            return false;
        }
        if(!TryCreateOrdinaryActualArgumentList(method,invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var actualArguments)) {
            return false;
        }
        var explicitTypeArgs = InvocationHasExplicitTypeArguments(invocation) ? RenderTypeArgumentList(method) : string.Empty;
        var argumentList = string.Join(", ",actualArguments);
        var declaringType = method.ContainingType.ToDisplayString(完全修飾形式);
        var canCallDirectly = method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
        if(method.IsStatic) {
            if(canCallDirectly) {
                // 生成 namespace は元 namespace と異なるため、静的メソッド呼び出しは型名を完全修飾する。
                expressionText=$"{declaringType}.{method.Name}{explicitTypeArgs}({argumentList})";
                return true;
            }
            var accessorName = $"__StaticMethod_{SanitizeIdentifier(method.ContainingType.Name)}_{SanitizeIdentifier(method.Name)}";
            var returnTypeName = method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(完全修飾形式);
            var paramParts = new List<string> { $"{declaringType}? _" };
            foreach(var param in method.Parameters) {
                paramParts.Add(ParameterDeclarationForAccessor(param));
            }
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            unsafeAccessorCollector?.Add(accessorCode);
            expressionText=$"{accessorName}(null{(actualArguments.Count>0 ? ", " + argumentList : string.Empty)})";
            return true;
        }
        if(!TryRenderOrdinaryReceiverExpression(invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var receiverText)) {
            return false;
        }
        if(canCallDirectly) {
            expressionText=$"{receiverText}.{method.Name}{explicitTypeArgs}({argumentList})";
            return true;
        }
        var instanceAccessorName = $"__Method_{SanitizeIdentifier(method.ContainingType.Name)}_{SanitizeIdentifier(method.Name)}";
        var instanceReturnTypeName = method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(完全修飾形式);
        var instanceParamParts = new List<string> { $"{declaringType} @this" };
        foreach(var param in method.Parameters) {
            instanceParamParts.Add(ParameterDeclarationForAccessor(param));
        }
        var instanceAccessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{method.Name}\")]\nprivate static extern {instanceReturnTypeName} {instanceAccessorName}({string.Join(", ",instanceParamParts)});";
        unsafeAccessorCollector?.Add(instanceAccessorCode);
        expressionText=$"{instanceAccessorName}({receiverText}{(actualArguments.Count>0 ? ", " + argumentList : string.Empty)})";
        return true;
    }
    /// <summary>
    /// 通常メソッドの実引数リストを構築する。
    /// <br/>名前付き引数に対応するため位置引数とスロット配列で処理する。
    /// 省略可能パラメータは params 配列を空配列で補い、デフォルト値は <see cref="RenderConstantValue"/> で展開する。
    /// スロットが埋まらない必須パラメータがある場合は false を返す。
    /// </summary>
    private static bool TryCreateOrdinaryActualArgumentList(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out List<string> actualArguments) {
        actualArguments=[];
        var parameters = method.Parameters;
        var slots = new string?[parameters.Length];
        var paramsIndex = parameters.Length>0 && parameters[^1].IsParams ? parameters.Length-1 : -1;

        // params 展開呼び出し（例: Row(1, a, b, c)）は、固定引数を超える位置引数、または
        // 末尾の単一引数が配列型へ直接変換されない場合に該当する。その場合は末尾をまとめて配列生成式にする。
        if(paramsIndex>=0) {
            var positionalArgs = invocation.ArgumentList.Arguments.Where(static a=>a.NameColon is null).ToList();
            var isExpandedParamsCall = positionalArgs.Count>parameters.Length
                || (positionalArgs.Count==parameters.Length
                    && !SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetTypeInfo(positionalArgs[^1].Expression).ConvertedType,
                        parameters[paramsIndex].Type));
            if(isExpandedParamsCall) {
                if(positionalArgs.Count<paramsIndex || invocation.ArgumentList.Arguments.Any(a=>a.NameColon is not null)) {
                    return false;
                }
                var elementTypeName = ((IArrayTypeSymbol)parameters[paramsIndex].Type).ElementType.ToDisplayString(完全修飾形式);
                var elements = positionalArgs.Skip(paramsIndex)
                    .Select(a=>RenderArgumentForGeneratedMethod(a,semanticModel,containingType,unsafeAccessorCollector,replacementMap));
                for(var i=0;i<paramsIndex;i++) {
                    slots[i]=RenderArgumentForGeneratedMethod(positionalArgs[i],semanticModel,containingType,unsafeAccessorCollector,replacementMap);
                }
                slots[paramsIndex]=$"new {elementTypeName}[] {{ {string.Join(", ",elements)} }}";
                actualArguments=slots.Select(static slot=>slot!).ToList();
                return true;
            }
        }

        var nextParameterIndex = 0;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=0;i<method.Parameters.Length;i++) {
                    if(method.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<slots.Length && slots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }
            if(targetParameterIndex<0
                || targetParameterIndex>=slots.Length
                || slots[targetParameterIndex] is not null) {
                return false;
            }
            slots[targetParameterIndex]=RenderArgumentForGeneratedMethod(argument,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        }
        for(var i=0;i<slots.Length;i++) {
            if(slots[i] is not null) {
                continue;
            }
            var parameter = method.Parameters[i];
            if(parameter.IsParams && parameter.Type is IArrayTypeSymbol paramsArray) {
                slots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                slots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                return false;
            }
        }
        actualArguments=slots.Select(static slot=>slot!).ToList();
        return true;
    }
    /// <summary>
    /// 通常メソッド呼び出しのレシーバー式テキストを解決する。
    /// <c>obj.Method(…)</c> → <c>obj</c> のレンダリング結果、
    /// <c>Method(…)</c>（暗黙の this）→ <c>@this</c>。
    /// </summary>
    private static bool TryRenderOrdinaryReceiverExpression(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string receiverText) {
        receiverText=string.Empty;
        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            receiverText=RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
            return true;
        }
        if(invocation.Expression is IdentifierNameSyntax) {
            receiverText="@this";
            return true;
        }
        return false;
    }
    private static string ParameterDeclarationForAccessor(IParameterSymbol parameter) {
        var prefix = parameter.RefKind switch {
            RefKind.Ref=>"ref ",
            RefKind.Out=>"out ",
            RefKind.In=>"in ",
            _=>string.Empty,
        };
        return $"{prefix}{parameter.Type.ToDisplayString(完全修飾形式)} {parameter.Name}";
    }
    /// <summary>
    /// テキスト内の型名を完全修飾名に安全に置換する。
    /// 既に一致しているか <c>global::</c> 修飾済みなら何もしない。
    /// 単語境界を考慮した正規表現で部分一致置換を防ぐ。
    /// </summary>
    private static string ReplaceTypeNameForGeneratedMethod(string text,string originalName,string qualifiedName) {
        if(originalName==qualifiedName || originalName.StartsWith("global::",StringComparison.Ordinal)) {
            return text;
        }
        return System.Text.RegularExpressions.Regex.Replace(
            text,
            $@"(?<![\w\.:]){System.Text.RegularExpressions.Regex.Escape(originalName)}(?![\w])",
            qualifiedName,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string ReplaceExpressionFragment(string text,string oldText,string newText) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            $@"(?<![\w\.]){System.Text.RegularExpressions.Regex.Escape(oldText)}",
            newText,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool CanAccessFromGeneratedCode(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static bool IsMemberAccessName(IdentifierNameSyntax identifier) =>
        identifier.Parent is MemberAccessExpressionSyntax memberAccess
        && ReferenceEquals(memberAccess.Name,identifier);

    private static bool IsInsideNameof(SyntaxNode node) {
        for(var current = node.Parent;current is not null;current=current.Parent) {
            if(current is InvocationExpressionSyntax {
                Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }
            }) {
                return true;
            }
        }
        return false;
    }

    private static string ReplaceIdentifierReference(string text,string identifierText,string replacement) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            $@"\b{System.Text.RegularExpressions.Regex.Escape(identifierText)}\b",
            replacement,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string RenderPropertyForwardExpression(IPropertySymbol property,string receiverText,List<string>? unsafeAccessorCollector) {
        var accessorName = $"__Property_{SanitizeIdentifier(property.ContainingType.Name)}_{SanitizeIdentifier(property.Name)}";
        var targetTypeName = property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var propTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_{property.Name}\")]\nprivate static extern {propTypeName} {accessorName}({targetTypeName} @this);";
        unsafeAccessorCollector?.Add(accessorCode);
        return $"{accessorName}({receiverText})";
    }

    /// <summary>
    /// 非 System.Linq 拡張メソッド呼び出しを、その本体をインライン展開した式に置き換える。
    /// <br/>成功条件: 拡張メソッドのソースが取得でき、本体が式本体か単一 <c>return</c> 文であること。
    /// <br/>実引数リストを <see cref="TryCreateExtensionActualArgumentList"/> で構築し、
    /// <see cref="RenderInlineMethodBodyExpression"/> でパラメータ参照を実引数に置換してインライン化する。
    /// </summary>
    private static bool TryRenderExtensionMethodInvocationExpression(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax extensionMemberAccess,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string expressionText) {
        expressionText=string.Empty;
        if(method.ReducedFrom is not { } definition
            || !definition.IsExtensionMethod
            || definition.Parameters.Length==0
            || definition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax
            || invocation.ArgumentList.Arguments.Any(static argument=>!argument.RefKindKeyword.IsKind(SyntaxKind.None))) {
            return false;
        }
        if(!TryCreateExtensionActualArgumentList(
            definition,
            invocation,
            extensionMemberAccess,
            semanticModel,
            containingType,
            unsafeAccessorCollector,
            replacementMap,
            out var actualArguments)) {
            return false;
        }
        var bodySemanticModel = semanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            expressionText=RenderInlineMethodBodyExpression(expressionBody,definition.Parameters,actualArguments,bodySemanticModel,definition.ContainingType,unsafeAccessorCollector);
            return true;
        }
        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            expressionText=RenderInlineMethodBodyExpression(returnExpression,definition.Parameters,actualArguments,bodySemanticModel,definition.ContainingType,unsafeAccessorCollector);
            return true;
        }
        return false;
    }
    /// <summary>
    /// 拡張メソッドのインライン展開用実引数リストを構築する。
    /// スロット[0] にレシーバー式（<c>source</c>）をセットし、
    /// 以降はソース呼び出しの引数リストを名前付き引数対応でスロットに当てはめる。
    /// <see cref="TryCreateOrdinaryActualArgumentList"/> の拡張メソッド版。
    /// </summary>
    private static bool TryCreateExtensionActualArgumentList(
        IMethodSymbol definition,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax extensionMemberAccess,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out List<string> actualArguments) {
        var actualArgumentSlots = new string?[definition.Parameters.Length];
        actualArgumentSlots[0]=RenderExpressionForGeneratedMethod(extensionMemberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        var nextParameterIndex = 1;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=1;i<definition.Parameters.Length;i++) {
                    if(definition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<actualArgumentSlots.Length && actualArgumentSlots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }
            if(targetParameterIndex<=0
                || targetParameterIndex>=actualArgumentSlots.Length
                || actualArgumentSlots[targetParameterIndex] is not null) {
                actualArguments=[];
                return false;
            }
            actualArgumentSlots[targetParameterIndex]=RenderArgumentForGeneratedMethod(argument,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        }
        for(var i=1;i<actualArgumentSlots.Length;i++) {
            if(actualArgumentSlots[i] is not null) {
                continue;
            }
            var parameter = definition.Parameters[i];
            if(parameter.IsParams && parameter.Type is IArrayTypeSymbol paramsArray) {
                actualArgumentSlots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                actualArgumentSlots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                actualArguments=[];
                return false;
            }
        }
        actualArguments=actualArgumentSlots.Select(static argument=>argument!).ToList();
        return true;
    }
    /// <summary>
    /// メソッド本体式をインライン化する。
    /// <see cref="RenderExpressionForGeneratedMethod"/> で本体テキストを生成した後、
    /// <see cref="ReplaceInlineParameterReferences"/> でパラメータ参照を実引数テキストに置換する。
    /// コレクション式や暗黙の配列生成は変換先の型にキャストする（型推論が使えないため）。
    /// </summary>
    private static string RenderInlineMethodBodyExpression(
        ExpressionSyntax bodyExpression,
        ImmutableArray<IParameterSymbol> parameters,
        IReadOnlyList<string> actualArguments,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector) {
        var expressionText = RenderExpressionForGeneratedMethod(bodyExpression,semanticModel,containingType,unsafeAccessorCollector);
        expressionText=ReplaceInlineParameterReferences(expressionText,parameters,actualArguments);
        if(bodyExpression is CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax) {
            var returnType = semanticModel.GetTypeInfo(bodyExpression).ConvertedType?.ToDisplayString(完全修飾形式);
            if(!string.IsNullOrEmpty(returnType)) {
                expressionText=$"(({returnType}){expressionText})";
            }
        }
        return expressionText;
    }
    /// <summary>
    /// 式テキスト内のパラメータ名を実引数テキストに置換する。
    /// <see cref="InlineParameterReferenceRewriter"/> で構文木を書き換えることで、
    /// 単純な文字列置換では不可能な「メンバー名・ラベル等としてのパラメータ名を置換しない」制約を実現する。
    /// </summary>
    private static string ReplaceInlineParameterReferences(string expressionText,ImmutableArray<IParameterSymbol> parameters,IReadOnlyList<string> actualArguments) {
        var replacements = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=0;i<parameters.Length;i++) {
            replacements[parameters[i].Name]=actualArguments[i];
        }
        var expression = SyntaxFactory.ParseExpression(expressionText);
        return NormalizeExpressionText(new InlineParameterReferenceRewriter(replacements).Visit(expression)!.ToString());
    }
    private sealed class InlineParameterReferenceRewriter(IReadOnlyDictionary<string,string> replacements): CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,string> replacements = replacements;

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(!replacements.TryGetValue(node.Identifier.ValueText,out var replacement)
                || node.Parent is MemberAccessExpressionSyntax { Name: var memberName } && ReferenceEquals(memberName,node)
                || node.Parent is MemberBindingExpressionSyntax { Name: var bindingName } && ReferenceEquals(bindingName,node)
                || node.Parent is NameColonSyntax
                || node.Parent is NameEqualsSyntax) {
                return node;
            }
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParseExpression(replacement)).WithTriviaFrom(node);
        }
    }
    /// <summary>
    /// ローカルヘルパーメソッドをインライン化できなかった場合の代替式を生成する。
    /// 静かなフォールバックを作らず、実行時に即例外を投げるラムダ式を返す。
    /// <c>void</c> メソッドは <c>Action</c>、戻り値あり は <c>Func&lt;TResult&gt;</c> のキャストで即呼び出しする。
    /// </summary>
    private static string RenderInlineFailureInvocationExpression(IMethodSymbol method) {
        var message = EscapeStringLiteral($"Local helper method '{method.Name}' could not be inlined. Forwarding is disabled.");
        return method.ReturnsVoid
            ? $"((global::System.Action)(()=>throw global::Seamless.Linq.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()"
            : $"((global::System.Func<{method.ReturnType.ToDisplayString(完全修飾形式)}>)(()=>throw global::Seamless.Linq.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()";
    }

    /// <summary>文字列リテラルに埋め込むために <c>\</c> と <c>"</c> をエスケープする。</summary>
    private static string EscapeStringLiteral(string text) =>
        text.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
}
