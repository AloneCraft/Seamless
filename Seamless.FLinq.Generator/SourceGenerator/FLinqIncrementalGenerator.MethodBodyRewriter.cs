using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;

using Enumerable = Seamless.FLinq.Generator.Enumerable;

namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator {
    internal sealed partial class GenerationPipeline {

    /// <summary>
    /// インターセプト対象メソッドの本体を丸ごと再構成し、文中の LINQ 呼び出しを Fragment 実行へ差し替える。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>メソッドの構文参照から <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/> を取得する。式本体（<c>=&gt;</c>）の場合は <c>式本体式</c> として保持し、後段で専用処理に渡す。</description></item>
    ///   <item><description><see cref="CollectMethodBodyIdentifiers"/> でメソッドスコープの識別子をすべて収集し、生成変数名の衝突防止に使う。</description></item>
    ///   <item><description>各文を <see cref="文を生成コードへ変換"/> に渡して再構成する。1 文でも失敗したら false を返す。</description></item>
    ///   <item><description>式本体の場合は <see cref="LINQ呼び出し差し替え式本体生成"/> で処理する（ラムダ内に this. がある場合は対象外）。</description></item>
    ///   <item><description>LINQ 呼び出しが 1 件も置換されなかった場合は false を返す（再構成不要のメソッドを無駄にインターセプトしない）。</description></item>
    ///   <item><description>生成済み本体テキストの <c>this.</c> を <c>@this.</c> に置換して、インスタンスメソッドのレシーバー引数と整合させる。</description></item>
    ///   <item><description><see cref="補助メンバーをメソッド専用クラスへ退避"/> で補助 struct を専用クラスへ退避し名前衝突を回避する。</description></item>
    ///   <item><description>Fragment ツリーおよびソース式から UnsafeAccessor 宣言を収集し、クラス直下用の <c>静的メソッド群</c> に追記する。</description></item>
    /// </list>
    /// </summary>
    internal bool TryBuildLocalMethodBody(Linq傍受対象型 対象,out string 静的メソッド群,out string 本体,HashSet<string>? emittedUnsafeAccessors = null) {
        静的メソッド群=string.Empty;
        this.state.StaticMethods=string.Empty;
        本体=string.Empty;

        // 以前は識別子ハッシュから開始番号を作っていたため Struct1144748 のような読みにくい名前になっていた。
        // いまはメソッドごとに 0 から連番を振り、必要なら外側で補助クラスに包んで衝突を避ける。
        this.state.FragmentNumber = フラグメント番号初期値取得();
        if(対象.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax メソッド構文) {
            return false;
        }

        ExpressionSyntax? 式本体式 = null;
        IEnumerable<StatementSyntax> statements;
        if(メソッド構文.Body is { } ブロック本体) {
            statements=ブロック本体.Statements;
        } else if(メソッド構文.ExpressionBody is { Expression: var アロー式 }) {
            // 合成ノード SyntaxFactory.ReturnStatement(アロー式) を作ると SyntaxTree が null になり
            // semanticModel.GetSymbolInfo() が「構文ノードが構文ツリー内にありません」を投げる。
            // 元ツリーのノードを保持して後で専用関数に渡す。
            // インスタンスメソッドでも LINQ呼び出し差し替え式本体生成 が PropagateContainingTypeName と
            // this.→@this. 変換でソース式レベルの this. を処理できる。
            // lambda 内の this. は 関数式フラグメント生成 が capturesThis を検出して @this を struct closure の
            // キャプチャ変数として持ち回るため、fragment struct 内でも @this は定義される。
            式本体式=アロー式;
            statements=global::System.Linq.Enumerable.Empty<StatementSyntax>();
        } else {
            return false;
        }

        var セマンティックモデル = 対象.SemanticModel.Compilation.GetSemanticModel(メソッド構文.SyntaxTree);
        var usedNames=this.state.UsedIdentifiers;
        usedNames.Clear();
        usedNames.UnionWith(CollectMethodBodyIdentifiers(メソッド構文,対象.Method));
        var sourceUnsafeAccessorCollector = this.state.UnsafeAccessorCodes;
        sourceUnsafeAccessorCollector.Clear();
        var generatedFragments = this.state.GeneratedFragments;
        generatedFragments.Clear();
        var 本体ビルダー = new StringBuilder();
        this.state.LinqInvocationCount=0;
        foreach(var 文 in statements) {
            if(!文を生成コードへ変換(文,セマンティックモデル,対象.EnclosingType,ref this.state.FragmentNumber,対象.Method.IsStatic,ref this.state.StaticMethods,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref this.state.LinqInvocationCount,usedNames)) {
                return false;
            }
        }

        if(式本体式 is not null) {
            if(!LINQ呼び出し差し替え式本体生成(式本体式,セマンティックモデル,対象.EnclosingType,ref this.state.FragmentNumber,対象.Method.IsStatic,対象.Method.ReturnsVoid,ref this.state.StaticMethods,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref this.state.LinqInvocationCount,usedNames)) {
                return false;
            }
        }

        if(this.state.LinqInvocationCount==0) {
            return false;
        }

        本体=本体ビルダー
            .ToString()
            .TrimEnd()
            .Replace("throw NoElements();","throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();",StringComparison.Ordinal);

        // 安全網: 匿名型→record struct 置換で取りこぼした生の匿名型表示が残ると不正な C# になるため、
        // その場合はインライン展開を諦めて呼び出し元の例外スタブ生成へフォールバックする。
        if(本体.IndexOf("<anonymous type",StringComparison.Ordinal)>=0
            ||this.state.StaticMethods.IndexOf("<anonymous type",StringComparison.Ordinal)>=0) {
            return false;
        }

        if(!対象.Method.IsStatic) {
            本体=System.Text.RegularExpressions.Regex.Replace(
                本体,
                @"(?<!@)\bthis\.",
                "@this.",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        補助メンバーをメソッド専用クラスへ退避(対象.MethodName,ref this.state.StaticMethods,ref 本体);

        // フラグメントツリーおよびソース式から UnsafeAccessor 宣言を収集し、補助クラスの外（クラス直下）に追記する。
        // これにより、インターセプタメソッド本体から短い名前で直接参照できる。
        // emittedUnsafeAccessors が渡された場合はクラスレベルで重複しないようフィルタする。
        var generatedCodeWithoutAccessors = this.state.StaticMethods+"\n"+本体;
        var unsafeAccessorCodes = generatedFragments
            .SelectMany(static fragment => fragment.CollectAllUnsafeAccessorCodes())
            .Concat(sourceUnsafeAccessorCollector)
            .Where(code => UnsafeAccessorIsReferenced(code,generatedCodeWithoutAccessors))
            .Where(code => emittedUnsafeAccessors?.Add(code)??true)
            .ToList();
        if(unsafeAccessorCodes.Count>0) {
            this.state.StaticMethods=this.state.StaticMethods+"\n"+string.Join("\n",unsafeAccessorCodes);
        }

        静的メソッド群=this.state.StaticMethods;
        return true;
    }

    private bool UnsafeAccessorIsReferenced(string accessorCode,string generatedCode) {
        var match = System.Text.RegularExpressions.Regex.Match(
            accessorCode,
            @"\b(__[A-Za-z0-9_]+)(?:<[^>\r\n]+>)?\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success&&generatedCode.Contains(match.Groups[1].Value,StringComparison.Ordinal);
    }

    /// <summary>
    /// Fragment を実行するコードを <paramref name="builder"/> に追記する。
    /// <br/>Fragment の種類に応じて 3 通りの出力を使い分ける:
    /// <list type="bullet">
    ///   <item><description><see cref="DirectExecutionFragment"/>: <c>GetDirectExecutionStatement</c> の結果をそのまま出力（例: <c>ToList()</c> など副作用付き終端操作）。</description></item>
    ///   <item><description><see cref="Enumerable"/>: <c>var {name} = {GetEnumerableExpression};</c> を出力（遅延評価の列挙結果を変数に束縛）。</description></item>
    ///   <item><description>その他の <see cref="Fragment"/>（スカラー等）: <c>GetEnumeratorExpression</c> で enumerator を取得し <c>TryGetNext</c> で 1 要素を取り出すパターンを出力。</description></item>
    /// </list>
    /// <see cref="GetUniqueIdentifier"/> で enumerator 変数名・current 変数名の衝突を避ける。
    /// </summary>
    private bool フラグメント実行文追加(StringBuilder builder,Fragment fragment,string actualVariableName,string actualTypeName,HashSet<string> usedNames) {
        var suffix = SanitizeIdentifier(actualVariableName);
        var enumeratorName = GetUniqueIdentifier($"e_{suffix}",usedNames);
        var currentName = GetUniqueIdentifier($"c_{suffix}",usedNames);


        if(fragment is DirectExecutionFragment directExecutionFragment) {
            MethodBodyRenderPlan.Statement(directExecutionFragment.GetDirectExecutionStatement(actualVariableName,actualTypeName)).AppendTo(builder);
            return true;
        }

        if(fragment is Enumerable enumerableFragment) {
            try {
                MethodBodyRenderPlan.Statement($"var {actualVariableName}={enumerableFragment.GetEnumerableExpression};").AppendTo(builder);
            } catch(NotSupportedException) {
                return false;
            }
            return true;
        }

        // スカラー終端は単一要素 enumerator struct（Struct{番号}）を生成せず、畳み込みを destVar へ直書きできる。
        // RenderInlineComputation が非 null を返すなら Struct{番号}+TryGetNext を丸ごと省く。
        if(fragment is Scalar scalarFragment
            &&scalarFragment.RenderInlineComputation(actualVariableName,actualTypeName) is { } inlineComputation) {
            MethodBodyRenderPlan.Statement(inlineComputation).AppendTo(builder);
            return true;
        }

        string scalarEnumeratorExpression;
        try {
            scalarEnumeratorExpression=fragment.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return false;
        }
        MethodBodyRenderPlan.ScalarRead(actualTypeName,actualVariableName,enumeratorName,scalarEnumeratorExpression).AppendTo(builder);

        return true;
    }

    /// <summary>
    /// スカラー断片が <see cref="Scalar.RenderInlineComputation"/> を返す場合は Struct{番号}（単一要素 enumerator）を
    /// 含まない <see cref="Scalar.InlineSupportingMembers"/> を、そうでなければ従来の <c>GetEnumeratorStruct</c> を返す。
    /// <see cref="フラグメント実行文追加"/> のインライン判定と一致させて補助メンバの過不足を防ぐ。
    /// </summary>
    private string スカラー対応補助メンバー取得(Fragment fragment,string varName,string typeName) =>
        fragment is Scalar sc&&sc.RenderInlineComputation(varName,typeName) is not null
            ? sc.InlineSupportingMembers
            : fragment.GetEnumeratorStruct;

    private void ApplyCounterInstrumentation(Fragment fragment,SemanticModel semanticModel,SyntaxNode node) {
        if(!IsCounterInstrumentationEnabled(semanticModel,node)) {
            return;
        }

        fragment.ApplyRenderContext(fragment.RenderContext.WithCounterInstrumentation(true));
    }

    private bool IsCounterInstrumentationEnabled(SemanticModel semanticModel,SyntaxNode node) {
        var methodDeclaration = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if(methodDeclaration is null
            ||semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method) {
            return false;
        }

        if(TryGetLinqInstrumentation(method.GetAttributes(),out var methodInstrumentation)) {
            return methodInstrumentation;
        }

        for(var type = method.ContainingType;type is not null;type=type.ContainingType) {
            if(TryGetLinqInstrumentation(type.GetAttributes(),out var typeInstrumentation)) {
                return typeInstrumentation;
            }
        }

        return TryGetLinqInstrumentation(semanticModel.Compilation.Assembly.GetAttributes(),out var assemblyInstrumentation)
            &&assemblyInstrumentation;
    }

    /// <summary>
    /// <c>expr.Consume(consumer)</c>（引数1個・文形）を直接消費ループへ展開する。
    /// <br/>演算子別の特殊処理は持たない（thin orchestrator）。<see cref="呼び出し式からフラグメント生成"/> で
    /// Fragment を構築し、消費ループの組み立ては <see cref="Enumerable.GetConsumeExecutionStatement"/> に委譲する。
    /// span 直走査などの最適化は各 Fragment 側に存在する（コード片は Fragment が持つ原則）。
    /// </summary>
    private bool 直接Consumeループ文生成(
        ExpressionStatementSyntax statement,
        InvocationExpressionSyntax linqInvocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement.Expression is not InvocationExpressionSyntax consumeInvocation) {
            return false;
        }
        if(consumeInvocation.Expression is not MemberAccessExpressionSyntax memberAccess
            ||memberAccess.Name.Identifier.ValueText!="Consume"
            ||consumeInvocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }

        if(!TryGetDirectConsumeConsumerExpression(consumeInvocation,linqInvocation,out var consumerExpression,out var linqIsConsumeReceiver)) {
            return false;
        }
        if(semanticModel.GetSymbolInfo(linqInvocation).Symbol is IMethodSymbol sourceMethod
            &&sourceMethod.ContainingType.Name=="UnorderedGuard") {
            return false;
        }

        var snapshot = uniqueNumber;
        var linqInvocationCountSnapshot = linqInvocationCount;
        var staticMethodsSnapshot = staticMethods;
        var builderLengthSnapshot = builder.Length;
        var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
        var fragmentBuilt=呼び出し式からフラグメント生成(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector);
        if(!fragmentBuilt
            &&TryBuildKnownEnumerableRoot(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var ordinaryRootFragment,unsafeAccessorCollector)) {
            fragment=ordinaryRootFragment;
            invocationStaticMethods=string.Empty;
            fragmentBuilt=true;
        }
        if(!fragmentBuilt) {
            if(!列挙式フラグメント生成(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var enumerableFragmentFallback,out invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                linqInvocationCount=linqInvocationCountSnapshot;
                staticMethods=staticMethodsSnapshot;
                builder.Length=builderLengthSnapshot;
                return false;
            }
            fragment=enumerableFragmentFallback;
        }
        if(fragment is not Enumerable enumerableFragment) {
            uniqueNumber=snapshot;
            linqInvocationCount=linqInvocationCountSnapshot;
            staticMethods=staticMethodsSnapshot;
            builder.Length=builderLengthSnapshot;
            return false;
        }
        if(!methodIsStatic) {
            fragment.ApplyRenderContext(fragment.RenderContext.WithContainingTypeName(containingType.ToDisplayString(完全修飾形式),this.state.ResolveOuterTypeParameters(containingType).ToImmutableArray()));
        }
        ApplyCounterInstrumentation(fragment,semanticModel,linqInvocation);
        generatedFragments.Add(fragment);
        staticMethods=静的メソッドコード連結(staticMethods,invocationStaticMethods);
        staticMethods=静的メソッドコード連結(staticMethods,fragment.GetEnumeratorStruct);

        var consumerName = GetUniqueIdentifier($"consumer_{SanitizeIdentifier(tempName)}",usedNames);
        AppendConsumeSetup(
            builder,
            MethodBodyRenderPlan.Statement(enumerableFragment.GetConsumePreparationStatement()),
            MethodBodyRenderPlan.ConsumerAssignment(consumerName,RenderExpressionForGeneratedMethod(consumerExpression,semanticModel,containingType,unsafeAccessorCollector)),
            linqIsConsumeReceiver);
        // 演算子別の Consume 展開は Fragment（Enumerable.GetConsumeExecutionStatement）が担う。
        MethodBodyRenderPlan.Statement(enumerableFragment.GetConsumeExecutionStatement(consumerName)).AppendTo(builder);
        return true;
    }

    private bool 直接Consume式生成(
        InvocationExpressionSyntax consumeInvocation,
        InvocationExpressionSyntax linqInvocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(consumeInvocation.Expression is not MemberAccessExpressionSyntax memberAccess
            ||memberAccess.Name.Identifier.ValueText!="Consume"
            ||consumeInvocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }

        if(!TryGetDirectConsumeConsumerExpression(consumeInvocation,linqInvocation,out var consumerExpression,out var linqIsConsumeReceiver)) {
            return false;
        }
        if(semanticModel.GetSymbolInfo(linqInvocation).Symbol is IMethodSymbol sourceMethod
            &&sourceMethod.ContainingType.Name=="UnorderedGuard") {
            return false;
        }
        if(TryGetElidableMaterializeSource(linqInvocation,semanticModel,out var materializeSource)
            &&(!linqIsConsumeReceiver||IsEvaluationOrderFree(consumerExpression))
            &&直接Consume本体生成(consumerExpression,linqIsConsumeReceiver,materializeSource,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
            return true;
        }
        return 直接Consume本体生成(consumerExpression,linqIsConsumeReceiver,linqInvocation,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    // Consume へ渡すだけの ToArray()/ToList() は、作った配列・List を流しても元の列を流しても要素と順が同じ。
    // FLinq の契約(.Anchor の無い式は副作用・例外なし)では途中の評価の順も観測されないので、作らずに元の列を直接流す。
    // 途中で例外を投げ得る演算子(Cast)を含む列は、例外の前に Consume が呼ばれる回数が変わるので省かない。
    // 列が Consume の受け手のときは、System.Linq では列(ソース式)を作り終えてから消費側の式を評価する。直接流すと
    // ソース式の評価が消費側の式より後になるので、消費側の式が評価順の観測できない形(変数・フィールド)のときだけ省く。
    private bool TryGetElidableMaterializeSource(InvocationExpressionSyntax invocation,SemanticModel semanticModel,out InvocationExpressionSyntax source) {
        source=null!;
        if(invocation.ArgumentList.Arguments.Count!=0
            ||invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            ||StripConsumeParentheses(memberAccess.Expression) is not InvocationExpressionSyntax receiver
            ||semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            ||(method.ReducedFrom??method) is not { Name: "ToArray" or "ToList" } definition
            ||!IsSupportedLinqEnumerableMethod(definition)
            ||AnchorAnalysis.ContainsAnchor(receiver,semanticModel)
            ||receiver.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(static call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Cast" })) {
            return false;
        }
        source=receiver;
        return true;
    }

    private static bool IsEvaluationOrderFree(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression switch {
            IdentifierNameSyntax or ThisExpressionSyntax => true,
            MemberAccessExpressionSyntax { Expression: var receiver } memberAccess when memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression) => IsEvaluationOrderFree(receiver),
            _ => false,
        };
    }

    private bool 直接Consume本体生成(
        ExpressionSyntax consumerExpression,
        bool linqIsConsumeReceiver,
        InvocationExpressionSyntax linqInvocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var snapshot = uniqueNumber;
        var linqInvocationCountSnapshot = linqInvocationCount;
        var staticMethodsSnapshot = staticMethods;
        var builderLengthSnapshot = builder.Length;
        var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
        var fragmentBuilt=呼び出し式からフラグメント生成(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector);
        if(!fragmentBuilt
            &&TryBuildKnownEnumerableRoot(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var ordinaryRootFragment,unsafeAccessorCollector)) {
            fragment=ordinaryRootFragment;
            invocationStaticMethods=string.Empty;
            fragmentBuilt=true;
        }
        if(!fragmentBuilt) {
            if(!列挙式フラグメント生成(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var enumerableFragmentFallback,out invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                linqInvocationCount=linqInvocationCountSnapshot;
                staticMethods=staticMethodsSnapshot;
                builder.Length=builderLengthSnapshot;
                return false;
            }
            fragment=enumerableFragmentFallback;
        }
        if(fragment is not Enumerable&&fragment is not Scalar) {
            uniqueNumber=snapshot;
            linqInvocationCount=linqInvocationCountSnapshot;
            staticMethods=staticMethodsSnapshot;
            builder.Length=builderLengthSnapshot;
            return false;
        }
        if(!methodIsStatic) {
            fragment.ApplyRenderContext(fragment.RenderContext.WithContainingTypeName(containingType.ToDisplayString(完全修飾形式),this.state.ResolveOuterTypeParameters(containingType).ToImmutableArray()));
        }
        ApplyCounterInstrumentation(fragment,semanticModel,linqInvocation);
        var isScalarMaterializeTerminal = IsScalarConsumeMaterializeTerminal(linqInvocation,semanticModel);
        var scalarResultTypeName = string.Empty;
        string? scalarInlineComputation = null;
        string? scalarConsumeEnumeratorExpression = null;
        string? materializeEnumeratorExpression = null;
        string? materializeInlineComputation = null;
        string? materializeResultTypeName = null;
        if(fragment is Scalar scalarPreflight&&!isScalarMaterializeTerminal) {
            var scalarResultTypeInfo = semanticModel.GetTypeInfo(linqInvocation);
            scalarResultTypeName=(scalarResultTypeInfo.Type??scalarResultTypeInfo.ConvertedType)?.ToDisplayString(完全修飾形式)
                ??"object";
            if(fragment is not DirectExecutionFragment) {
                scalarInlineComputation=scalarPreflight.RenderInlineComputation(tempName,scalarResultTypeName);
                if(scalarInlineComputation is null) {
                    try {
                        scalarConsumeEnumeratorExpression=fragment.GetEnumeratorExpression;
                    } catch(NotSupportedException) {
                        uniqueNumber=snapshot;
                        linqInvocationCount=linqInvocationCountSnapshot;
                        staticMethods=staticMethodsSnapshot;
                        builder.Length=builderLengthSnapshot;
                        return false;
                    }
                }
            }
        }
        if(fragment is Scalar&&isScalarMaterializeTerminal&&fragment is not DirectExecutionFragment) {
            var materializeResultTypeInfo=semanticModel.GetTypeInfo(linqInvocation);
            materializeResultTypeName=(materializeResultTypeInfo.Type??materializeResultTypeInfo.ConvertedType)?.ToDisplayString(完全修飾形式)
                ??"object";
            if(fragment is ToList { CanInlineMaterializeConsume: true } toList) {
                materializeInlineComputation=toList.RenderInlineComputation(tempName,materializeResultTypeName);
            }
            if(materializeInlineComputation is null) {
                try {
                    materializeEnumeratorExpression=fragment.GetEnumeratorExpression;
                } catch(NotSupportedException) {
                    uniqueNumber=snapshot;
                    linqInvocationCount=linqInvocationCountSnapshot;
                    staticMethods=staticMethodsSnapshot;
                    builder.Length=builderLengthSnapshot;
                    return false;
                }
            }
        }

        generatedFragments.Add(fragment);
        staticMethods=静的メソッドコード連結(staticMethods,invocationStaticMethods);
        if(materializeInlineComputation is not null) {
            staticMethods=静的メソッドコード連結(staticMethods,((Scalar)fragment).InlineSupportingMembers);
        } else if(fragment is Scalar&&!isScalarMaterializeTerminal) {
            staticMethods=静的メソッドコード連結(staticMethods,スカラー対応補助メンバー取得(fragment,tempName,scalarResultTypeName));
        } else {
            staticMethods=静的メソッドコード連結(staticMethods,fragment.GetEnumeratorStruct);
        }

        var consumerName = GetUniqueIdentifier($"consumer_{SanitizeIdentifier(tempName)}",usedNames);
        var consumerAssignment=MethodBodyRenderPlan.ConsumerAssignment(
            consumerName,
            RenderExpressionForGeneratedMethod(consumerExpression,semanticModel,containingType,unsafeAccessorCollector));
        if(fragment is Enumerable enumerableFragment) {
            AppendConsumeSetup(
                builder,
                MethodBodyRenderPlan.Statement(enumerableFragment.GetConsumePreparationStatement()),
                consumerAssignment,
                linqIsConsumeReceiver);
            MethodBodyRenderPlan.Statement(enumerableFragment.GetConsumeExecutionStatement(consumerName)).AppendTo(builder);
            return true;
        }

        if(fragment is Scalar&&!isScalarMaterializeTerminal) {
            MethodBodyRenderPlan scalarEvaluation;
            if(fragment is DirectExecutionFragment directExecutionFragment) {
                scalarEvaluation=MethodBodyRenderPlan.Statement(directExecutionFragment.GetDirectExecutionStatement(tempName,scalarResultTypeName));
            } else if(scalarInlineComputation is not null) {
                scalarEvaluation=MethodBodyRenderPlan.Statement(scalarInlineComputation);
            } else {
                var scalarEnumeratorName = GetUniqueIdentifier($"e_{SanitizeIdentifier(tempName)}",usedNames);
                scalarEvaluation=MethodBodyRenderPlan.ScalarRead(scalarResultTypeName,tempName,scalarEnumeratorName,scalarConsumeEnumeratorExpression!);
            }
            AppendConsumeSetup(builder,scalarEvaluation,consumerAssignment,linqIsConsumeReceiver);
            MethodBodyRenderPlan.Consume(consumerName,tempName).AppendTo(builder);
            return true;
        }

        // Consume へ流すだけの実体化で、作らずに要素を流しても同じになる形(Range の ToHashSet・恒等キーの ToDictionary)。
        // 列が受け手のときは、ソース式の評価が消費側の式より後になるので、消費側の式が評価順の観測できない形のときだけ。
        if(fragment is Scalar&&isScalarMaterializeTerminal
            &&fragment is IStreamingMaterializeConsume streamingMaterialize
            &&(!linqIsConsumeReceiver||IsEvaluationOrderFree(consumerExpression))
            &&streamingMaterialize.RenderStreamingConsume(consumerName) is { } streamingConsume) {
            AppendConsumeSetup(builder,MethodBodyRenderPlan.Statement(string.Empty),consumerAssignment,linqIsConsumeReceiver);
            MethodBodyRenderPlan.Statement(streamingConsume).AppendTo(builder);
            return true;
        }

        if(fragment is Scalar&&isScalarMaterializeTerminal&&fragment is DirectExecutionFragment materializeDirectExecutionFragment) {
            var resultTypeInfo = semanticModel.GetTypeInfo(linqInvocation);
            var resultTypeName = (resultTypeInfo.Type??resultTypeInfo.ConvertedType)?.ToDisplayString(完全修飾形式)
                ??"object";
            AppendConsumeSetup(
                builder,
                MethodBodyRenderPlan.Statement(materializeDirectExecutionFragment.GetDirectExecutionStatement(tempName,resultTypeName)),
                consumerAssignment,
                linqIsConsumeReceiver);
            MethodBodyRenderPlan.MaterializeLoop(tempName,consumerName).AppendTo(builder);
            return true;
        }

        if(fragment is Scalar&&isScalarMaterializeTerminal) {
            MethodBodyRenderPlan materializeEvaluation;
            if(materializeInlineComputation is not null) {
                materializeEvaluation=MethodBodyRenderPlan.Statement(materializeInlineComputation);
            } else {
                var scalarEnumeratorName = GetUniqueIdentifier($"e_{SanitizeIdentifier(tempName)}",usedNames);
                materializeEvaluation=MethodBodyRenderPlan.ScalarRead(materializeResultTypeName!,tempName,scalarEnumeratorName,materializeEnumeratorExpression!);
            }

            AppendConsumeSetup(builder,materializeEvaluation,consumerAssignment,linqIsConsumeReceiver);
            MethodBodyRenderPlan.MaterializeLoop(tempName,consumerName).AppendTo(builder);
            return true;
        }

        uniqueNumber=snapshot;
        linqInvocationCount=linqInvocationCountSnapshot;
        staticMethods=staticMethodsSnapshot;
        builder.Length=builderLengthSnapshot;
        return false;
    }

    private bool IsScalarConsumeMaterializeTerminal(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom??method;
        if(!IsSupportedLinqEnumerableMethod(definition)) {
            return false;
        }

        return definition.Name is "ToArray" or "ToList" or "ToHashSet" or "ToDictionary" or "ToLookup";
    }

    private bool TryGetDirectConsumeConsumerExpression(
        InvocationExpressionSyntax consumeInvocation,
        InvocationExpressionSyntax linqInvocation,
        out ExpressionSyntax consumerExpression,
        out bool linqIsConsumeReceiver) {
        consumerExpression=null!;
        linqIsConsumeReceiver=false;
        if(consumeInvocation.Expression is not MemberAccessExpressionSyntax memberAccess
            ||memberAccess.Name.Identifier.ValueText!="Consume"
            ||consumeInvocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }

        if(SyntaxExpressionMatches(memberAccess.Expression,linqInvocation)) {
            consumerExpression=consumeInvocation.ArgumentList.Arguments[0].Expression;
            linqIsConsumeReceiver=true;
            return true;
        }
        if(SyntaxExpressionMatches(consumeInvocation.ArgumentList.Arguments[0].Expression,linqInvocation)) {
            consumerExpression=memberAccess.Expression;
            linqIsConsumeReceiver=false;
            return true;
        }
        return false;
    }

    private static void AppendConsumeSetup(
        StringBuilder builder,
        MethodBodyRenderPlan linqEvaluation,
        MethodBodyRenderPlan consumerEvaluation,
        bool linqIsConsumeReceiver) {
        MethodBodyRenderPlan.Sequence(
            linqIsConsumeReceiver?linqEvaluation:consumerEvaluation,
            linqIsConsumeReceiver?consumerEvaluation:linqEvaluation)
            .AppendTo(builder);
    }

    private bool SyntaxExpressionMatches(ExpressionSyntax expression,ExpressionSyntax other) =>
        NormalizeExpressionText(StripConsumeParentheses(expression).ToString())==NormalizeExpressionText(StripConsumeParentheses(other).ToString());

    private bool TryGetConsumeSourceInvocation(
        InvocationExpressionSyntax consumeExpression,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax sourceInvocation) {
        sourceInvocation=null!;
        if(consumeExpression.Expression is not MemberAccessExpressionSyntax consumeMember
            ||consumeMember.Name.Identifier.ValueText!="Consume") {
            return false;
        }

        var receiverInvocation=StripConsumeParentheses(consumeMember.Expression) as InvocationExpressionSyntax;
        var argumentInvocation=consumeExpression.ArgumentList.Arguments.Count==1
            ? StripConsumeParentheses(consumeExpression.ArgumentList.Arguments[0].Expression) as InvocationExpressionSyntax
            : null;

        // A System LINQ invocation is unambiguous even when the other side is a factory call.
        if(receiverInvocation is not null&&IsSupportedLinqInvocation(receiverInvocation,semanticModel)) {
            sourceInvocation=receiverInvocation;
            return true;
        }
        if(argumentInvocation is not null&&IsSupportedLinqInvocation(argumentInvocation,semanticModel)) {
            sourceInvocation=argumentInvocation;
            return true;
        }

        // Preserve the existing single-invocation behavior. Ordinary transparent wrapper
        // methods (for example RangeX.Create) are recognized later by the root builder.
        if(receiverInvocation is null) {
            if(argumentInvocation is null) {
                return false;
            }
            sourceInvocation=argumentInvocation;
            return true;
        }
        if(argumentInvocation is null) {
            sourceInvocation=receiverInvocation;
            return true;
        }

        // Both Consume operands are calls. Select the one whose static result is enumerable;
        // this distinguishes an ordinary LINQ wrapper from a consumer factory without
        // assuming that the wrapper itself is declared in System.Linq.Enumerable.
        var receiverIsEnumerable=this.IsEnumerableConsumeSourceInvocation(receiverInvocation,semanticModel);
        var argumentIsEnumerable=this.IsEnumerableConsumeSourceInvocation(argumentInvocation,semanticModel);
        if(receiverIsEnumerable==argumentIsEnumerable) {
            return false;
        }
        sourceInvocation=receiverIsEnumerable?receiverInvocation:argumentInvocation;
        return true;
    }

    private bool IsEnumerableConsumeSourceInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) {
        var typeInfo=semanticModel.GetTypeInfo(invocation);
        return this.列挙要素型取得(typeInfo.Type??typeInfo.ConvertedType,out _,out _);
    }
    private ExpressionSyntax StripConsumeParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }
    /// <summary>
    /// 1 文を受け取り、対応するコードを <paramref name="builder"/> に生成する。
    /// <br/>文の種類に応じて専用メソッドへディスパッチする:
    /// <list type="bullet">
    ///   <item><description><c>Block</c> → 子文を再帰的に処理してブレースで囲む。</description></item>
    ///   <item><description><c>Try</c> → <see cref="Try文生成"/>。catch/finally ブロックも再帰処理する。</description></item>
    ///   <item><description><c>Using</c> → <see cref="Using文生成"/>。</description></item>
    ///   <item><description><c>LocalDeclaration</c> / <c>ExpressionStatement</c> → <see cref="LINQ呼び出し差し替え文生成"/> で LINQ 呼び出しを差し替え。</description></item>
    ///   <item><description><c>For</c> / <c>ForEach</c> / <c>While</c> / <c>Do</c> / <c>If</c> / <c>Switch</c> → 各専用生成メソッドへ。</description></item>
    ///   <item><description>その他の文 → <see cref="LINQ呼び出し差し替え文生成"/> にフォールバック。</description></item>
    /// </list>
    /// 1 文でも false を返した時点でメソッド全体の再構成を中止する。
    /// </summary>
    private bool 文を生成コードへ変換(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        switch(statement) {
            case BlockSyntax block:
                builder.AppendLine("{");
                foreach(var childStatement in block.Statements) {
                    if(!文を生成コードへ変換(childStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                        return false;
                    }
                }
                builder.AppendLine("}");
                return true;
            case TryStatementSyntax tryStatement:
                return Try文生成(tryStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case UsingStatementSyntax usingStatement:
                return Using文生成(usingStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case LocalDeclarationStatementSyntax localDeclaration:
                return LINQ呼び出し差し替え文生成(localDeclaration,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ExpressionStatementSyntax expressionStatement:
                return LINQ呼び出し差し替え文生成(expressionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ForStatementSyntax forStatement:
                return For文生成(forStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ForEachStatementSyntax foreachStatement:
                return ForEach文生成(foreachStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case WhileStatementSyntax whileStatement:
                return While文生成(whileStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case DoStatementSyntax doStatement:
                return Do文生成(doStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case IfStatementSyntax ifStatement:
                return If文生成(ifStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case SwitchStatementSyntax switchStatement:
                return Switch文生成(switchStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            default:
                return LINQ呼び出し差し替え文生成(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
        }
    }

    /// <summary>
    /// for/while/do/foreach/if/using などの本体文を生成する。
    /// <c>statement</c> が <see cref="BlockSyntax"/> でない場合、複数文への展開時に
    /// 親構文（for等）のヘッダ直後に裸の文が並んで構文エラーになるのを防ぐため、
    /// <c>{ }</c> で囲んでから <see cref="文を生成コードへ変換"/> に委譲する。
    /// 既に <see cref="BlockSyntax"/> の場合は <see cref="文を生成コードへ変換"/> 自身が <c>{ }</c> を出力するため、
    /// そのまま委譲する（二重 <c>{ }</c> を避ける）。
    /// </summary>
    private bool 本体文生成(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement is BlockSyntax) {
            return 文を生成コードへ変換(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
        }
        builder.AppendLine("{");
        if(!文を生成コードへ変換(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
            return false;
        }
        builder.AppendLine("}");
        return true;
    }

    /// <summary>
    /// <c>try { … } catch(…) { … } finally { … }</c> 文を生成する。
    /// try ブロック・各 catch ブロック・finally ブロックをそれぞれ <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// catch 宣言・catch フィルター (<c>when</c>) はソーステキストをそのまま出力する。
    /// </summary>
    private bool Try文生成(
        TryStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        builder.AppendLine("try");
        if(!文を生成コードへ変換(statement.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
            return false;
        }

        foreach(var catchClause in statement.Catches) {
            builder.Append("catch");
            if(catchClause.Declaration is not null) {
                builder.Append(catchClause.Declaration.ToString());
            }
            if(catchClause.Filter is not null) {
                builder.Append(catchClause.Filter.ToString());
            }
            builder.AppendLine();
            if(!文を生成コードへ変換(catchClause.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                return false;
            }
        }

        if(statement.Finally is not null) {
            builder.AppendLine("finally");
            if(!文を生成コードへ変換(statement.Finally.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// <c>using (declaration) { … }</c> または <c>using (expression) { … }</c> 文を生成する。
    /// 変数宣言がある場合は <see cref="変数宣言生成"/> で型名を完全修飾し、
    /// 式の場合は <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングする。
    /// 本体は <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// </summary>
    private bool Using文生成(
        UsingStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement.Declaration is not null) {
            builder.AppendLine($"using({変数宣言生成(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)})");
        } else if(statement.Expression is not null) {
            builder.AppendLine($"using({RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector)})");
        } else {
            return false;
        }
        return 本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>for (init; cond; incr) { … }</c> 文を生成する。
    /// 初期化部・条件部・増分部はそれぞれ <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 本体は <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// 初期化部が変数宣言の場合は <see cref="変数宣言生成"/> で完全修飾する。
    /// </summary>
    private bool For文生成(
        ForStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var init = statement.Declaration is not null
            ? 変数宣言生成(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)
            : string.Join(", ",statement.Initializers.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        var cond = statement.Condition is not null
            ? RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector)
            : string.Empty;
        var incr = string.Join(", ",statement.Incrementors.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        builder.AppendLine($"for({init}; {cond}; {incr})");
        return 本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>foreach (T x in expr) { … }</c> 文を生成する。
    /// 要素型をセマンティックモデルで解決して完全修飾名にする（<c>var</c> はそのまま残す）。
    /// 列挙対象式は <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングする。
    /// 本体は <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// </summary>
    private bool ForEach文生成(
        ForEachStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var typeSymbol = semanticModel.GetTypeInfo(statement.Type).Type;
        var typeName = typeSymbol is not null
            ? typeSymbol.ToDisplayString(完全修飾形式)
            : (statement.Type is IdentifierNameSyntax { IsVar: true } ? "var" : NormalizeExpressionText(statement.Type.ToString()));
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"foreach({typeName} {statement.Identifier} in {exprText})");
        return 本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>while (cond) { … }</c> 文を生成する。
    /// 条件式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 本体を <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// </summary>
    private bool While文生成(
        WhileStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"while({condText})");
        return 本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>do { … } while (cond);</c> 文を生成する。
    /// 本体を <see cref="文を生成コードへ変換"/> で先に再帰処理し、その後に条件式を出力する。
    /// </summary>
    private bool Do文生成(
        DoStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        builder.AppendLine("do");
        if(!本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"while({condText});");
        return true;
    }

    /// <summary>
    /// <c>if (cond) { … } else { … }</c> 文を生成する。
    /// 条件式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// then 節・else 節をそれぞれ <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// </summary>
    private bool If文生成(
        IfStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"if({condText})");
        if(!本体文生成(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        if(statement.Else is not null) {
            builder.AppendLine("else");
            if(!本体文生成(statement.Else.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        }
        return true;
    }

    /// <summary>
    /// <c>switch (expr) { case …: … }</c> 文を生成する。
    /// switch 対象式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 各 section のラベルはソーステキストをそのまま出力、
    /// 各 section の文は <see cref="文を生成コードへ変換"/> で再帰処理する。
    /// </summary>
    private bool Switch文生成(
        SwitchStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"switch({exprText})");
        builder.AppendLine("{");
        foreach(var section in statement.Sections) {
            foreach(var label in section.Labels) {
                builder.AppendLine(label.ToString());
            }
            foreach(var sectionStatement in section.Statements) {
                if(!文を生成コードへ変換(sectionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
            }
        }
        builder.AppendLine("}");
        return true;
    }

    /// <summary>
    /// 文中の LINQ 呼び出しを Fragment 実行コードに置き換えた等価な文を生成する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>クエリ式が含まれる場合は <see cref="TryLowerQueriesToExtensionMethods{TSyntax}"/> で拡張メソッドチェーンへ変換し、セマンティックモデルを差し替える。</description></item>
    ///   <item><description><c>ReturnStatement</c> の式がクエリ式なら <see cref="クエリ式本体生成"/> へ渡す。</description></item>
    ///   <item><description>文全体が単一 LINQ 呼び出しで戻り値を捨てる場合（列挙自体に副作用がない遅延評価）は文ごとスキップする。</description></item>
    ///   <item><description><see cref="外側LINQ呼び出し取得"/> で「ラムダ内部ではなく文直下の LINQ 呼び出し」を出現順に収集する。</description></item>
    ///   <item><description>各呼び出しに対して <see cref="呼び出し式からフラグメント生成"/> でFragment を生成し、<c>__linq0</c>, <c>__linq1</c>... という一時変数名に束縛するコードを出力する。</description></item>
    ///   <item><description>文のテキストを <c>replacementMap</c>（正規化済み LINQ 式テキスト → 一時変数名）で置換し、最終的な文を出力する。</description></item>
    ///   <item><description>匿名型をレシーバーに持つ LINQ 呼び出しは Fragment 化せず System.Linq に委譲するため、置換マップへの登録をスキップして続行する。</description></item>
    /// </list>
    /// </summary>
    private bool LINQ呼び出し差し替え文生成(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var originalStatementForQueryLowering = statement;
        if(TryLowerQueriesToExtensionMethods(statement,semanticModel,containingType,out var loweredStatement,out var loweredSemanticModel,out var loweredContainingType)) {
            statement=loweredStatement;
            semanticModel=loweredSemanticModel;
            containingType=loweredContainingType;
            builder.AppendLine(クエリLowering確認コメント生成(originalStatementForQueryLowering,loweredStatement));
        }
        if(this.state.Configuration.ConvergenceMode==LinqConvergenceMode.GlobalFixedPoint){
            if(TryOptimizeGlobalFixedPoint(statement,semanticModel,containingType,out var optimizedStatement,out var optimizedSemanticModel,out var optimizedContainingType)){
                statement=optimizedStatement;
                semanticModel=optimizedSemanticModel;
                containingType=optimizedContainingType;
            }
        }else if(TryOptimizeLegacy(statement,semanticModel,containingType,out var optimizedStatement,out var optimizedSemanticModel,out var optimizedContainingType)){
            statement=optimizedStatement;
            semanticModel=optimizedSemanticModel;
            containingType=optimizedContainingType;
        }
        if(TryRewriteTryGetNonEnumeratedCountStatement(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
            return true;
        }

        if(statement is ExpressionStatementSyntax consumeStatement
            &&consumeStatement.Expression is InvocationExpressionSyntax bareConsumeExpression
            &&TryGetConsumeSourceInvocation(bareConsumeExpression,semanticModel,out var consumeSourceInvocation)
            &&直接Consumeループ文生成(
                consumeStatement,
                consumeSourceInvocation,
                semanticModel,
                containingType,
                ref uniqueNumber,
                methodIsStatic,
                ref staticMethods,
                unsafeAccessorCollector,
                generatedFragments,
                builder,
                ref linqInvocationCount,
                usedNames)) {
            return true;
        }

        // ReturnStatement の式がクエリ式（括弧なしまたはあり）の場合は専用パスへ
        if(statement is ReturnStatementSyntax { Expression: { } retExpr }) {
            var retUnwrapped = retExpr;
            while(retUnwrapped is ParenthesizedExpressionSyntax retParen) {
                retUnwrapped=retParen.Expression;
            }
            if(retUnwrapped is QueryExpressionSyntax retQueryExpr) {
                return クエリ式本体生成(
                    retQueryExpr,semanticModel,containingType,ref uniqueNumber,
                    methodIsStatic,ref staticMethods,unsafeAccessorCollector,
                    generatedFragments,builder,ref linqInvocationCount,usedNames);
            }
        }
        // 式文の全体が単一 LINQ 呼び出しで結果が破棄される場合（e.g. Take(0).Select(...)）、
        // GetEnumerator() を呼ばないよう文ごとスキップする（元 LINQ の遅延評価と整合させる）。
        var outerLinqInvocations = 外側LINQ呼び出し取得(statement,semanticModel);
        if(statement is ExpressionStatementSyntax discardedExprStatement
            &&outerLinqInvocations.Count==1
            &&NormalizeExpressionText(discardedExprStatement.Expression.ToString())==NormalizeExpressionText(outerLinqInvocations[0].ToString())) {
            linqInvocationCount++;
            return true;
        }
        if(statement is ExpressionStatementSyntax expressionStatement
            &&expressionStatement.Expression is InvocationExpressionSyntax consumeExpression
            &&outerLinqInvocations.Count==1
            &&直接Consume式生成(
                consumeExpression,
                outerLinqInvocations[0],
                semanticModel,
                containingType,
                ref uniqueNumber,
                methodIsStatic,
                ref staticMethods,
                unsafeAccessorCollector,
                generatedFragments,
                builder,
                ref linqInvocationCount,
                usedNames)) {
            return true;
        }

        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var invocation in outerLinqInvocations) {
            var snapshot = uniqueNumber;
            var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
            if(!呼び出し式からフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                // 匿名型がソースに含まれる場合 → Fragment化できないが元の System.Linq 式を維持して続行する。
                // これにより匿名型を返す LINQ の結果に対してチェーンする操作は System.Linq にフォールバックする。
                return false;
            }
            if(!methodIsStatic) {
                fragment.ApplyRenderContext(fragment.RenderContext.WithContainingTypeName(containingType.ToDisplayString(完全修飾形式),this.state.ResolveOuterTypeParameters(containingType).ToImmutableArray()));
            }
            ApplyCounterInstrumentation(fragment,semanticModel,invocation);
            generatedFragments.Add(fragment);

            staticMethods=静的メソッドコード連結(staticMethods,invocationStaticMethods);
            var resultTypeName = semanticModel.GetTypeInfo(invocation).Type?.ToDisplayString(完全修飾形式)
                ??semanticModel.GetTypeInfo(invocation).ConvertedType?.ToDisplayString(完全修飾形式)
                ??"object";
            staticMethods=静的メソッドコード連結(staticMethods,スカラー対応補助メンバー取得(fragment,tempName,resultTypeName));
            if(!フラグメント実行文追加(builder,fragment,tempName,resultTypeName,usedNames)) {
                return false;
            }
            replacementMap[NormalizeExpressionText(invocation.ToString())]=tempName;
        }

        var generatedStatement = statement switch {
            LocalDeclarationStatementSyntax localDeclaration => ローカル変数宣言生成(localDeclaration,semanticModel,containingType,replacementMap,unsafeAccessorCollector),
            ExpressionStatementSyntax expressionStatement2 => $"{RenderExpressionForGeneratedMethod(expressionStatement2.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)};",
            _ => RenderStatementForGeneratedMethod(statement,replacementMap),
        };
        builder.AppendLine(generatedStatement);
        return true;
    }

    private bool TryRewriteTryGetNonEnumeratedCountStatement(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement is LocalDeclarationStatementSyntax {
            Declaration: {
                Variables: { Count: 1 } variables
            }
        }&&variables[0] is VariableDeclaratorSyntax { Initializer.Value: InvocationExpressionSyntax invocation } variable) {
            if(!TryGetNonEnumeratedCountInvocation(invocation,semanticModel,out var sourceExpression,out var outArgument,out var outTypeName)
                ||!TryRenderTryGetNonEnumeratedCountOutArgument(outArgument,semanticModel,containingType,unsafeAccessorCollector,outTypeName,out var outDeclaration,out var outTarget)
                ||!TryPrepareTryGetNonEnumeratedCountSource(sourceExpression,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,out var sourceFragment)) {
                return false;
            }

            if(!string.IsNullOrWhiteSpace(outDeclaration)) {
                builder.AppendLine(outDeclaration);
            }
            var resultName = variable.Identifier.ValueText;
            // Persisted/Tsavorite の直接型ルート(ProviderNativeRootBase)は Count プロパティで O(1) の
            // 件数を持つが、汎用 struct の TryGetNonEnumeratedCount はどのソースでも使える汎用実装のため
            // 常に false を返す。Tsavorite.HashSet<T> は ICollection<T> を実装しているため、素の
            // System.Linq.Enumerable.TryGetNonEnumeratedCount は true を返せる — 汎用 struct 経由に
            // 委ねると素の呼び出しより悪い(false になる)結果を返す退行になるため、ここで NativeCountExpression
            // を直接使う fast path へ分岐する。
            if(sourceFragment is ProviderNativeRootBase nativeRoot) {
                builder.AppendLine($"bool {resultName}=true;");
                var nativeCounterCall = CounterCallForGeneratedMethod(semanticModel,invocation,"IncrementTryGetNonEnumeratedCount");
                if(!string.IsNullOrEmpty(nativeCounterCall)) {
                    builder.AppendLine(nativeCounterCall);
                }
                builder.AppendLine($"{outTarget}=checked((int)({nativeRoot.NativeCountExpression}));");
                linqInvocationCount++;
                return true;
            }
            var enumeratorName = GetUniqueIdentifier($"__tryGetCount{linqInvocationCount}",usedNames);
            builder.AppendLine($"bool {resultName}=default;");
            builder.AppendLine($"using var {enumeratorName}={sourceFragment.GetEnumeratorExpression};");
            var counterCall = CounterCallForGeneratedMethod(semanticModel,invocation,"IncrementTryGetNonEnumeratedCount");
            if(!string.IsNullOrEmpty(counterCall)) {
                builder.AppendLine(counterCall);
            }
            builder.AppendLine($"{resultName}={enumeratorName}.TryGetNonEnumeratedCount(out {outTarget});");
            linqInvocationCount++;
            return true;
        }

        if(statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax expressionInvocation }) {
            if(!TryGetNonEnumeratedCountInvocation(expressionInvocation,semanticModel,out var sourceExpression,out var outArgument,out var outTypeName)
                ||!TryRenderTryGetNonEnumeratedCountOutArgument(outArgument,semanticModel,containingType,unsafeAccessorCollector,outTypeName,out var outDeclaration,out var outTarget)
                ||!TryPrepareTryGetNonEnumeratedCountSource(sourceExpression,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,out var sourceFragment)) {
                return false;
            }

            if(!string.IsNullOrWhiteSpace(outDeclaration)) {
                builder.AppendLine(outDeclaration);
            }
            if(sourceFragment is ProviderNativeRootBase nativeRoot) {
                var nativeCounterCall = CounterCallForGeneratedMethod(semanticModel,expressionInvocation,"IncrementTryGetNonEnumeratedCount");
                if(!string.IsNullOrEmpty(nativeCounterCall)) {
                    builder.AppendLine(nativeCounterCall);
                }
                builder.AppendLine($"{outTarget}=checked((int)({nativeRoot.NativeCountExpression}));");
                linqInvocationCount++;
                return true;
            }
            var enumeratorName = GetUniqueIdentifier($"__tryGetCount{linqInvocationCount}",usedNames);
            builder.AppendLine($"using var {enumeratorName}={sourceFragment.GetEnumeratorExpression};");
            var counterCall = CounterCallForGeneratedMethod(semanticModel,expressionInvocation,"IncrementTryGetNonEnumeratedCount");
            if(!string.IsNullOrEmpty(counterCall)) {
                builder.AppendLine(counterCall);
            }
            builder.AppendLine($"{enumeratorName}.TryGetNonEnumeratedCount(out {outTarget});");
            linqInvocationCount++;
            return true;
        }

        if(statement is ReturnStatementSyntax { Expression: InvocationExpressionSyntax returnInvocation }) {
            if(!TryGetNonEnumeratedCountInvocation(returnInvocation,semanticModel,out var sourceExpression,out var outArgument,out var outTypeName)
                ||!TryRenderTryGetNonEnumeratedCountOutArgument(outArgument,semanticModel,containingType,unsafeAccessorCollector,outTypeName,out var outDeclaration,out var outTarget)
                ||!TryPrepareTryGetNonEnumeratedCountSource(sourceExpression,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,out var sourceFragment)) {
                return false;
            }

            if(!string.IsNullOrWhiteSpace(outDeclaration)) {
                builder.AppendLine(outDeclaration);
            }
            if(sourceFragment is ProviderNativeRootBase nativeRoot) {
                var nativeCounterCall = CounterCallForGeneratedMethod(semanticModel,returnInvocation,"IncrementTryGetNonEnumeratedCount");
                if(!string.IsNullOrEmpty(nativeCounterCall)) {
                    builder.AppendLine(nativeCounterCall);
                }
                builder.AppendLine($"{outTarget}=checked((int)({nativeRoot.NativeCountExpression}));");
                builder.AppendLine("return true;");
                linqInvocationCount++;
                return true;
            }
            var enumeratorName = GetUniqueIdentifier($"__tryGetCount{linqInvocationCount}",usedNames);
            builder.AppendLine($"using var {enumeratorName}={sourceFragment.GetEnumeratorExpression};");
            var counterCall = CounterCallForGeneratedMethod(semanticModel,returnInvocation,"IncrementTryGetNonEnumeratedCount");
            if(!string.IsNullOrEmpty(counterCall)) {
                builder.AppendLine(counterCall);
            }
            builder.AppendLine($"return {enumeratorName}.TryGetNonEnumeratedCount(out {outTarget});");
            linqInvocationCount++;
            return true;
        }

        return false;
    }

    private bool TryPrepareTryGetNonEnumeratedCountSource(
        ExpressionSyntax sourceExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        out Enumerable sourceFragment) {
        var snapshot = uniqueNumber;
        if(!単独列挙ソースフラグメント生成(semanticModel,sourceExpression,containingType,ref uniqueNumber,out sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector)) {
            uniqueNumber=snapshot;
            return false;
        }

        if(!methodIsStatic) {
            sourceFragment.ApplyRenderContext(sourceFragment.RenderContext.WithContainingTypeName(containingType.ToDisplayString(完全修飾形式),this.state.ResolveOuterTypeParameters(containingType).ToImmutableArray()));
        }
        ApplyCounterInstrumentation(sourceFragment,semanticModel,sourceExpression);
        generatedFragments.Add(sourceFragment);
        staticMethods=静的メソッドコード連結(staticMethods,sourceStaticMethods);
        staticMethods=静的メソッドコード連結(staticMethods,sourceFragment.GetEnumeratorStruct);
        return true;
    }

    private bool TryGetNonEnumeratedCountInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out ExpressionSyntax sourceExpression,
        out ArgumentSyntax outArgument,
        out string outTypeName) {
        sourceExpression=null!;
        outArgument=null!;
        outTypeName=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom??method;
        if(definition.Name!="TryGetNonEnumeratedCount"
            ||definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        if(method.ReducedFrom is not null
            &&invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
            &&invocation.ArgumentList.Arguments.Count==1
            &&invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) {
            sourceExpression=receiver;
            outArgument=invocation.ArgumentList.Arguments[0];
            outTypeName=method.Parameters[0].Type.ToDisplayString(完全修飾形式);
            return true;
        }

        if(method.ReducedFrom is null
            &&invocation.ArgumentList.Arguments.Count==2
            &&invocation.ArgumentList.Arguments[1].RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) {
            sourceExpression=invocation.ArgumentList.Arguments[0].Expression;
            outArgument=invocation.ArgumentList.Arguments[1];
            outTypeName=method.Parameters[1].Type.ToDisplayString(完全修飾形式);
            return true;
        }

        return false;
    }

    private bool TryRenderTryGetNonEnumeratedCountOutArgument(
        ArgumentSyntax outArgument,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessorCollector,
        string outTypeName,
        out string declaration,
        out string target) {
        declaration=string.Empty;
        target=string.Empty;
        if(outArgument.Expression is DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax single }) {
            target=single.Identifier.ValueText;
            declaration=$"{outTypeName} {target};";
            return true;
        }

        target=RenderExpressionForGeneratedMethod(outArgument.Expression,semanticModel,containingType,unsafeAccessorCollector);
        return true;
    }

    private string CounterCallForGeneratedMethod(SemanticModel semanticModel,SyntaxNode node,string methodName) =>
        IsCounterInstrumentationEnabled(semanticModel,node)
            ? $"global::Seamless.Linq.LinqCounterScope.Counter.{methodName}();"
            : string.Empty;

    /// <summary>
    /// 式本体メソッド（<c>=> expr</c>）の LINQ 呼び出しを置き換え、値を返すメソッドでは <c>return <em>result</em>;</c>、<c>void</c> では式文を出力する。
    /// <br/><see cref="LINQ呼び出し差し替え文生成"/> の式専用版。
    /// <c>SyntaxFactory.ReturnStatement()</c> で合成ノードを作ると <c>SyntaxTree</c> が null になり
    /// <c>SemanticModel.GetSymbolInfo()</c> が失敗するため、元ツリーのノードをそのまま使う。
    /// <br/>Fragment 化結果が匿名型を含む場合は生成コードに不正な型名が埋まるため、
    /// その呼び出しはスナップショットに戻してスキップする（<see cref="LINQ呼び出し差し替え文生成"/> と同じ方針）。
    /// </summary>
    private bool LINQ呼び出し差し替え式本体生成(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        bool methodReturnsVoid,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var originalExpressionForQueryLowering = expression;
        if(TryLowerQueriesToExtensionMethods(expression,semanticModel,containingType,out var loweredExpression,out var loweredSemanticModel,out var loweredContainingType)) {
            expression=loweredExpression;
            semanticModel=loweredSemanticModel;
            containingType=loweredContainingType;
            builder.AppendLine(クエリLowering確認コメント生成(originalExpressionForQueryLowering,loweredExpression));
        }
        if(this.state.Configuration.ConvergenceMode==LinqConvergenceMode.GlobalFixedPoint){
            if(TryOptimizeGlobalFixedPoint(expression,semanticModel,containingType,out var optimizedExpression,out var optimizedSemanticModel,out var optimizedContainingType)){
                expression=optimizedExpression;
                semanticModel=optimizedSemanticModel;
                containingType=optimizedContainingType;
            }
        }else if(TryOptimizeLegacy(expression,semanticModel,containingType,out var optimizedExpression,out var optimizedSemanticModel,out var optimizedContainingType)){
            expression=optimizedExpression;
            semanticModel=optimizedSemanticModel;
            containingType=optimizedContainingType;
        }
        // 括弧を剥がしてクエリ式かどうかを判定する（例: (from v in Source() select v*2)）
        var unwrapped = expression;
        while(unwrapped is ParenthesizedExpressionSyntax paren) {
            unwrapped=paren.Expression;
        }
        if(unwrapped is QueryExpressionSyntax queryExpression) {
            return クエリ式本体生成(
                queryExpression,semanticModel,containingType,ref uniqueNumber,
                methodIsStatic,ref staticMethods,unsafeAccessorCollector,
                generatedFragments,builder,ref linqInvocationCount,usedNames);
        }

        var outerLinqInvocations = 外側LINQ呼び出し取得_式(expression,semanticModel);
        if(methodReturnsVoid
            &&expression is InvocationExpressionSyntax consumeExpression
            &&consumeExpression.Expression is MemberAccessExpressionSyntax consumeMember
            &&consumeMember.Name.Identifier.ValueText=="Consume"
            &&TryGetConsumeSourceInvocation(consumeExpression,semanticModel,out var consumeSourceInvocation)) {
            if(consumeSourceInvocation is not null
                &&直接Consume式生成(
                    consumeExpression,
                    consumeSourceInvocation,
                    semanticModel,
                    containingType,
                    ref uniqueNumber,
                    methodIsStatic,
                    ref staticMethods,
                    unsafeAccessorCollector,
                    generatedFragments,
                    builder,
                    ref linqInvocationCount,
                    usedNames)) {
                return true;
            }
        }
        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var invocation in outerLinqInvocations) {
            var snapshot = uniqueNumber;
            var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
            if(!呼び出し式からフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                linqInvocationCount--;
                return false;
            }
            // fragment 生成が成功しても結果型が匿名型を含む場合は struct コードに不正な型名が埋まるためスキップ。
            // uniqueNumber をスナップショットに戻してカウントも戻す。
            var invResultType = semanticModel.GetTypeInfo(invocation).Type;
            if(invResultType is not null&&ContainsAnonymousType(invResultType)) {
                uniqueNumber=snapshot;
                linqInvocationCount--;
                continue;
            }
            if(!methodIsStatic) {
                fragment.ApplyRenderContext(fragment.RenderContext.WithContainingTypeName(containingType.ToDisplayString(完全修飾形式),this.state.ResolveOuterTypeParameters(containingType).ToImmutableArray()));
            }
            ApplyCounterInstrumentation(fragment,semanticModel,invocation);
            generatedFragments.Add(fragment);
            staticMethods=静的メソッドコード連結(staticMethods,invocationStaticMethods);
            var resultTypeName = semanticModel.GetTypeInfo(invocation).Type?.ToDisplayString(完全修飾形式)
                ??semanticModel.GetTypeInfo(invocation).ConvertedType?.ToDisplayString(完全修飾形式)
                ??"object";
            staticMethods=静的メソッドコード連結(staticMethods,スカラー対応補助メンバー取得(fragment,tempName,resultTypeName));
            if(!フラグメント実行文追加(builder,fragment,tempName,resultTypeName,usedNames)) {
                return false;
            }
            replacementMap[NormalizeExpressionText(invocation.ToString())]=tempName;
        }

        var rendered = RenderExpressionForGeneratedMethod(
            expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        // RenderStatementForGeneratedMethod と同じ後処理：this. → @this. への変換
        rendered=rendered
            .Replace("this.","@this.",StringComparison.Ordinal);
        builder.AppendLine(methodReturnsVoid ? $"{rendered};" : $"return {rendered};");
        return true;
    }

    /// <summary>
    /// クエリ式（<c>from … where … select …</c>）を拡張メソッドチェーン（<c>.Where(…).Select(…)</c>）へ
    /// 変換（Lowering）し、変換後ノードのセマンティックモデルと含む型シンボルを返す。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>ノードにクエリ式が含まれなければ false を即返す。</description></item>
    ///   <item><description><c>OptimizeVisitor.RewriteQueriesToExtensionMethods</c> で構文木を書き換える。等価なら false。</description></item>
    ///   <item><description>書き換え済みノードに <c>TsavoriteQueryLowering</c> アノテーションを付与し、元の構文ツリーを新ツリーで置き換えた新 <see cref="Microsoft.CodeAnalysis.Compilation"/> を作成する。</description></item>
    ///   <item><description>新ツリーからアノテーション付きノードを探し、そのセマンティックモデルと包含型シンボルを out パラメータで返す。</description></item>
    /// </list>
    /// 成功時は呼び出し元が <c>statement</c> / <c>semanticModel</c> / <c>containingType</c> を差し替えて続行する。
    /// </summary>
    private bool TryLowerQueriesToExtensionMethods<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax loweredNode,
        out SemanticModel loweredSemanticModel,
        out INamedTypeSymbol loweredContainingType)
        where TSyntax : SyntaxNode {
        loweredNode=node;
        loweredSemanticModel=semanticModel;
        loweredContainingType=containingType;

        if(!node.DescendantNodesAndSelf().OfType<QueryExpressionSyntax>().Any()) {
            return false;
        }

        var rewritten = OptimizeVisitor.RewriteQueriesToExtensionMethods(node);
        return TryRebindRewritten(node,rewritten,semanticModel,containingType,"TsavoriteQueryLowering",
            out loweredNode,out loweredSemanticModel,out loweredContainingType);
    }

    /// <summary>
    /// <see cref="AlgebraicRewriter"/> で §1 代数的書換（Where(p).Op()→Op(p)、OrderBy(k).First()→MinBy(k)、
    /// Count()&gt;0→Any()、恒等 Select / 冗長 AsEnumerable 除去、連続 Where の選択率順入替）を適用する。
    /// 合成書換（Count()&gt;0→Any() の後の Where(p).Any()→Any(p) 等）を取りこぼさないよう、変化が無くなるまで
    /// 「書換→再束縛」を繰り返す（拡張メソッド形の Enumerable 呼び出しのみが対象なので静的 SystemEnumerable 参照は不変）。
    /// </summary>
    private bool TryOptimizeLegacy<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax optimizedNode,
        out SemanticModel optimizedSemanticModel,
        out INamedTypeSymbol optimizedContainingType)
        where TSyntax:SyntaxNode{
        optimizedNode=node;
        optimizedSemanticModel=semanticModel;
        optimizedContainingType=containingType;
        var changed=false;
        if(TryRewriteScalarAlgebra(optimizedNode,optimizedSemanticModel,optimizedContainingType,out var algebraNode,out var algebraModel,out var algebraType)){
            optimizedNode=algebraNode;
            optimizedSemanticModel=algebraModel;
            optimizedContainingType=algebraType;
            changed=true;
        }
        if(TryPushDownPredicates(optimizedNode,optimizedSemanticModel,optimizedContainingType,out var pushedNode,out var pushedModel,out var pushedType)){
            optimizedNode=pushedNode;
            optimizedSemanticModel=pushedModel;
            optimizedContainingType=pushedType;
            changed=true;
        }
        if(TryMergeProjections(optimizedNode,optimizedSemanticModel,optimizedContainingType,out var mergedNode,out var mergedModel,out var mergedType)){
            optimizedNode=mergedNode;
            optimizedSemanticModel=mergedModel;
            optimizedContainingType=mergedType;
            changed=true;
            if(TryRewriteScalarAlgebra(optimizedNode,optimizedSemanticModel,optimizedContainingType,out var tidiedNode,out var tidiedModel,out var tidiedType)){
                optimizedNode=tidiedNode;
                optimizedSemanticModel=tidiedModel;
                optimizedContainingType=tidiedType;
            }
        }
        return changed;
    }
    private readonly record struct SyntaxOptimizationState<TSyntax>(
        TSyntax Node,
        SemanticModel SemanticModel,
        INamedTypeSymbol ContainingType)
        where TSyntax:SyntaxNode;

    private bool TryOptimizeGlobalFixedPoint<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax optimizedNode,
        out SemanticModel optimizedSemanticModel,
        out INamedTypeSymbol optimizedContainingType)
        where TSyntax:SyntaxNode{
        var initial=new SyntaxOptimizationState<TSyntax>(node,semanticModel,containingType);
        var trace=this.state.Trace;
        var runId=trace?.BeginRun(OptimizationLayer.Syntax)??0;
        var result=FixedPointRunner.Run(
            initial,
            (state,round)=>RunGlobalOptimizationRound(state,round,runId,trace),
            static state=>OptimizationTraceText.Syntax(state.Node.WithoutTrivia()));
        trace?.EndRun(runId,OptimizationLayer.Syntax,result.RoundCount,result.Termination);
        optimizedNode=result.State.Node;
        optimizedSemanticModel=result.State.SemanticModel;
        optimizedContainingType=result.State.ContainingType;
        return !SyntaxFactory.AreEquivalent(node,optimizedNode);
    }

    private FixedPointRoundResult<SyntaxOptimizationState<TSyntax>> RunGlobalOptimizationRound<TSyntax>(
        SyntaxOptimizationState<TSyntax> state,
        int round,
        int runId,
        OptimizationTraceCollector? trace)
        where TSyntax:SyntaxNode{
        var checkpoint=trace?.Checkpoint??0;
        var current=state;
        var changed=false;
        var completedPasses=0;

        if((this.state.Configuration.Optimizations&LinqOptimizations.Rewrite)!=0){
            completedPasses++;
            var rewritten=AlgebraicRewriter.Rewrite(current.Node,current.SemanticModel);
            if(!SyntaxFactory.AreEquivalent(current.Node,rewritten)){
                if(!TryRebindRewritten(current.Node,rewritten,current.SemanticModel,current.ContainingType,"TsavoriteGlobalAlgebraicRewrite",out var nextNode,out var nextModel,out var nextType)){
                    trace?.Rollback(checkpoint);
                    return FixedPointRoundResult<SyntaxOptimizationState<TSyntax>>.RebindFailure(state);
                }
                trace?.RecordSyntaxChanges(runId,"AlgebraicRewriter",GetAlgebraicRuleId(current.Node,nextNode),round,1,current.Node,nextNode);
                current=new(nextNode,nextModel,nextType);
                changed=true;
            }
        }

        if((this.state.Configuration.Optimizations&LinqOptimizations.PredicatePushdown)!=0){
            completedPasses++;
            var rewritten=OptimizeVisitor.Optimize(current.Node,current.SemanticModel);
            if(!SyntaxFactory.AreEquivalent(current.Node,rewritten)){
                if(!TryRebindRewritten(current.Node,rewritten,current.SemanticModel,current.ContainingType,"TsavoriteGlobalPredicatePushdown",out var nextNode,out var nextModel,out var nextType)){
                    trace?.Rollback(checkpoint);
                    return FixedPointRoundResult<SyntaxOptimizationState<TSyntax>>.RebindFailure(state);
                }
                trace?.RecordSyntaxChanges(runId,"OptimizeVisitor","Optimize.PredicatePushdown",round,2,current.Node,nextNode);
                current=new(nextNode,nextModel,nextType);
                changed=true;
            }
        }

        if((this.state.Configuration.Optimizations&LinqOptimizations.ProjectionMerge)!=0){
            completedPasses++;
            var rewritten=ProjectionMergeVisitor.Merge(current.Node,current.SemanticModel);
            if(!SyntaxFactory.AreEquivalent(current.Node,rewritten)){
                if(!TryRebindRewritten(current.Node,rewritten,current.SemanticModel,current.ContainingType,"TsavoriteGlobalProjectionMerge",out var nextNode,out var nextModel,out var nextType)){
                    trace?.Rollback(checkpoint);
                    return FixedPointRoundResult<SyntaxOptimizationState<TSyntax>>.RebindFailure(state);
                }
                trace?.RecordSyntaxChanges(runId,"ProjectionMergeVisitor","Projection.Merge",round,3,current.Node,nextNode);
                current=new(nextNode,nextModel,nextType);
                changed=true;
            }
        }

        for(var pass=0;pass<completedPasses;pass++){
            trace?.MarkPass();
        }
        if(changed){
            return FixedPointRoundResult<SyntaxOptimizationState<TSyntax>>.ChangedState(current);
        }
        return FixedPointRoundResult<SyntaxOptimizationState<TSyntax>>.Unchanged(current);
    }
    private string GetAlgebraicRuleId(SyntaxNode before,SyntaxNode after){
        var beforeText=before.ToString();
        var afterText=after.ToString();
        if((beforeText.Contains(".Count(",StringComparison.Ordinal)
                ||beforeText.Contains(".LongCount(",StringComparison.Ordinal))
            &&afterText.Contains(".Any(",StringComparison.Ordinal)){
            return "Algebraic.CountComparisonToAny";
        }
        if(beforeText.Contains(".Where(",StringComparison.Ordinal)
            &&afterText.Contains(".Any(",StringComparison.Ordinal)){
            return "Algebraic.WhereTerminalFusion";
        }
        if(beforeText.Contains(".Select(",StringComparison.Ordinal)){
            return "Algebraic.IdentitySelectRemoval";
        }
        if(beforeText.Contains(".Reverse(",StringComparison.Ordinal)){
            return "Algebraic.Reverse";
        }
        return "Algebraic.Rewrite";
    }
    private bool TryRewriteScalarAlgebra<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax rewrittenNode,
        out SemanticModel rewrittenSemanticModel,
        out INamedTypeSymbol rewrittenContainingType)
        where TSyntax : SyntaxNode {
        rewrittenNode=node;
        rewrittenSemanticModel=semanticModel;
        rewrittenContainingType=containingType;
        var changed = false;
        for(var iteration = 0;iteration<8;iteration++) {
            if((this.state.Configuration.Optimizations&LinqOptimizations.Rewrite)==0) {
                break;
            }
            var rewritten = AlgebraicRewriter.Rewrite(rewrittenNode,rewrittenSemanticModel);
            if(!TryRebindRewritten(rewrittenNode,rewritten,rewrittenSemanticModel,rewrittenContainingType,"TsavoriteAlgebraicRewrite",
                out var nextNode,out var nextModel,out var nextType)) {
                break;
            }
            rewrittenNode=nextNode;
            rewrittenSemanticModel=nextModel;
            rewrittenContainingType=nextType;
            changed=true;
        }
        return changed;
    }

    /// <summary>
    /// <see cref="OptimizeVisitor"/> で Where を葉へ押し下げる（Select/SelectMany の carrier 逆解析、
    /// Join/LeftJoin の片側依存 predicate 移動と AND 連言分割、Distinct/OrderBy/Concat 等の前段挿入）。
    /// <see cref="OptimizeVisitor"/> は合成ノードに意味論ゲートを掛けられないため 1 パスにつき経路ごと 1 書換しか行わない。
    /// 取りこぼしを無くすよう、変化が無くなるまで「書換→再束縛」を繰り返す。
    /// </summary>
    private bool TryPushDownPredicates<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax pushedNode,
        out SemanticModel pushedSemanticModel,
        out INamedTypeSymbol pushedContainingType)
        where TSyntax : SyntaxNode {
        pushedNode=node;
        pushedSemanticModel=semanticModel;
        pushedContainingType=containingType;
        var changed = false;
        for(var iteration = 0;iteration<8;iteration++) {
            if((this.state.Configuration.Optimizations&LinqOptimizations.PredicatePushdown)==0) {
                break;
            }
            var rewritten = OptimizeVisitor.Optimize(pushedNode,pushedSemanticModel);
            if(!TryRebindRewritten(pushedNode,rewritten,pushedSemanticModel,pushedContainingType,"TsavoritePredicatePushdown",
                out var nextNode,out var nextModel,out var nextType)) {
                break;
            }
            pushedNode=nextNode;
            pushedSemanticModel=nextModel;
            pushedContainingType=nextType;
            changed=true;
        }
        return changed;
    }

    /// <summary>
    /// <see cref="ProjectionMergeVisitor"/> で射影を直下の selector / result selector へ吸収する
    /// （Select(f).Select(g)→Select(g∘f)、Join/SelectMany の result selector と後続 Select の融合）。
    /// 述語プッシュダウンより後に走らせること。Join(..).Where(..).Select(..) は Join と Select が
    /// 隣接していないため、Where を葉へ退けた後でないと透過 carrier が消えない。
    /// <see cref="ProjectionMergeVisitor"/> も 1 パスにつき経路ごと 1 書換しか行わないので、
    /// 変化が無くなるまで「書換→再束縛」を繰り返す。1 書換ごとに Select 呼び出しが 1 個減るので必ず止まる。
    /// </summary>
    private bool TryMergeProjections<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax mergedNode,
        out SemanticModel mergedSemanticModel,
        out INamedTypeSymbol mergedContainingType)
        where TSyntax : SyntaxNode {
        mergedNode=node;
        mergedSemanticModel=semanticModel;
        mergedContainingType=containingType;
        var changed = false;
        for(var iteration = 0;iteration<8;iteration++) {
            if((this.state.Configuration.Optimizations&LinqOptimizations.ProjectionMerge)==0) {
                break;
            }
            var rewritten = ProjectionMergeVisitor.Merge(mergedNode,mergedSemanticModel);
            if(!TryRebindRewritten(mergedNode,rewritten,mergedSemanticModel,mergedContainingType,"TsavoriteProjectionMerge",
                out var nextNode,out var nextModel,out var nextType)) {
                break;
            }
            mergedNode=nextNode;
            mergedSemanticModel=nextModel;
            mergedContainingType=nextType;
            changed=true;
        }
        return changed;
    }

    /// <summary>
    /// 書き換え済みノード <paramref name="rewritten"/> を元ツリーへ差し込み、新 <see cref="Compilation"/> を作って
    /// セマンティックモデルと包含型を取り直す共通処理。<paramref name="node"/> と等価なら false を返し、out は元の値のまま。
    /// </summary>
    private bool TryRebindRewritten<TSyntax>(
        TSyntax node,
        SyntaxNode rewritten,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        string annotationKind,
        out TSyntax reboundNode,
        out SemanticModel reboundSemanticModel,
        out INamedTypeSymbol reboundContainingType)
        where TSyntax : SyntaxNode {
        reboundNode=node;
        reboundSemanticModel=semanticModel;
        reboundContainingType=containingType;
        if(SyntaxFactory.AreEquivalent(node,rewritten)) {
            return false;
        }

        var annotation = new SyntaxAnnotation(annotationKind);
        var annotated = rewritten.WithAdditionalAnnotations(annotation);
        var root = node.SyntaxTree.GetRoot();
        var newRoot = root.ReplaceNode(node,annotated);
        var newTree = newRoot.SyntaxTree;
        var newCompilation = semanticModel.Compilation.ReplaceSyntaxTree(node.SyntaxTree,newTree);
        var newModel = newCompilation.GetSemanticModel(newTree);
        var annotatedNode = newTree.GetRoot()
            .GetAnnotatedNodes(annotation)
            .OfType<TSyntax>()
            .SingleOrDefault();
        if(annotatedNode is null) {
            return false;
        }

        var typeDeclaration = annotatedNode.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if(typeDeclaration is null||newModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol newContainingType) {
            return false;
        }

        reboundNode=annotatedNode;
        reboundSemanticModel=newModel;
        reboundContainingType=newContainingType;
        return true;
    }

    private string クエリLowering確認コメント生成(SyntaxNode original,SyntaxNode lowered) =>
        $"// Tsavorite query lowered: {NormalizeExpressionText(original.ToString())}=>{NormalizeExpressionText(lowered.ToString())}";

    // クエリ式を直接 Fragment チェーンへ変換し "return <result>;" を出力する旧フォールバック。
    // 通常は TryLowerQueriesToExtensionMethods でクエリ式を拡張メソッドチェーンへ下げてから既存の LINQ パスで処理する。
    private bool クエリ式本体生成(
        QueryExpressionSyntax queryExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var fromClause = queryExpression.FromClause;
        var body = queryExpression.Body;

        // let / join は未対応（フォールバック）。group は SelectOrGroup で別途チェック。
        if(body.Clauses.Any(static c => c is LetClauseSyntax or JoinClauseSyntax)) {
            return false;
        }
        if(body.SelectOrGroup is GroupClauseSyntax) {
            return false;
        }

        var paramName = fromClause.Identifier.ValueText;
        var sourceExpression = fromClause.Expression;

        // Source fragment を構築
        if(!単独列挙ソースフラグメント生成(semanticModel,sourceExpression,containingType,
            ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector)) {
            return false;
        }
        staticMethods=静的メソッドコード連結(staticMethods,sourceStaticMethods);
        Enumerable currentFragment = sourceFragment;

        // Where 節
        foreach(var clause in body.Clauses.OfType<WhereClauseSyntax>()) {
            var predBody = RenderExpressionForGeneratedMethod(
                clause.Condition,semanticModel,containingType,unsafeAccessorCollector);
            var whereFunc = new LambdaFunc {
                InlineBody=predBody,
                ParameterNames=new[] { paramName },
                ParameterTypeNames=new[] { currentFragment.ElementType },
                ReturnTypeName="bool",
                CanInlineAssign=false,
                CanGenerateMethod=true,
            };
            if(!OperatorRegistry.TryCreateDirect("Where",string.Empty,new DirectFragmentArguments(uniqueNumber++,new object?[] { currentFragment,whereFunc }),out var whereDirect)
                ||whereDirect is not Enumerable whereFragment) {
                return false;
            }
            ApplyCounterInstrumentation(whereFragment,semanticModel,clause);
            staticMethods=静的メソッドコード連結(staticMethods,whereFragment.GetEnumeratorStruct);
            currentFragment=whereFragment;
        }

        // OrderBy / ThenBy 節
        foreach(var clause in body.Clauses.OfType<OrderByClauseSyntax>()) {
            var orderings = clause.Orderings.ToList();
            for(var i = 0;i<orderings.Count;i++) {
                var ordering = orderings[i];
                var keyBody = RenderExpressionForGeneratedMethod(
                    ordering.Expression,semanticModel,containingType,unsafeAccessorCollector);
                var keyTypeName = semanticModel.GetTypeInfo(ordering.Expression).Type?
                    .ToDisplayString(完全修飾形式)??"object";
                var orderFunc = new LambdaFunc {
                    InlineBody=keyBody,
                    ParameterNames=new[] { paramName },
                    ParameterTypeNames=new[] { currentFragment.ElementType },
                    ReturnTypeName=keyTypeName,
                    CanInlineAssign=false,
                    CanGenerateMethod=true,
                };
                var isDescending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                var obMethodName = i==0
                    ? (isDescending ? "OrderByDescending" : "OrderBy")
                    : (isDescending ? "ThenByDescending" : "ThenBy");
                if(!OperatorRegistry.TryCreateDirect(obMethodName,string.Empty,new DirectFragmentArguments(uniqueNumber++,new object?[] { currentFragment,orderFunc }),out var orderByDirect)
                    ||orderByDirect is not Enumerable obFragment) {
                    return false;
                }
                ApplyCounterInstrumentation(obFragment,semanticModel,ordering);
                staticMethods=静的メソッドコード連結(staticMethods,obFragment.GetEnumeratorStruct);
                currentFragment=obFragment;
            }
        }

        // Select 節（identity select は不要）
        if(body.SelectOrGroup is SelectClauseSyntax selectClause) {
            var selectExpr = selectClause.Expression;
            var isIdentitySelect = selectExpr is IdentifierNameSyntax { Identifier.ValueText: var sid }&&sid==paramName;
            if(!isIdentitySelect) {
                var selectorBody = RenderExpressionForGeneratedMethod(
                    selectExpr,semanticModel,containingType,unsafeAccessorCollector);
                var resultTypeName2 = semanticModel.GetTypeInfo(selectExpr).Type?
                    .ToDisplayString(完全修飾形式)??"object";
                var selectFunc = new LambdaFunc {
                    InlineBody=selectorBody,
                    ParameterNames=new[] { paramName },
                    ParameterTypeNames=new[] { currentFragment.ElementType },
                    ReturnTypeName=resultTypeName2,
                    CanInlineAssign=true,
                    CanGenerateMethod=true,
                };
                if(!OperatorRegistry.TryCreateDirect("Select",string.Empty,new DirectFragmentArguments(uniqueNumber++,new object?[] { currentFragment,selectFunc }),out var selectDirect)
                    ||selectDirect is not Enumerable selectFrag) {
                    return false;
                }
                ApplyCounterInstrumentation(selectFrag,semanticModel,selectClause);
                staticMethods=静的メソッドコード連結(staticMethods,selectFrag.GetEnumeratorStruct);
                currentFragment=selectFrag;
            }
        }

        var tempName2 = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
        ApplyCounterInstrumentation(currentFragment,semanticModel,queryExpression);
        generatedFragments.Add(currentFragment);
        var returnTypeName = semanticModel.GetTypeInfo(queryExpression).Type?.ToDisplayString(完全修飾形式)
            ??semanticModel.GetTypeInfo(queryExpression).ConvertedType?.ToDisplayString(完全修飾形式)
            ??"object";
        if(!フラグメント実行文追加(builder,currentFragment,tempName2,returnTypeName,usedNames)) {
            return false;
        }
        builder.AppendLine($"return {tempName2};");
        return true;
    }
    /// <summary>
    /// <c>T x = expr, y = expr2;</c> のようなローカル変数宣言文を生成する。
    /// 型名はセマンティックモデルで解決して完全修飾し、初期化式は
    /// <see cref="RenderExpressionForGeneratedMethod"/> と <paramref name="replacementMap"/> で LINQ 一時変数に置換する。
    /// <c>var</c> はそのまま出力する。
    /// </summary>
    private string ローカル変数宣言生成(
        LocalDeclarationStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        IReadOnlyDictionary<string,string>? replacementMap = null,
        List<string>? unsafeAccessorCollector = null) {
        var variables = statement.Declaration.Variables.Select(variable => {
            if(variable.Initializer?.Value is null) {
                return variable.ToString();
            }
            return $"{variable.Identifier.ValueText}={RenderExpressionForGeneratedMethod(variable.Initializer.Value,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
        });
        var declType = statement.Declaration.Type;
        string typeName;
        if(declType is IdentifierNameSyntax { IsVar: true }) {
            typeName="var";
        } else {
            var typeSymbol = semanticModel.GetTypeInfo(declType).Type;
            typeName=typeSymbol is not null
                ? typeSymbol.ToDisplayString(完全修飾形式)
                : NormalizeExpressionText(declType.ToString());
        }
        return $"{typeName} {string.Join(", ",variables)};";
    }

    /// <summary>
    /// 文の直下にある「最も外側の」 LINQ 拡張メソッド呼び出しを出現順に列挙する。
    /// <br/>除外条件:
    /// <list type="bullet">
    ///   <item><description>ラムダ式の内部にある呼び出し（ラムダ内の LINQ は別の Fragment 生成パスで処理される）。</description></item>
    ///   <item><description>別の LINQ 呼び出しの内側にある呼び出し（メソッドチェーンの途中ノードは最外ノードとして扱う）。</description></item>
    ///   <item><description>静的呼び出し（<c>ReducedFrom == null</c>）および out/ref パラメータを持つメソッド。</description></item>
    /// </list>
    /// </summary>
    private List<InvocationExpressionSyntax> 外側LINQ呼び出し取得(StatementSyntax statement,SemanticModel semanticModel) {
        return statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            // 拡張メソッド呼び出し、または Range/Repeat/Empty root を対象とし、out/ref パラメータ持ちは除外する
            .Where(invocation => SystemLinqEnumerable呼び出し判定(invocation,semanticModel)
                &&semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
                &&(method.ReducedFrom is not null||IsKnownEnumerableRootMethod(method)||IsStaticSystemEnumerableLowerable(method))
                &&!(method.ReducedFrom??method).Parameters.Any(static p => p.RefKind!=RefKind.None))
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=statement)
                .OfType<LambdaExpressionSyntax>()
                .Any())
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=statement)
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestorInvocation => SystemLinqEnumerable呼び出し判定(ancestorInvocation,semanticModel)))
            // レシーバーが静的 System.Linq.Enumerable 呼び出しかつ Fragment 対応メソッドの場合は傍受しない。
            // Fragment 対応メソッドは拡張メソッド形式 source.X(...) でも呼ばれるため、
            // static 形式 Enumerable.X(source,...) は "正解値" として元コードのまま保持する。
            // Fragment 非対応メソッド（Repeat/Range/Empty 等）は DuckTypingRoot として展開を継続する。
            .Where(invocation =>
                !(invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                &&括弧除去(receiver) is InvocationExpressionSyntax receiverInvocation
                &&semanticModel.GetSymbolInfo(receiverInvocation).Symbol is IMethodSymbol { ReducedFrom: null } receiverMethod
                &&SystemLinqEnumerableメソッド判定(receiverMethod)
                &&!IsKnownEnumerableRootMethod(receiverMethod)
                &&Enumerableメソッドがフラグメント対応済みか(receiverMethod.Name)))
            .OrderBy(static invocation => invocation.SpanStart)
            .ToList();
    }
    /// <summary>
    /// 式本体版 <see cref="外側LINQ呼び出し取得"/>。
    /// <c>SyntaxFactory.ReturnStatement(アロー式)</c> のような合成ノードを避け、元ツリーのノードを直接 root として使う。
    /// <br/>境界を <c>expression.Parent</c> にする理由:
    /// expression（例: <c>.ToArray()</c>）自体が最外の LINQ 呼び出しであるとき、
    /// 祖先走査の境界を expression にすると expression 自身が TakeWhile から抜け落ち、
    /// その直下の <c>.Select()</c> が「外側に LINQ なし」と誤判定されて二重生成される。
    /// statement 版は statement が境界なので問題ない。
    /// </summary>
    private List<InvocationExpressionSyntax> 外側LINQ呼び出し取得_式(ExpressionSyntax expression,SemanticModel semanticModel) {
        // expression 自身が最外の LINQ 呼び出しになりうる（例: 式本体=>xxx.Where(...).Select(...).ToArray()）。
        // 祖先走査の境界を expression ではなく expression.Parent にしないと、expression(=.ToArray()) 自身が
        // TakeWhile から抜け落ち、その直下の .Select() が「外側に LINQ なし」と誤判定され __linq1 として
        // 二重生成される。statement 版は statement が境界なので問題ないが、式本体版はここで明示的に1段上げる。
        var boundary = expression.Parent;
        return expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => SystemLinqEnumerable呼び出し判定(invocation,semanticModel)
                &&semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
                &&(method.ReducedFrom is not null||IsKnownEnumerableRootMethod(method)||IsStaticSystemEnumerableLowerable(method))
                &&!(method.ReducedFrom??method).Parameters.Any(static p => p.RefKind!=RefKind.None))
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=boundary)
                .OfType<LambdaExpressionSyntax>()
                .Any())
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=boundary)
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestorInvocation => SystemLinqEnumerable呼び出し判定(ancestorInvocation,semanticModel)))
            // レシーバーが静的 System.Linq.Enumerable 呼び出しかつ Fragment 対応メソッドの場合は傍受しない。
            // Fragment 非対応メソッド（Repeat/Range/Empty 等）は DuckTypingRoot として展開を継続する。
            .Where(invocation =>
                !(invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                &&括弧除去(receiver) is InvocationExpressionSyntax receiverInvocation
                &&semanticModel.GetSymbolInfo(receiverInvocation).Symbol is IMethodSymbol { ReducedFrom: null } receiverMethod
                &&SystemLinqEnumerableメソッド判定(receiverMethod)
                &&!IsKnownEnumerableRootMethod(receiverMethod)
                &&Enumerableメソッドがフラグメント対応済みか(receiverMethod.Name)))
            .OrderBy(static invocation => invocation.SpanStart)
            .ToList();
    }
    /// <summary>
    /// 呼び出し式が <c>System.Linq.Enumerable</c> または <c>System.MemoryExtensions.Contains</c> の
    /// メソッドであるかを判定する。
    /// <c>MemoryExtensions.Contains</c> は <c>Span&lt;T&gt;</c>/<c>ReadOnlySpan&lt;T&gt;</c> の
    /// 拡張メソッドとして LINQ 的に使われるため対象に含める。
    /// </summary>
    private static bool IsStaticSystemEnumerableLowerable(IMethodSymbol method) =>
        method.ReducedFrom is null
        &&((method.Name=="Contains"&&method.Parameters.Length is 2 or 3)
            ||(method.Name=="Reverse"&&method.Parameters.Length==1))
        &&method.ContainingType.Name=="Enumerable"
        &&method.ContainingNamespace.ToDisplayString()=="System.Linq";

    private bool SystemLinqEnumerable呼び出し判定(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        return IsSupportedLinqInvocation(invocation,semanticModel);
    }
}
}
