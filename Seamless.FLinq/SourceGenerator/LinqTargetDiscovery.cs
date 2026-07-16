using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {

    private static Linq傍受探索結果? Linq傍受対象生成(GeneratorSyntaxContext context,CancellationToken cancellationToken) {
        //呼び出しである必要がある
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }

        if(TryCreateLocalMethodInterceptionTarget(context,invocation,method,cancellationToken,out var localMethodTarget,out var diagnostic)) {
            return new(localMethodTarget,null);
        }
        if(diagnostic is not null) {
            return new(null,diagnostic);
        }
        return null;
    }

    private static bool TryCreateLocalMethodInterceptionTarget(
        GeneratorSyntaxContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        CancellationToken cancellationToken,
        out Linq傍受対象型? target,
        out Diagnostic? diagnostic) {
        target=null;
        diagnostic=null;

        // 呼び出し側で型引数が確定済みのジェネリックメソッド呼び出し（例: Foo<int>(...)）も、
        // 未構築の定義（OriginalDefinition）に正規化してから以降の判定・生成を行う。
        // 生成される補助メソッドは元の型パラメーター名（例: TAnonymous）を持つジェネリックメソッドとして
        // 宣言され、呼び出し側の型引数はインターセプター経由でコンパイラが解決する。
        if(method.IsGenericMethod) {
            method=method.OriginalDefinition;
        }

        // インターセプト対象にする主条件は 2 つ:
        // 1. その場所が
        // 1-1.通常の関数呼び出し
        // 1-2.sealed override関数呼び出し
        // 1-3.sealed class のoverride関数であること
        // 2. 呼び出し先メソッドの内部に System.Linq.Enumerable 呼び出しがあること
        // それ以外の条件は、生成したインターセプターがコンパイル不能になったり、
        // 仮想呼び出しの動的ディスパッチを壊したりしないための安全制約として残す。
        //
        // ここで「Fragment に落とせる LINQ だけ」を対象に絞らないのが重要。
        // System.Linq.Enumerable を含むメソッドはまず全部 intercept target にし、
        // その後段で Fragment が見つからない/組み立てられない場合に
        // 生成インターセプターから明示的な例外を返すことで、未対応ケースを隠さない。
        if(method.MethodKind!=MethodKind.Ordinary
            // Partial method/ctor/operator などは現在のローカルメソッド再構成対象外。
            ||method.ContainingType is null
            // 所属型がないと interceptor 側で this/型名を再現できない。

            ||(!method.IsStatic&&(method.IsAbstract||method.IsVirtual||method.IsOverride))
            // 仮想呼び出しを intercept すると動的ディスパッチを壊すのでそのまま残す。
            ||ContainsGeneratedTypeAccessibilityRisk(method.ContainingType)
            // private/protected な入れ子型は生成コードから参照できずコンパイル不能になる。
            ||method.ContainingType.IsRefLikeType
            // ref struct を interceptor 引数/receiver として扱う経路は未対応。
            ||method.ContainingType.IsStatic
            // static クラスのメンバーは生成コードで型パラメータとして使用できないため除外。
            ||(!method.IsStatic&&method.ContainingType.IsValueType)
            // 値型 instance method は receiver のコピー/boxing 差異が大きいためまだ除外。
            ||method.DeclaringSyntaxReferences.Length==0
            // ソース本体がないメソッドは「内部に LINQ があるか」を判定できない。
            ||ContainsAnonymousType(method.ReturnType)
            // 匿名型戻り値は生成メソッドの署名に出せない。
            ||ContainsGeneratedTypeAccessibilityRisk(method.ReturnType)
            // 非公開戻り値型も生成コードから見えない。
            ||method.ReturnType.IsRefLikeType
            // ref-like 戻り値の forward/inline は寿命制約を満たしにくい。

            ||method.Parameters.Any(static parameter => parameter.RefKind!=RefKind.None)
            // ref/out/in は aliasing を壊しやすいのでいったん除外。
            ||method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))
            // 匿名型引数も署名へ出せない。
            ||method.Parameters.Any(static parameter => ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            // 非公開型引数は生成コードから参照できない。
            ||method.Parameters.Any(static parameter => parameter.Type.IsRefLikeType)) {
            // ref struct 引数は寿命制約を再現しにくい。
            return false;
        }

        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return false;
        }

        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return false;
        }

        var linqAnalysis = ローカルメソッドLinq解析取得(method,context.SemanticModel.Compilation);
        if(!linqAnalysis.HasEnumerableCall) {
            return false;
        }

        var optimizeLinq = LinqRewrite判定(method,context.SemanticModel.Compilation);
        var counterInstrumentation = LinqInstrumentation判定(method,context.SemanticModel.Compilation);
        if(!optimizeLinq&&!counterInstrumentation) {
            return false;
        }

        if(!全型パラメーター取得(method,out var allTypeParameters,out var signatureFailureReason)) {
            diagnostic=Diagnostic.Create(署名生成不能Descriptor,invocation.GetLocation(),method.Name,signatureFailureReason);
            return false;
        }

        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return false;
        }

        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return false;
        }

        var invocationLineSpan = invocation.GetLocation().GetLineSpan();
        var line = invocationLineSpan.StartLinePosition.Line+1;
        var character = invocationLineSpan.StartLinePosition.Character+1;
        var identifier = SanitizeIdentifier($"{method.Name}_{System.IO.Path.GetFileNameWithoutExtension(invocationLineSpan.Path)}_{containingMethod.Name}_{line}_{character}");
        var parameterSpecs = method.Parameters
            .Select(static parameter => new InterceptParameterSpec(parameter.Name,parameter.Type.ToDisplayString(完全修飾形式),parameter.RefKind))
            .ToArray();

        // 含有型がジェネリックなら、その型パラメーターを傍受メソッド（別クラスのジェネリック静的メソッド）へ移す。
        // これでレシーバ型 this Base<T> と要素型 T を生成側で名指しできる。
        // 型パラメーターの順序は「含有型 → メソッド自身」。制約（where）も同順で再現する。

        var methodTypeParameterNames = allTypeParameters.Length==0
            ? null
            : allTypeParameters.Select(static t => t.Name).ToArray();
        var typeParameterConstraintClause = 型パラメーター制約節生成(allTypeParameters);

        target=new Linq傍受対象型(
            identifier,
            containingMethod.Name,
            interceptsLocationAttributeText,
            method,
            method,
            invocation,
            containingMethod.ContainingType,
            context.SemanticModel,
            parameterSpecs,
            linqAnalysis,
            methodTypeParameterNames: methodTypeParameterNames,
            typeParameterConstraintClause: typeParameterConstraintClause,
            isLocalMethodInterception: true,
            optimizeLinq: optimizeLinq,
            counterInstrumentation: counterInstrumentation);
        return true;
    }

    // 外側から内側の含有型、最後にメソッド自身の順で型パラメーターを収集する。
    private static bool 全型パラメーター取得(
        IMethodSymbol method,
        out ImmutableArray<ITypeParameterSymbol> typeParameters,
        out string failureReason) {
        var typeStack = new Stack<INamedTypeSymbol>();
        for(var type = method.ContainingType;type is not null;type=type.ContainingType) {
            typeStack.Push(type);
        }

        var builder = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while(typeStack.Count>0) {
            var type = typeStack.Pop();
            foreach(var typeParameter in type.TypeParameters) {
                if(!型パラメーター追加(typeParameter,builder,names,out failureReason)) {
                    typeParameters=[];
                    return false;
                }
            }
        }
        foreach(var typeParameter in method.TypeParameters) {
            if(!型パラメーター追加(typeParameter,builder,names,out failureReason)) {
                typeParameters=[];
                return false;
            }
        }

        typeParameters=builder.ToImmutable();
        failureReason=string.Empty;
        return true;
    }

    private static bool 型パラメーター追加(
        ITypeParameterSymbol typeParameter,
        ImmutableArray<ITypeParameterSymbol>.Builder builder,
        HashSet<string> names,
        out string failureReason) {
        if(!names.Add(typeParameter.Name)) {
            failureReason=$"型パラメーター名 '{typeParameter.Name}' が外側と内側で重複しています";
            return false;
        }
        foreach(var constraint in typeParameter.ConstraintTypes) {
            if(ContainsGeneratedTypeAccessibilityRisk(constraint)) {
                failureReason=$"型パラメーター '{typeParameter.Name}' の制約型 '{constraint.ToDisplayString()}' を生成コードから参照できません";
                return false;
            }
        }
        builder.Add(typeParameter);
        failureReason=string.Empty;
        return true;
    }
    // 型パラメーターの where 制約節を生成する（例: " where T : notnull"）。
    private static string 型パラメーター制約節生成(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        var clauses = new List<string>();
        foreach(var typeParameter in typeParameters) {
            var parts = new List<string>();
            if(typeParameter.HasUnmanagedTypeConstraint) {
                parts.Add("unmanaged");
            } else if(typeParameter.HasValueTypeConstraint) {
                parts.Add("struct");
            } else if(typeParameter.HasReferenceTypeConstraint) {
                parts.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation==NullableAnnotation.Annotated ? "class?" : "class");
            }
            if(typeParameter.HasNotNullConstraint) {
                parts.Add("notnull");
            }
            foreach(var constraintType in typeParameter.ConstraintTypes) {
                parts.Add(constraintType.ToDisplayString(完全修飾形式));
            }
            if(typeParameter.HasConstructorConstraint) {
                parts.Add("new()");
            }
            if(parts.Count>0) {
                clauses.Add($"where {typeParameter.Name} : {string.Join(", ",parts)}");
            }
        }
        return clauses.Count==0 ? string.Empty : " "+string.Join(" ",clauses);
    }

    // [LinqIntercept(true/false)] によるLINQ最適化可否の判定。
    // メソッド > 入れ子クラス(内側から外側) > アセンブリ の順で最初に見つかった指定を採用し、
    // どこにも無ければ展開する(従来どおりのデフォルト挙動)。
    private static bool LinqRewrite判定(IMethodSymbol method,Compilation compilation) {
        if(TryGetLinqRewriteOverride(method.GetAttributes(),out var methodOverride)) {
            return methodOverride;
        }

        for(var type = method.ContainingType;type is not null;type=type.ContainingType) {
            if(TryGetLinqRewriteOverride(type.GetAttributes(),out var typeOverride)) {
                return typeOverride;
            }
        }

        if(TryGetLinqRewriteOverride(compilation.Assembly.GetAttributes(),out var assemblyOverride)) {
            return assemblyOverride;
        }

        return true;
    }

    private static bool TryGetLinqRewriteOverride(ImmutableArray<AttributeData> attributes,out bool shouldRewrite) {
        foreach(var attribute in attributes) {
            if(attribute.AttributeClass?.Name=="LinqInterceptAttribute") {
                // [LinqIntercept(true)]=展開、[LinqIntercept(false)]=非展開。
                shouldRewrite=attribute.ConstructorArguments.Length>0
                    &&attribute.ConstructorArguments[0].Value is bool intercept&&intercept;
                return true;
            }
        }
        shouldRewrite=default;
        return false;
    }

    private static bool LinqInstrumentation判定(IMethodSymbol method,Compilation compilation) {
        if(TryGetLinqInstrumentation(method.GetAttributes(),out var methodInstrumentation)) {
            return methodInstrumentation;
        }

        for(var type = method.ContainingType;type is not null;type=type.ContainingType) {
            if(TryGetLinqInstrumentation(type.GetAttributes(),out var typeInstrumentation)) {
                return typeInstrumentation;
            }
        }

        return TryGetLinqInstrumentation(compilation.Assembly.GetAttributes(),out var assemblyInstrumentation)
            &&assemblyInstrumentation;
    }

    private static LinqOptimizations LinqOptimizations判定(IMethodSymbol method,Compilation compilation) {
        if(TryGetLinqOptimizations(method.GetAttributes(),out var methodOptimizations)) {
            return methodOptimizations;
        }
        for(var type = method.ContainingType;type is not null;type=type.ContainingType) {
            if(TryGetLinqOptimizations(type.GetAttributes(),out var typeOptimizations)) {
                return typeOptimizations;
            }
        }
        return TryGetLinqOptimizations(compilation.Assembly.GetAttributes(),out var assemblyOptimizations)
            ? assemblyOptimizations
            : LinqOptimizations.All;
    }

    private static bool TryGetLinqOptimizations(ImmutableArray<AttributeData> attributes,out LinqOptimizations optimizations) {
        foreach(var attribute in attributes) {
            if(attribute.AttributeClass?.Name!="LinqInterceptAttribute") {
                continue;
            }
            foreach(var named in attribute.NamedArguments) {
                if(named.Key=="Optimizations"&&named.Value.Value is int value) {
                    optimizations=(LinqOptimizations)value;
                    return true;
                }
            }
        }
        optimizations=LinqOptimizations.All;
        return false;
    }

    private static bool TryGetLinqInstrumentation(ImmutableArray<AttributeData> attributes,out bool counterInstrumentation) {
        foreach(var attribute in attributes) {
            if(attribute.AttributeClass?.Name!="LinqInterceptAttribute") {
                continue;
            }

            foreach(var named in attribute.NamedArguments) {
                if(named.Key=="Instrumentation") {
                    counterInstrumentation=HasCounterInstrumentation(named.Value.Value);
                    return true;
                }
            }

            if(attribute.ConstructorArguments.Length>1) {
                counterInstrumentation=HasCounterInstrumentation(attribute.ConstructorArguments[1].Value);
                return true;
            }
        }

        counterInstrumentation=false;
        return false;
    }

    private static bool HasCounterInstrumentation(object? value) =>
        value switch {
            int intValue => (intValue&1)!=0,
            long longValue => (longValue&1L)!=0,
            _ => false,
        };

    /// <summary>
    /// <see cref="ローカルメソッドLinq解析実行"/> の結果を <see cref="Compilation"/> をキーにした
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> でキャッシュし、
    /// 同一 method の重複解析を防ぐ。
    /// キャッシュは <see cref="ConditionalWeakTable{TKey,TValue}"/> で Compilation ごとに管理されるため、
    /// インクリメンタルジェネレーターの世代をまたいでキャッシュが汚染されない。
    /// </summary>
    private static ローカルメソッドLinq解析結果 ローカルメソッドLinq解析取得(IMethodSymbol method,Compilation compilation) {
        var cache = ローカルメソッドLinq解析キャッシュ.GetValue(
            compilation,
            static _ => new System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>(SymbolEqualityComparer.Default));
        return cache.GetOrAdd(method,static (method,compilation) => ローカルメソッドLinq解析実行(method,compilation),compilation);
    }

    /// <summary>
    /// メソッドの構文ツリーを走査して System.Linq.Enumerable 呼び出しを解析する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="メソッド構文にLinq候補を含むか"/> で構文レベルの早期フィルタを行い、LINQ 候補がなければ即 <see cref="ローカルメソッドLinq解析結果.Empty"/> を返す。</description></item>
    ///   <item><description>セマンティックモデルで各 <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax"/> のシンボルを解決し、<see cref="SystemLinqEnumerableメソッド判定"/> が true のものだけを対象にする。</description></item>
    ///   <item><description>拡張メソッド呼び出し（<c>ReducedFrom != null</c>）のみを有効とし、静的呼び出しや out/ref パラメータ持ちは除外する。</description></item>
    ///   <item><description>Fragment 未対応メソッド名（<see cref="Enumerableメソッドがフラグメント対応済みか"/> が false）を <c>unsupportedMethods</c> に集積する。</description></item>
    ///   <item><description>戻り値・型引数・パラメータのいずれかに匿名型が含まれる呼び出しがあれば <c>hasAnonymousTypeLinq</c> を true にする。</description></item>
    ///   <item><description>LINQ 呼び出しが見つからなかった場合でも、クエリ式（<c>from...select</c> 等）があれば <c>hasEnumerableCall = true</c> とする。</description></item>
    /// </list>
    /// </summary>
    private static ローカルメソッドLinq解析結果 ローカルメソッドLinq解析実行(IMethodSymbol method,Compilation compilation) {
        if(method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return ローカルメソッドLinq解析結果.Empty;
        }

        if(!メソッド構文にLinq候補を含むか(methodSyntax)) {
            return ローカルメソッドLinq解析結果.Empty;
        }

        var semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        // 通常の block-bodied method と expression-bodied method の両方を見る。
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : [];

        var unsupportedMethods = new HashSet<string>(StringComparer.Ordinal);
        var hasEnumerableCall = false;
        var hasAnonymousTypeLinq = false;
        foreach(var invocation in invocationNodes) {
            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invokedMethod) {
                continue;
            }

            var definition = invokedMethod.ReducedFrom??invokedMethod;
            // target 判定では「Fragment 化できる LINQ か」ではなく
            // 「System.Linq.Enumerable を呼んでいるか」だけを見る。
            // これにより Cast/Append など未対応 or 一部未構築のケースも intercept され、
            // 後段で明示的な例外へ落とせる。
            if(!SystemLinqEnumerableメソッド判定(definition)) {
                continue;
            }

            // 静的呼び出し（SystemEnumerable.Method(source, ...)）は基本的に最適化対象外。
            // ただし Range/Repeat/Empty は「ソース生成 root」なので、Consume 直ループ等へ落とせる。
            if(invokedMethod.ReducedFrom is null
                &&!method.ReturnsVoid
                &&IsKnownEnumerableRootMethod(definition)) {
                continue;
            }
            if(invokedMethod.ReducedFrom is null
                &&!IsKnownEnumerableRootMethod(definition)
                &&definition.Name!="TryGetNonEnumeratedCount") {
                continue;
            }

            // out/ref パラメータを持つメソッド（TryGetNonEnumeratedCount 等）は
            // Fragment 化できないため解析対象外とする。
            if(definition.Parameters.Any(static p => p.RefKind!=RefKind.None)
                &&definition.Name!="TryGetNonEnumeratedCount") {
                continue;
            }

            hasEnumerableCall=true;
            if(!Enumerableメソッドがフラグメント対応済みか(definition.Name)) {
                unsupportedMethods.Add(definition.Name);
            }

            // 匿名型を含む LINQ 呼び出しがあれば警告フラグを立てる。
            // 戻り値型・型引数・パラメータのいずれかに匿名型が含まれていればアウト。
            if(!hasAnonymousTypeLinq) {
                if(ContainsAnonymousType(invokedMethod.ReturnType)
                    ||invokedMethod.TypeArguments.Any(ContainsAnonymousType)
                    ||invokedMethod.Parameters.Any(static p => ContainsAnonymousType(p.Type))) {
                    hasAnonymousTypeLinq=true;
                }
            }
        }

        // クエリ式（from ... select/where/orderby 等）も System.Linq.Enumerable 呼び出しとして扱う
        if(!hasEnumerableCall) {
            IEnumerable<SyntaxNode> queryBodyNodes = methodSyntax.Body is not null
                ? methodSyntax.Body.DescendantNodes()
                : methodSyntax.ExpressionBody is not null
                    ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf()
                    : [];
            if(queryBodyNodes.OfType<QueryExpressionSyntax>().Any()) {
                hasEnumerableCall=true;
            }
        }

        return new ローカルメソッドLinq解析結果(
            hasEnumerableCall,
            [.. unsupportedMethods.OrderBy(static methodName => methodName,StringComparer.Ordinal)],
            hasAnonymousTypeLinq);
    }

    /// <summary>
    /// セマンティックモデルを使わず構文だけで「メソッド本体内に System.Linq.Enumerable のメソッド名が出現するか」を判定する。
    /// <br/>セマンティック解析より安価なため、解析対象を絞る前処理として <see cref="ローカルメソッドLinq解析実行"/> の冒頭で呼ばれる。
    /// <br/>判定対象: メンバーアクセス（<c>source.Where(…)</c>）・識別子（<c>Where(…)</c>）・
    /// ジェネリックメソッド名（<c>Cast&lt;T&gt;(…)</c>）のいずれかが
    /// <see cref="SystemLinqEnumerableMethodNames"/> に含まれるか、またはクエリ式が存在するか。
    /// </summary>
    private static bool メソッド構文にLinq候補を含むか(MethodDeclarationSyntax methodSyntax) {
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : [];

        foreach(var invocation in invocationNodes) {
            if(invocation.Expression is MemberAccessExpressionSyntax { Name: { } memberName }
                &&SystemLinqEnumerableMethodNames.Contains(memberName.Identifier.ValueText)) {
                return true;
            }
            if(invocation.Expression is IdentifierNameSyntax identifierName
                &&SystemLinqEnumerableMethodNames.Contains(identifierName.Identifier.ValueText)) {
                return true;
            }
            if(invocation.Expression is GenericNameSyntax genericName
                &&SystemLinqEnumerableMethodNames.Contains(genericName.Identifier.ValueText)) {
                return true;
            }
        }

        // クエリ式（from ... select/where/orderby 等）も System.Linq.Enumerable 呼び出しに相当するため対象とする
        IEnumerable<SyntaxNode> bodyNodesForQuery = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf()
                : [];
        if(bodyNodesForQuery.OfType<QueryExpressionSyntax>().Any()) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// インターセプト対象の選定用。Fragment 化できるかどうかは問わず、
    /// メソッドの所属型が <c>System.Linq.Enumerable</c> であるかだけを判定する。
    /// <br/><c>method.ReducedFrom</c>（拡張メソッド解決前の定義）を優先して参照するため、
    /// 拡張メソッド呼び出し形式・静的呼び出し形式のどちらでも正しく判定できる。
    /// </summary>
    private static bool SystemLinqEnumerableメソッド判定(IMethodSymbol method) {
        return IsSupportedLinqEnumerableMethod(method);
    }

    private static 型対象型? 型対象生成(GeneratorSyntaxContext context) {
        if(context.Node is not TypeDeclarationSyntax declaration) {
            return null;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if(symbol is null) {
            return null;
        }

        var isMarker = symbol.Name=="DatabaseGenerator";
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isContainerCandidate = symbol.TypeKind==TypeKind.Class;
        if(!isMarker&&!isPartial&&!isContainerCandidate) {
            return null;
        }

        return new 型対象型(symbol,isPartial);
    }
}
