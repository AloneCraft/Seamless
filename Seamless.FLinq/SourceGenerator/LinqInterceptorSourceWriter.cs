namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {

    /// <summary>
    /// インターセプト対象群を名前空間ごとにグループ化し、1 ソースファイル分の C# テキストを生成する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="インターセプト名前空間取得"/> で元の名前空間を <c>Intercept.{originalNs}</c> に変換し、グループ化する。</description></item>
    ///   <item><description>各名前空間ブロック内に <c>using {originalNs};</c> を挿入して、生成コードで短い型名を使えるようにする。</description></item>
    ///   <item><description>同一名前空間内でさらに型ごとにグループ化し、<see cref="インターセプター型ソース構築"/> に委譲する。</description></item>
    /// </list>
    /// </summary>
    /// <summary>
    /// ドット区切り名前空間の 1 つ上の親名前空間を返す。最上位（ドット無し）なら空文字列。
    /// </summary>
    private static string 親名前空間取得(string 名前空間) {
        var i=名前空間.LastIndexOf('.');
        return i<0 ? string.Empty : 名前空間[..i];
    }

    private static string インターセプターソース構築(ImmutableArray<Linq傍受対象型> targets,List<Diagnostic> diagnostics) {
        var builder=new StringBuilder();
        builder.AppendLine("#nullable enable annotations");
        builder.AppendLine("#nullable disable warnings");
        builder.AppendLine("#pragma warning disable CS0414, CS0649");
        builder.AppendLine("using System.Linq;");

        // このファイルで生成する匿名型→record struct 透過置換のレジストリを用意し、
        // ネストした静的メソッド群（関数式フラグメント生成 等）から ThreadStatic 経由で参照させる。
        var anonRegistry=new AnonymousStructRecordRegistry(
            AnonRecordFileToken(targets.IsDefaultOrEmpty ? string.Empty : targets[0].SourcePath));
        var previousRegistry=_anonRegistry;
        _anonRegistry=anonRegistry;
        try {
            foreach(var namespaceGroup in targets.GroupBy(static target => インターセプト名前空間取得(target.EnclosingType),
                        StringComparer.Ordinal)) {
                builder.AppendLine();
                builder.AppendLine($"namespace {namespaceGroup.Key} {{");
                var originalNamespace=namespaceGroup.Key.StartsWith("Intercept.",StringComparison.Ordinal)
                    ? namespaceGroup.Key["Intercept.".Length..]
                    : string.Empty;
                if(!string.IsNullOrWhiteSpace(originalNamespace)) {
                    // 生成コードは Intercept.{originalNamespace} という別名前空間に置かれるため、
                    // 元のネスト名前空間スコープで暗黙に見えていた親名前空間のメンバー（拡張メソッド等）を
                    // 解決できるよう、元の名前空間とその全祖先を using として出力する。
                    // 生成側の名前空間が Intercept.Test... のように元の先頭セグメントを内包するため、
                    // 素の using だと相対解決で Intercept.Test... を指してしまう。global:: で明示する。
                    for(var ns=originalNamespace; !string.IsNullOrEmpty(ns); ns=親名前空間取得(ns)) {
                        builder.AppendLine($"    using global::{ns};");
                    }

                    builder.AppendLine();
                }
                foreach(var typeGroup in namespaceGroup.GroupBy(
                            static target => target.EnclosingType.ToDisplayString(完全修飾形式),StringComparer.Ordinal)) {
                    var typeTargets=typeGroup.ToImmutableArray();
                    var typeSource=インターセプター型ソース構築(typeTargets,diagnostics);
                    インデント付き追記(builder,typeSource,"    ");
                }
                builder.AppendLine("}");
            }
            // 匿名型置換で割り当てた record struct 定義をグローバル名前空間のホルダークラスとして出力する。
            var holder=anonRegistry.RenderHolder();
            if(!string.IsNullOrEmpty(holder)) {
                builder.AppendLine();
                builder.Append(holder);
            }
            return builder.ToString();
        }
        finally {
            _anonRegistry=previousRegistry;
        }
    }
    /// <summary>
    /// 1 つの型に属するインターセプト対象群をまとめて <c>internal static class</c> ブロックへ出力する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="型スタック取得"/> でネスト型の階層スタックを取得し、外側から順に <c>internal static class</c> を開く。</description></item>
    ///   <item><description>各ターゲットについて <see cref="ローカル傍受メソッド本体構築"/> を試みる。成功すれば <c>[InterceptsLocation]</c> 付きメソッドを出力する。</description></item>
    ///   <item><description>本体構築に失敗した場合は Fragment 未対応か展開不能かを判別し、いずれも例外 throw を生成して FLINQ002 警告を発行する（静かなフォールバックは作らない）。</description></item>
    ///   <item><description>例外が発生した場合も同様に例外 throw コードを生成し、同一メソッドへの二重出力は <c>emittedMethodNames</c> で抑制する。</description></item>
    ///   <item><description>UnsafeAccessor 宣言はクラス直下に出力し、型スタックを逆順に閉じる。</description></item>
    /// </list>
    /// </summary>
    private static string インターセプター型ソース構築(ImmutableArray<Linq傍受対象型> targets,List<Diagnostic> diagnostics) {
        var builder = new StringBuilder();
        var typeStack = 型スタック取得(targets[0].EnclosingType);
        var interceptorTypeName=string.Join("_",typeStack.Select(static type=>SanitizeIdentifier(type.Name)));
        builder.AppendLine($"internal static partial class {interceptorTypeName} {{");

        var emittedMethodNames = new HashSet<string>(StringComparer.Ordinal);
        var emittedUnsafeAccessors = new HashSet<string>(StringComparer.Ordinal);
        foreach(var target in targets) {
            if(target.OptimizeLinq){
                var seenUnorderedInvocations=new HashSet<int>();
                foreach(var syntaxReference in target.Method.DeclaringSyntaxReferences){
                    var syntax=syntaxReference.GetSyntax();
                    var unorderedSemanticModel=target.SemanticModel.Compilation.GetSemanticModel(syntax.SyntaxTree);
                    foreach(var unorderedInvocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()){
                        if(!seenUnorderedInvocations.Add(unorderedInvocation.SpanStart)
                            ||!TryGetUnorderedInvocation(unorderedSemanticModel,unorderedInvocation,out var unorderedMethodName,out var unorderedSourceType,out var explicitOptIn)){
                            continue;
                        }
                        diagnostics.Add(Diagnostic.Create(
                            explicitOptIn?UnorderedOperationOptInDescriptor:UnorderedOperationWarningDescriptor,
                            unorderedInvocation.GetLocation(),
                            unorderedMethodName,
                            unorderedSourceType));
                    }
                }
            }
            try {
                if(target.IsLocalMethodInterception) {
                    using var optimizationScope=LinqOptimizationContext.Enter(target.Optimizations);
                    var ローカル静的メソッド群 = string.Empty;
                    var ローカル本体 = string.Empty;
                    var builtLocalMethodBody = target.OptimizeLinq
                        ? ローカル傍受メソッド本体構築(target,out ローカル静的メソッド群,out ローカル本体,emittedUnsafeAccessors)
                        : TryBuildCounterOnlyLocalMethodBody(target,out ローカル静的メソッド群,out ローカル本体,emittedUnsafeAccessors);
                    if(builtLocalMethodBody) {
                        if(!string.IsNullOrWhiteSpace(ローカル静的メソッド群)) {
                            インデント付き追記(builder,ローカル静的メソッド群,"    ");
                        }
                        インターセプターXmlコメント追加(builder,target);
                        builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining|global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]");
                        builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                        builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}){target.TypeParameterConstraintClause} {{");
                        インデント付き追記(builder,ローカル本体,"        ");
                        builder.AppendLine("    }");
                        builder.AppendLine();
                        continue;
                    }

                    if(target.ContainingTypeIsGeneric) {
                        // ジェネリック含有型は段階対応中。展開できないケースは throw スタブを出さず、
                        // 傍受自体を諦めて元実装（素の System.Linq）をそのまま実行させる（遅いが正しい）。
                        continue;
                    }

                    インターセプターXmlコメント追加(builder,target);
                    builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining|global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]");
                    builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                    builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}){target.TypeParameterConstraintClause} {{");
                    if(未対応Enumerableメソッド一覧取得(target) is { Count: > 0 } unsupportedMethods) {
                        var loc1 = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                        diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,loc1,target.Method.Name));
                        インデント付き追記(builder,RenderUnsupportedLocalMethodBody(unsupportedMethods),"        ");
                    } else {
                        var loc2 = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                        diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,loc2,target.Method.Name));
                        インデント付き追記(builder,RenderUninlineableLocalMethodBody(target),"        ");
                    }
                    builder.AppendLine("    }");
                    builder.AppendLine();
                }
            } catch(Exception ex) {
                if(target.ContainingTypeIsGeneric) {
                    // ジェネリック含有型は段階対応中。展開時例外は throw スタブ化せず傍受を諦める（元実装のまま）。
                    continue;
                }
                if(emittedMethodNames.Contains(target.MethodName)) {
                    continue;
                }

                emittedMethodNames.Add(target.MethodName);
                var locEx = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,locEx,target.Method.Name));
                インターセプターXmlコメント追加(builder,target);
                builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining|global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]");
                builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}){target.TypeParameterConstraintClause} {{");
                インデント付き追記(builder,RenderUnsupportedDirectMethodBody(target,ex.Message),"        ");
                builder.AppendLine("    }");
                builder.AppendLine();
            }
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// ネスト型の外側から内側への順序で型シンボルのリストを返す。
    /// <br/>例: <c>Outer.Inner</c> なら <c>[Outer, Inner]</c>。
    /// <br/><c>ContainingType</c> を辿りながらスタックに積み、最後に Reverse することで外→内の順にする。
    /// </summary>
    private static List<INamedTypeSymbol> 型スタック取得(INamedTypeSymbol type) {
        var stack = new List<INamedTypeSymbol>();
        for(INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            stack.Add(current);
        }
        stack.Reverse();
        return stack;
    }

    /// <summary>
    /// 元の型の名前空間を <c>Intercept.{originalNs}</c> へ変換する。
    /// グローバル名前空間の型は <c>Intercept</c> のみ。
    /// インターセプターを別名前空間に置くことで、元コードと名前が衝突しない。
    /// </summary>
    private static string インターセプト名前空間取得(INamedTypeSymbol type) {
        var originalNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;
        return string.IsNullOrWhiteSpace(originalNamespace)
            ? "Intercept"
            : $"Intercept.{originalNamespace}";
    }

    /// <summary>
    /// ソースファイルのパスからインターセプター生成ファイルの HintName（<c>{FileName}.g.cs</c>）を返す。
    /// ファイル名が空の場合は <c>Unknown.g.cs</c> にフォールバックする。
    /// <see cref="SanitizeIdentifier"/> で C# 識別子として使えない文字を <c>_</c> に置換する。
    /// </summary>
    private static string インターセプターHintName取得(string sourcePath) {
        if(string.IsNullOrWhiteSpace(sourcePath)) {
            return "Unknown.g.cs";
        }
        //同名ファイルが別ディレクトリに存在しても hintName が衝突しないよう、フルパスから生成する
        return $"{SanitizeIdentifier(sourcePath)}.g.cs";
    }

    /// <summary>
    /// インターセプターメソッドの直前に XML doc コメントを追加する。
    /// 型が公開アクセス可能な場合は <c>&lt;see cref="Type"/&gt;</c> でクラスへのリンクを生成し、
    /// そうでない場合は <c>&lt;c&gt;</c> タグのみで完全修飾名を表示する。
    /// private メソッドへの <c>&lt;see cref&gt;</c> は Roslyn が解決できず CS1574 になるため、
    /// メソッド名は常に <c>&lt;c&gt;</c> タグで出力する。
    /// </summary>
    private static void インターセプターXmlコメント追加(StringBuilder builder,Linq傍受対象型 target) {
        var method = target.Method;
        var methodName = Xmlテキストエスケープ(method.Name);
        string content;
        if(型が公開アクセス可能か(method.ContainingType)){
            var typeCref = Xml属性値エスケープ(method.ContainingType.ToDisplayString(cref形式));
            content = $"<c>{methodName}()</c> in <see cref=\"{typeCref}\"/>";
        }else{
            var fqn = Xmlテキストエスケープ(メソッドCref構築(method));
            content = $"<c>{fqn}</c>";
        }
        builder.AppendLine($"    /// <summary>Intercepts {content}.</summary>");
    }

    private static bool 型が公開アクセス可能か(INamedTypeSymbol type) {
        for(var t = type; t is not null; t = t.ContainingType){
            if(t.DeclaredAccessibility is not(Accessibility.Public or Accessibility.Internal)){
                return false;
            }
        }
        return true;
    }

    private static string メソッドCref構築(IMethodSymbol method) {
        var containingType = method.ContainingType.ToDisplayString(cref形式);
        var parameters = string.Join(",",method.Parameters.Select(static parameter=>parameter.Type.ToDisplayString(cref形式)));
        return $"{containingType}.{method.Name}({parameters})";
    }

    private static string Xml属性値エスケープ(string text) =>
        text
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal);

    private static string Xmlテキストエスケープ(string text) =>
        text
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal);
    private static string RenderUnsupportedLocalMethodBody(IReadOnlyList<string> unsupportedMethods) {
        // Fragment クラスが存在しない Enumerable メソッドは、元実装へ黙って戻さず例外にする。
        // これにより「intercept 対象だったが未対応だった」ことを生成コードと実行時の両方で確認できる。
        var methodList = string.Join(", ",unsupportedMethods);
        return $"throw global::Seamless.Linq.GeneratedCodeHelpers.UnsupportedLinqExpansion(\"local method interception\",\"System.Linq.Enumerable methods without Fragment implementations: {methodList}.\");";
    }
    private static string RenderUninlineableLocalMethodBody(Linq傍受対象型 target) {
        // Fragment クラス名は存在しても、source 形状や delegate 形状の都合で最終的に再構成できないことがある。
        // その場合も静かに元実装へ戻さず失敗を見える化する。
        var methodName = target.Method.ToDisplayString(完全修飾形式)
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw global::Seamless.Linq.GeneratedCodeHelpers.UnsupportedLinqExpansion(\"{methodName}\",\"Forwarding is disabled for methods that contain System.Linq.Enumerable calls.\");";
    }
    private static string RenderUnsupportedDirectMethodBody(Linq傍受対象型 target,string? detail = null) {
        var methodName = target.Method.ReducedFrom?.ToDisplayString(完全修飾形式) ?? target.Method.ToDisplayString(完全修飾形式);
        var message = $"Direct interception could not inline '{methodName}' inside '{target.ContainingMethodName}'.";
        if(!string.IsNullOrWhiteSpace(detail)) {
            message=$"{message} {detail}";
        }
        message=message
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw global::Seamless.Linq.GeneratedCodeHelpers.UnsupportedLinqExpansion(\"{target.Method.Name}\",\"{message}\");";
    }
}
