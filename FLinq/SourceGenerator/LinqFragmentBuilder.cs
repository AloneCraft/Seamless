using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Tsavorite.SourceGenerator.GeneratorHelpers;

using Tsavorite.SourceGenerator.Linq.Fragments;
using Enumerable = Tsavorite.SourceGenerator.Linq.Fragments.Enumerable;

namespace Tsavorite.SourceGenerator;

public sealed partial class TsavoriteIncrementalGenerator {

    // 呼び出し式から対応する Fragment を選び、再生成に必要な静的補助コードも回収する。
    /// <summary>
    /// source.Method(...)
    /// ↓
    /// Method に応じた Fragment 連結へ振り分ける
    /// 例:
    /// source.Any(x=>x > 0) → Where(x=>x > 0) → Any()
    /// source.Join(inner, outerKey, innerKey, result) → Join(...)
    /// </summary>
    private static bool 呼び出し式からフラグメント生成(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var methodName = definition.Name;

        if(TryBuildKnownEnumerableRootFromSystemInvocation(
            semanticModel,
            invocation,
            definition,
            containingType,
            ref uniqueNumber,
            out var rootFragment,
            unsafeAccessorCollector,
            replacementMap: null)) {
            fragment=rootFragment;
            return true;
        }

        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            if(methodName=="Any" && method.Parameters.Length==0) {
                return false;
            }
        }

        if(PipelineBuilder.TryBuild(
            semanticModel,
            invocation,
            ref uniqueNumber,
            out fragment,
            out staticMethods,
            new PipelineBuildServices(
                完全修飾形式,
                (ExpressionSyntax expression,ref int number,out Enumerable enumerable,out string methods) =>
                    列挙式フラグメント生成(semanticModel,expression,containingType,ref number,out enumerable,out methods,unsafeAccessorCollector),
                (ExpressionSyntax expression,ITypeSymbol delegateType,ref int number,out FuncFragment func) =>
                    関数式フラグメント生成(expression,delegateType,ref number,out func,semanticModel,containingType),
                関数フラグメントが利用可能か,
                expression=>RenderExpressionForGeneratedMethod(expression,semanticModel,containingType),
                (ITypeSymbol type,out ITypeSymbol elementType)=>{
                    if(列挙要素型取得(type,out elementType,out _)) {
                        return true;
                    }
                    elementType=null!;
                    return false;
                },
                ContainsAnonymousType,
                静的メソッドコード連結),
            unsafeAccessorCollector)) {
            return true;
        }

        // SelectMany selector の内部チェーンを struct にインライン化できる場合は専用パスへ。
        // レジストリ有効のまま試行することで内側チェーンの匿名型を record struct 化し、
        // 結果要素が匿名型なら SelectManyインライン化を試行 内の出口 Select で元の匿名型へ復元する。
        if(methodName=="SelectMany"
            && SelectManyインライン化を試行(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector)) {
            return true;
        }

        // 匿名型要素チェーン（TResult ファクトリ形）の ToList は要素型名を生成コードに書けないため、
        // Carrier の汎用 ToList へ型推論で委譲する直接実行文として生成する。
        // ソース構築はレジストリ有効のまま行い、内部の匿名型を record struct 置換できるようにする。
        if(methodName=="ToList"
            && invocation.ArgumentList.Arguments.Count==0
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var anonListSourceExpr }
            && ContainsAnonymousType(method.ReturnType)) {
            var anonListSnapshot = uniqueNumber;
            if(列挙式フラグメント生成(semanticModel,anonListSourceExpr,containingType,ref uniqueNumber,out var anonListSource,out var anonListStatics,unsafeAccessorCollector)
                && anonListSource.ElementType=="TResult") {
                fragment=new AnonymousExitToList(uniqueNumber++,anonListSource);
                staticMethods=anonListStatics;
                return true;
            }
            uniqueNumber=anonListSnapshot;
        }

        // ここから先は単発オペレーション等で「結果の匿名型がそのまま外へ逃げる」ケースが多い。
        // それらを record struct へ置換すると呼び出し元が期待する匿名型と型が合わなくなるため、
        // フォールバック経路では匿名型置換を抑止し、従来どおり匿名型を維持する。
        var anonSuspension = SuspendAnonRegistry();
        try {
            if(methodName=="Any") {
                return Any述語フラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
            }

            return 登録済みフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
        } finally {
            RestoreAnonRegistry(anonSuspension);
        }
    }

    /// <summary>
    /// source.Any(x=>x > 0)
    /// ↓
    /// Where(x=>x > 0) → Any()
    /// </summary>
    private static bool Any述語フラグメント生成(SemanticModel semanticModel,InvocationExpressionSyntax invocation,INamedTypeSymbol containingType,ref int uniqueNumber,out Fragment fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null)
       =>Any.TryBuildWithPredicate(
            semanticModel,invocation,containingType,ref uniqueNumber,
            out fragment,out staticMethods,unsafeAccessorCollector,
            列挙式フラグメント生成,
            関数式フラグメント生成,
            (name,variant,n,extraArgs)=>{
                if(!フラグメント候補選択(name,variant,out var c)) return null;
                return (Fragment?)c.Constructor.Invoke([n,..extraArgs]);
            });

    /// <summary>
    /// アセンブリ内の Fragment 継承クラスをリフレクションで全列挙し、
    /// 型名の命名規則に基づいて「LINQ メソッド名 → その overload/variant 群」の
    /// 逆引き辞書を構築する。
    ///
    /// 【命名規則】
    ///   Fragment 継承クラスの型名は "{MethodName}_{VariantName}" の形式。
    ///   例:
    ///     Aggregate_func          → MethodName="Aggregate", VariantName="func"
    ///     Aggregate_seed_func     → MethodName="Aggregate", VariantName="seed_func"
    ///     Where_indexPredicate    → MethodName="Where",     VariantName="indexPredicate"
    ///     Join                    → MethodName="Join",      VariantName="" (空文字)
    ///
    /// 【除外する型 (helperTypes)】
    ///   抽象基底クラス Fragment, Scalar, Enumerable, Root は
    ///   具体 Fragment ではないため候補から除外する。
    ///
    /// 【コンストラクタの制約】
    ///   第1引数は必ず int (一意な識別番号) であること。
    ///   それ以降の引数が Fragment チェーン構築に使われるパラメータとなる。
    ///
    /// 【ソート順】
    ///   同一メソッド名の候補はパラメータ数の降順で並ぶ。
    ///   これにより 登録済みフラグメント生成を試行() が
    ///   より特化した overload (引数が多い) から先に試行できる。
    ///
    /// 【利用シーン】
    ///   この辞書は フラグメント候補一覧 静的フィールドとして保持され、
    ///   登録済みフラグメント生成() や フラグメント候補選択() から
    ///   「LINQ メソッド名 → 対応 Fragment 型」の解決に使われる。
    ///   新しい Fragment 継承クラスを追加するだけで自動的に認識されるため、
    ///   辞書の手動更新は不要。
    /// </summary>
    private static IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧構築() {
        // Fragment 抽象基底クラスの型情報を取得
        var fragmentType = typeof(Fragment);
        // 具体 Fragment ではない基底型/補助型を除外リストに追加
        var helperTypes = new HashSet<Type> {
            typeof(Fragment),
            typeof(Scalar),
            typeof(Enumerable),
            typeof(Root),
        };

        // アセンブリ内の全型から Fragment 継承クラスを抽出
        var candidates = fragmentType.Assembly.GetTypes()
            .Where(type=>!type.IsAbstract                          // 抽象クラスは除外
                && fragmentType.IsAssignableFrom(type)               // Fragment を継承している
                && !helperTypes.Contains(type))                      // 基底型/補助型を除外
            .Select(type=>{
                // コンストラクタは1つだけ想定
                var constructor = type.GetConstructors().SingleOrDefault();
                if(constructor is null) {
                    return null;
                }

                var parameters = constructor.GetParameters();
                // 第1引数は int (一意識別番号) である必要がある
                if(parameters.Length==0 || parameters[0].ParameterType!=typeof(int)) {
                    return null;
                }

                // 型名を '_' で分割して MethodName と VariantName を抽出
                // 例: "Aggregate_seed_func" → ["Aggregate", "seed", "func"]
                var parts = type.Name.Split('_');
                var isApplicableMethod = type.GetMethod(
                    "IsApplicableOverload",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    [typeof(IMethodSymbol)],
                    null);
                Func<IMethodSymbol, bool>? isApplicableDelegate = isApplicableMethod is not null
                    ? method=>(bool)isApplicableMethod.Invoke(null,[method])!
                    : null;
                return new フラグメント候補 {
                    FragmentType=type,
                    Constructor=constructor,
                    MethodName=parts[0],                              // 例: "Aggregate"
                    VariantName=parts.Length>1                         // 例: "seed_func"
                        ? string.Join("_", parts.Skip(1))
                        : string.Empty,
                    Parameters=parameters[1..],                       // int 識別番号を除いた実パラメータ
                    IsApplicableOverload=isApplicableDelegate,
                };
            })
            .Where(static candidate=>candidate is not null)
            .Cast<フラグメント候補>()
            // パラメータ数の降順 → 型名の昇順でソート
            // パラメータ数が多い = より特化した overload を先に試行するため
            .OrderByDescending(static candidate=>candidate.Parameters.Length)
            .ThenBy(static candidate=>candidate.FragmentType.Name, StringComparer.Ordinal)
            // MethodName でグループ化して辞書化
            .GroupBy(static candidate=>candidate.MethodName, StringComparer.Ordinal)
            .ToDictionary(
                static group=>group.Key,
                static group=>group.ToImmutableArray(),
                StringComparer.Ordinal);

        return candidates;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// Fragment 候補一覧から Method 名に対応する型
    /// 例:
    /// Aggregate_func / Aggregate_seed_func / Aggregate_seed_func_resultSelector
    /// Where_predicate / Where_indexPredicate
    /// Join / GroupJoin
    /// を順に試し、成立したチェーンへ正規化する。
    /// </summary>
    private static bool 登録済みフラグメント生成(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        if(!呼び出しソース式取得(semanticModel,invocation,method,out var sourceExpression,out var argumentStartIndex,out var fragmentMethod)) {
            return false;
        }

        var methodName = (fragmentMethod.ReducedFrom ?? fragmentMethod).Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(登録済みフラグメント生成を試行(candidate,semanticModel,invocation,sourceExpression,argumentStartIndex,fragmentMethod,containingType,ref snapshot,out fragment,out staticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                return true;
            }
        }

        return false;
    }

    private static bool 呼び出しソース式取得(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ExpressionSyntax sourceExpression,
        out int argumentStartIndex,
        out IMethodSymbol fragmentMethod) {
        sourceExpression=null!;
        argumentStartIndex=0;
        fragmentMethod=method;

        // 拡張メソッド呼び出しのみを対象とする。
        // 静的呼び出し（SystemEnumerable.Method(source, ...)）は展開せず、
        // 列挙式フラグメント生成 が単独列挙ソースフラグメント生成 にフォールバックして不透明な Root として扱う。
        if(method.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression=括弧除去(receiverExpression);
            return true;
        }

        return false;
    }

    private static bool IsEnumerableSourceParameter(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType
        && (IsIEnumerableType(namedType) || IsNonGenericIEnumerableType(namedType));

    /// <summary>
    /// source.SelectMany(p =&gt; innerChain) の内部チェーンを struct にインライン化する。
    /// <br/>selector が単一引数ラムダで本体が LINQ チェーンにパース可能な場合のみ成功する。
    /// 外側パラメータ名（例: p）を内側 Fragment の GetEnumeratorExpression 内で
    /// outerValue フィールド名に置換し、SelectMany_WithInlinedInner を生成する。
    /// </summary>
    private static bool SelectManyインライン化を試行(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        // 内側チェーンの構築は再帰的に呼び出し式からフラグメント生成 のフォールバック経路
        // （SuspendAnonRegistry 付き）を通るため、ピン留めして匿名型の record struct 置換を維持する。
        PinAnonRegistry();
        try {
            if(!SelectManyBase.TryBuildInlined(
                semanticModel,invocation,containingType,ref uniqueNumber,
                out fragment,out staticMethods,unsafeAccessorCollector,
                列挙式フラグメント生成,
                TryGetLambdaExpressionBody,
                GetLambdaParameters)) {
                return false;
            }
        } finally {
            UnpinAnonRegistry();
        }
        // 結果要素が匿名型でなければ struct インライン化結果をそのまま使う。
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.TypeArguments.Length==0) {
            return true;
        }
        var resultElementType=method.TypeArguments[^1];
        if(!ContainsAnonymousType(resultElementType)) {
            return true;
        }
        // 内側チェーンは record struct 置換で構築済みのため、要素を元の匿名型へ復元する
        // 出口 Select を被せる（トップレベル匿名型 Select と同じ TResult 推論形）。
        var registry=_anonRegistry;
        if(registry is null || fragment is not Enumerable selectManyFragment) {
            // レジストリ無効時は record struct 置換が行われず TryBuildInlined がここまで
            // 来ないはずだが、保険としてフォールバック経路へ委ねる。
            return false;
        }
        var recordTypeName=registry.ResolveClosedName(resultElementType);
        var exitParamName=$"__anonExit{uniqueNumber}";
        var exitBody=registry.ReconstructAnonymousCreation(resultElementType,exitParamName);
        var exitFragment=new AnonymousDelegateFuncFragment(
            $"global::System.Func<{recordTypeName},TResult>",
            $"_anonExitDel{uniqueNumber++}",
            $"({recordTypeName} {exitParamName})=>{exitBody}") {
            ReturnTypeName="TResult",
            ParameterTypeNames=[recordTypeName],
            ParameterNames=[exitParamName],
            CanGenerateMethod=false,
        };
        var exitSelect=new Select_selector(uniqueNumber++,selectManyFragment,exitFragment);
        fragment=exitSelect;
        staticMethods=静的メソッドコード連結(staticMethods,exitSelect.StructMethod);
        return true;
    }

    private static bool フラグメント候補選択(string methodName,string variantName,out フラグメント候補 candidate) {
        candidate=null!;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var current in candidates) {
            if(current.VariantName!=variantName) {
                continue;
            }
            candidate=current;
            return true;
        }

        return false;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// candidate(番号, sourceFragment, ...)
    /// のコンストラクター形へ引数を並べ替えて、1 本の Fragment チェーンへ正規化する。
    /// 例:
    /// source.Aggregate(seed, func) → Aggregate_seed_func(sourceFragment, seed, funcFragment)
    /// source.Select(selector) → Select(sourceFragment, selectorFragment)
    /// source.Join(inner, outerKey, innerKey, result, comparer?)
    ///   → Join(outerFragment, innerFragment, outerKeyFragment, innerKeyFragment, resultFragment, comparer)
    /// </summary>
    private static bool 登録済みフラグメント生成を試行(
        フラグメント候補 candidate,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax sourceExpression,
        int argumentStartIndex,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(!フラグメント候補が使用可能か(candidate,method)) {
            return false;
        }

        var constructorArguments = new List<object?>(candidate.Parameters.Length+1) { uniqueNumber++ };
        if(!列挙式フラグメント生成(semanticModel,sourceExpression,containingType,ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector)) {
            return false;
        }

        constructorArguments.Add(sourceFragment);
        staticMethods=sourceStaticMethods;

        var argumentIndex = argumentStartIndex;
        for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
            var parameter = candidate.Parameters[parameterIndex];
            if(parameter.ParameterType==typeof(string)
                && TryResolveSyntheticStringArgument(parameter,method,out var syntheticArgument)) {
                constructorArguments.Add(syntheticArgument);
                continue;
            }

            if(argumentIndex>=invocation.ArgumentList.Arguments.Count) {
                // "comparer" 文字列パラメータは省略可能（= null）。他の string は合成引数か必須引数なので return false。
                if(parameter.ParameterType==typeof(string) && parameter.Name=="comparer") {
                    constructorArguments.Add("null");
                    continue;
                }
                return false;
            }

            var argumentExpression = invocation.ArgumentList.Arguments[argumentIndex].Expression;
            if(parameter.ParameterType==typeof(string)) {
                constructorArguments.Add(RenderExpressionForGeneratedMethod(argumentExpression,semanticModel,containingType));
                argumentIndex++;
                continue;
            }

            if(typeof(FuncFragment).IsAssignableFrom(parameter.ParameterType)) {
                var methodParameterIndex = argumentIndex - argumentStartIndex;
                if(methodParameterIndex < 0
                    || methodParameterIndex >= method.Parameters.Length
                    || !関数式フラグメント生成(argumentExpression,method.Parameters[methodParameterIndex].Type,ref uniqueNumber,out var funcFragment,semanticModel,containingType)
                    || !関数フラグメントが利用可能か(funcFragment)) {
                    return false;
                }

                constructorArguments.Add(funcFragment);
                argumentIndex++;
                continue;
            }

            if(typeof(Enumerable).IsAssignableFrom(parameter.ParameterType)) {
                if(!単独列挙ソースフラグメント生成(semanticModel,argumentExpression,containingType,ref uniqueNumber,out var enumerableFragment,out var enumerableStaticMethods,unsafeAccessorCollector)) {
                    return false;
                }

                constructorArguments.Add(enumerableFragment);
                staticMethods=静的メソッドコード連結(staticMethods,enumerableStaticMethods);
                argumentIndex++;
                continue;
            }

            return false;
        }

        if(argumentIndex!=invocation.ArgumentList.Arguments.Count) {
            return false;
        }

        if(candidate.Constructor.Invoke(constructorArguments.ToArray()) is not Fragment generatedFragment) {
            return false;
        }

        fragment=generatedFragment;
        staticMethods=静的メソッドコード連結(staticMethods,fragment.StructMethod);
        return true;
    }

    private static bool TryResolveSyntheticStringArgument(ParameterInfo parameter,IMethodSymbol method,out string value) {
        value=string.Empty;
        if(parameter.Name is not ("resultElementType" or "resultType")) {
            return false;
        }

        if(parameter.Name=="resultType") {
            value=method.ReturnType.ToDisplayString(完全修飾形式);
            return true;
        }

        if(!列挙要素型取得(method.ReturnType,out var elementType,out _)) {
            return false;
        }

        value=elementType.ToDisplayString(完全修飾形式);
        return true;
    }

    private static bool フラグメント候補が使用可能か(フラグメント候補 candidate,IMethodSymbol method)
       =>candidate.IsApplicableOverload?.Invoke(method) ?? true;

    private static bool 関数フラグメントが利用可能か(FuncFragment funcFragment) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        // キャプチャ付きラムダ/delegate 式は struct closure として primary constructor に閉じ込め、
        // ループ内では式へ展開する。delegate 変数・メソッドグループは受け付けない。
        if(funcFragment is LambdaFunc) {
            return true;
        }

        // try-catch を含む複数文ブロック本体は BlockFunc で表現し、FuncAssign で文として展開する。
        // BlockFunc.RequiresStatementInlining = true のため FuncAssign 側が RenderInlineAssign を使う。
        if(funcFragment is BlockFunc) {
            return true;
        }

        if(funcFragment.CanInvokeAsDelegate) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// source.Select(...).Where(...)
    /// ↓
    /// Select(...) → Where(...)
    /// のように、入れ子の LINQ 呼び出しなら再帰的に Fragment チェーンへほどく。
    /// LINQ 呼び出しでなければ Root(sourceExpression) として扱う。
    /// </summary>
    private static bool 列挙式フラグメント生成(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        expression=括弧除去(expression);
        if(expression is InvocationExpressionSyntax invocation
            && 呼び出し式からフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out var nestedFragment,out staticMethods,unsafeAccessorCollector)
            && nestedFragment is Enumerable enumerableFragment) {
            fragment=enumerableFragment;
            return true;
        }

        return 単独列挙ソースフラグメント生成(semanticModel,expression,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
    }

    /// <summary>
    /// 式を包む <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedExpressionSyntax"/> を再帰的に剥がし、内側の式を返す。
    /// Fragment 生成の前処理として呼ぶことで、<c>(source.Where(…))</c> のような余分な括弧を無視できる。
    /// </summary>
    private static ExpressionSyntax 括弧除去(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>
    /// Source() / Second() / rows
    /// ↓
    /// Root(sourceExpression)
    /// として、メソッドチェーンの起点になる列挙ソースへ正規化する。
    /// </summary>
    private static bool 単独列挙ソースフラグメント生成(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        staticMethods=string.Empty;
        if(TryBuildKnownEnumerableRoot(semanticModel,expression,containingType,ref uniqueNumber,out fragment,unsafeAccessorCollector)) {
            return true;
        }
        var typeInfo = semanticModel.GetTypeInfo(expression);
        var expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if(!列挙要素型取得(expressionType,out var elementType,out var useEnumerableInterface)) {
            if(IsNonGenericIEnumerableType(expressionType)) {
                fragment=new NonGenericRoot(
                    uniqueNumber++,
                    RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector));
                return true;
            }

            fragment=null!;
            return false;
        }

        // 具象型エニュメレータ名を取得する。具象 GetEnumerator() が値型を返す場合はその型名を使い、
        // インターフェース経由の場合は IEnumerator<T> 扱いに切り替える。
        var elementTypeName = elementType.ToDisplayString(完全修飾形式);
        var (concreteEnumeratorTypeName, isDuckTyping) = 具象エニュメレータ型名取得(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ?? $"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        // 具象型が取れなかった場合はインターフェース IEnumerator<T> になるため struct 制約なし扱いにする。
        var effectiveUseEnumerableInterface = useEnumerableInterface || concreteEnumeratorTypeName is null;

        var _srcExpr=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        var _num=uniqueNumber++;
        fragment = expressionType is IArrayTypeSymbol
            ? new ArrayRoot(_num, _srcExpr, elementTypeName)
            : IsListType(expressionType)
                ? new ListRoot(_num, _srcExpr, elementTypeName)
                : IsTsavoriteConcreteHashSetType(expressionType,out _)
                    ? new TsavoriteHashSetRoot(_num, _srcExpr, elementTypeName, enumeratorTypeName)
                : isDuckTyping && concreteEnumeratorTypeName is not null
                    ? new DuckTypingRoot(_num, _srcExpr, elementTypeName, concreteEnumeratorTypeName)
                    : CreateRootEnumerableFragment(_srcExpr, elementTypeName, enumeratorTypeName, effectiveUseEnumerableInterface, _num);
        return true;
    }

    /// <summary>
    /// ソース型の public な非明示的実装 <c>GetEnumerator()</c> を探し、具象エニュメレータ型名と
    /// duck-typing フラグを返す。
    /// <br/>インターフェース型・配列・戻り値がインターフェースや抽象型の場合は <c>(null, false)</c>。
    /// <br/><c>isDuckTyping=true</c>: エニュメレータが <c>IEnumerator&lt;T&gt;</c> を実装しない
    /// duck-typing struct（例: <c>ImmutableArray&lt;T&gt;.Enumerator</c>）。
    /// この場合 struct 制約 (<c>where TEnum : struct, IEnumerator&lt;T&gt;</c>) に渡せず
    /// <see cref="Linq.Fragments.DuckTypingRoot"/> で直接展開する必要がある。
    /// </summary>
    private static (string? typeName, bool isDuckTyping) 具象エニュメレータ型名取得(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface || sourceType is not INamedTypeSymbol namedSource) {
            return (null, false);
        }
        // 明示的インターフェース実装を除いた public GetEnumerator() を探す。
        var getEnumerator = namedSource.GetMembers("GetEnumerator")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.IsEmpty
                && m.DeclaredAccessibility==Accessibility.Public
                && m.MethodKind!=MethodKind.ExplicitInterfaceImplementation
                && !m.IsStatic);
        if(getEnumerator is null) {
            return (null, false);
        }
        // 戻り値が具象値型（インターフェースでも抽象型でもない）であれば使う。
        if(getEnumerator.ReturnType is not INamedTypeSymbol returnType
            || returnType.IsAbstract
            || returnType.TypeKind==TypeKind.Interface) {
            return (null, false);
        }
        var typeName = returnType.ToDisplayString(完全修飾形式);
        // IEnumerator<elementType> を実装しているかチェック。
        // ImmutableArray<T>.Enumerator などは duck-typing パターンのみで IEnumerator<T> を実装しない。
        // そのような型を struct 制約 (where TEnum : struct, IEnumerator<T>) に渡すと CS0315 が発生する。
        var implementsIEnumerator = returnType.AllInterfaces.Any(i =>
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
            && i.MetadataName == "IEnumerator`1"
            && i.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0],elementType));
        return (typeName, isDuckTyping: !implementsIEnumerator);
    }

    /// <summary>
    /// 型がちょうど <c>System.Collections.Generic.List&lt;T&gt;</c> かを判定する。
    /// 継承クラスは内部配列の意味が同じでも別物として扱い、従来の Root パスへ流す。
    /// <c>List&lt;T&gt;</c> と判定された場合は <see cref="Linq.Fragments.ListRoot"/> で
    /// <c>CollectionsMarshal.AsSpan</c> ベースの直走査を使える。
    /// </summary>
    private static bool IsListType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && named.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.List<T>";

    /// <summary>
    /// 型シンボルから列挙要素型（<c>T</c>）を解決する。
    /// <br/>解決順序:
    /// <list type="number">
    ///   <item><description>配列型 (<c>T[]</c>) → 要素型を直接取得。<c>useEnumerableInterface=true</c>（配列は <c>GetEnumerator()</c> が interface 実装）。</description></item>
    ///   <item><description>型自体が <c>IEnumerable&lt;T&gt;</c> → 型引数を取得。<c>useEnumerableInterface=true</c>。</description></item>
    ///   <item><description>上記以外の名前付き型 → <c>AllInterfaces</c> から <c>IEnumerable&lt;T&gt;</c> を探す。見つかれば <c>useEnumerableInterface=false</c>（具象 <c>GetEnumerator()</c> が使える可能性あり）。</description></item>
    /// </list>
    /// <c>useEnumerableInterface=false</c> の場合、<see cref="具象エニュメレータ型名取得"/> でさらに具象エニュメレータ型を探す。
    /// </summary>
    private static bool 列挙要素型取得(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType) && namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate=>IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    useEnumerableInterface=false;
                    return true;
                }
                break;
        }

        elementType=null!;
        useEnumerableInterface=false;
        return false;
    }

    private static bool TryCreateDelegateFuncFragment(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol namedDelegate) {
            return false;
        }

        var invoke=namedDelegate.DelegateInvokeMethod
            ?? namedDelegate.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
        if(invoke is null || invoke.ReturnsVoid) {
            return false;
        }

        var delegateTypeName=delegateType.ToDisplayString(完全修飾形式);
        var delegateFieldName=$"_del{uniqueNumber++}";
        string delegateConstructorArg;
        if(semanticModel is not null && containingType is not null) {
            delegateConstructorArg=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType);
        }else{
            delegateConstructorArg=NormalizeExpressionText(expression.ToString());
        }
        fragment=new DelegateFuncFragment(delegateTypeName,delegateFieldName,delegateConstructorArg) {
            ReturnTypeName=invoke.ReturnType.ToDisplayString(完全修飾形式),
            ParameterTypeNames=invoke.Parameters.Select(static p=>p.Type.ToDisplayString(完全修飾形式)).ToArray(),
            ParameterNames=invoke.Parameters.Select(static p=>p.Name).ToArray(),
            CanGenerateMethod=false,
            CanInlineAssign=false,
            ContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel),
        };
        return true;
    }

    /// <summary>
    /// x=>x * 2 / (left, right)=>left + right
    /// ↓
    /// Select や Aggregate に渡せる FuncFragment へ正規化する。
    /// 生成可能な static ラムダなら補助メソッド化し、
    /// 閉包付きラムダならインライン代入だけ許可する。
    /// delegate 変数がラムダ / anonymous delegate 初期化なら、その初期化式へ戻して同じ規則で処理する。
    /// </summary>
    private static bool 関数式フラグメント生成(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? containingType = null) {
        fragment=null!;
        // 方針: 直接式へインライン化できるならそれを最優先し、クロージャが必要なら struct primary constructor に閉じ込める。
        // delegate Invoke へ逃がす経路は作らない。
        // 仕様: Func<T> f = x=>...; のようなローカル delegate 変数は initializer のラムダへ戻してインライン化する。
        // 再代入された delegate 変数や method group は inline 対象外。delegate Invoke fallback は生成しない。
        if(TryGetInlineableDelegateVariableInitializer(expression,semanticModel,out var initializerExpression)) {
            return 関数式フラグメント生成(
                initializerExpression,
                delegateType,
                ref uniqueNumber,
                out fragment,
                semanticModel,
                containingType);
        }

        if(expression is LambdaExpressionSyntax lambda) {
            // 複数文ブロック本体（=>{ ...; return x; ... }、try-catch 含む）は BlockFunc で文インライン化する。
            // 式本体・単一 return 文は従来どおり下の式インライン経路へ流す。
            if(lambda.Body is BlockSyntax lambdaBlock
                && !IsSingleReturnBlock(lambdaBlock)
                && TryBuildBlockFunc(lambdaBlock,GetLambdaParameters(lambda),delegateType,semanticModel,containingType,out var lambdaBlockFunc)) {
                fragment = lambdaBlockFunc;
                return true;
            }

            // 匿名型を含む delegate 型は、匿名型→ジェネリック record struct 透過置換で「名指し可能な型」へ写像して
            // 通常の LambdaFunc としてインライン展開する（delegate 保持に頼らない）。
            // これによりメソッドチェーン中間の匿名型が後続ステージへ展開できるようになる。
            if(TryBuildRegistryMappedLambdaFunc(lambda,delegateType,semanticModel,containingType,out var registryFragment)) {
                fragment = registryFragment;
                return true;
            }

            // 出口パス: 引数・戻り値の両方に匿名型がある場合（struct record → 匿名型への変換が必要）。
            // TryBuildRegistryMappedLambdaFunc がこのケースを除外するため、ここで専用フラグメントを生成する。
            if(TryBuildAnonExitDelegateFuncFragment(lambda,delegateType,ref uniqueNumber,out var anonExitFragment,semanticModel,containingType)) {
                fragment = anonExitFragment;
                return true;
            }

            // ラムダが匿名型（または匿名型を含む型）を「返す」場合 → AnonymousDelegateFuncFragment を生成。
            // 引数型のみ匿名型の場合（ソースが匿名型）は対象外：TryCreateLambdaFuncFragment 失敗で return false。
            // TryCreateLambdaFuncFragment より先にチェック（匿名型は delegate 型解決に失敗するため）。
            if(semanticModel is not null
                && containingType is not null
                && delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } funcTypeSym
                && ContainsAnonymousType(funcTypeSym.TypeArguments[^1])) {
                var returnTypeSym = funcTypeSym.TypeArguments[^1];
                var returnTypeName = ReplaceAnonymousTypesWithTResult(returnTypeSym);
                var paramTypeNames = funcTypeSym.TypeArguments.Take(funcTypeSym.TypeArguments.Length - 1)
                    .Select(static t=>t.ToDisplayString(完全修飾形式)).ToArray();
                var lambdaParamNames = GetLambdaParameters(lambda).ToArray();
                var anonCapturedLocals = Array.Empty<(string Name, string TypeName)>();
                var anonBodyAccessorCodes = Array.Empty<string>();
                if(TryGetLambdaExpressionBody(lambda,out var anonBodyExpr)) {
                    {
                        var anonBodyCollector = new List<string>();
                        RenderExpressionForGeneratedMethod(anonBodyExpr,semanticModel,containingType!,anonBodyCollector);
                        anonBodyAccessorCodes = anonBodyCollector.ToArray();
                    }
                    var anonDataFlow = semanticModel.AnalyzeDataFlow(anonBodyExpr);
                    if(anonDataFlow?.Succeeded == true) {
                        var anonParamSet = new HashSet<string>(lambdaParamNames,StringComparer.Ordinal);
                        anonCapturedLocals = anonDataFlow.DataFlowsIn
                            .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !anonParamSet.Contains(sym.Name) && sym.Name != "this")
                            .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                                ? loc.Type.ToDisplayString(完全修飾形式)
                                : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
                var allTypeNames = paramTypeNames.Append(returnTypeName);
                var delegateTypeName = $"global::System.Func<{string.Join(", ", allTypeNames)}>";
                var delegateFieldName = $"_anonDel{uniqueNumber++}";
                var delegateConstructorArg = RenderExpressionForGeneratedMethod(lambda, semanticModel, containingType);
                    fragment = new AnonymousDelegateFuncFragment(delegateTypeName, delegateFieldName, delegateConstructorArg) {
                    ReturnTypeName = returnTypeName,
                    ParameterTypeNames = paramTypeNames,
                    ParameterNames = lambdaParamNames,
                    CanGenerateMethod = false,
                    CapturedLocals = anonCapturedLocals,
                    BodyUnsafeAccessorCodes = anonBodyAccessorCodes,
                    ContainsAnchor = AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
                    CardinalityUpperBound = lambda.Body is ExpressionSyntax anonLambdaBody
                        ? CardinalityAnalysis.TryGetUpperBound(anonLambdaBody,semanticModel)
                        : null
                };
                return true;
            }

            if(!TryCreateLambdaFuncFragment(lambda,delegateType,out var lambdaSpec)) {
                // delegate 型の引数に匿名型が含まれる場合（ソースが匿名型など）は投げずに return false。
                // 呼び出し元が元の System.Linq 式を維持する処理を行う。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } ft
                    && ft.TypeArguments.Any(ContainsAnonymousType)) {
                    fragment = null!;
                    return false;
                }
                throw LambdaInlineException(lambda,semanticModel,"ラムダ本体が式または単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = lambdaSpec.Body;
            var capturedLocals = Array.Empty<(string Name, string TypeName)>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            ExpressionSyntax? lambdaBodyExpr = null;
            if(semanticModel is not null && TryGetLambdaExpressionBody(lambda,out lambdaBodyExpr)) {
                if(containingType is not null) {
                    var bodyCollector = new List<string>();
                    body=RenderExpressionForGeneratedMethod(lambdaBodyExpr,semanticModel,containingType,bodyCollector);
                    bodyAccessorCodes = bodyCollector.ToArray();
                }
                // ラムダがキャプチャしているローカル変数・外側パラメータを検出する。
                // lambda 自身のパラメータと暗黙の this は除外する。
                var dataFlow = semanticModel.AnalyzeDataFlow(lambdaBodyExpr);
                if(dataFlow?.Succeeded == true) {
                    var paramSet = new HashSet<string>(lambdaSpec.ParameterNames,StringComparer.Ordinal);
                    var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym=>sym.Name == "this") as IParameterSymbol;
                    capturesThis = thisSymbol is not null;
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                    // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
                    // struct closure のキャプチャ変数として明示的に持ち回る。
                    // 型は containingType（呼び出し元の囲み型。Full Fusion 時は呼び出し先と異なりうる）ではなく
                    // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）を用いる。
                    if(thisSymbol is not null) {
                        capturedLocals = capturedLocals
                            .Append(("@this", thisSymbol.Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
            }

            if(semanticModel is not null
                && containingType is not null
                && lambdaBodyExpr is not null
                && TryFindSystemLinqInvocation(lambdaBodyExpr,semanticModel,out var scalarInvocation,out var scalarMethod)) {
                var snapshot = uniqueNumber;
                if(呼び出し式からフラグメント生成(
                        semanticModel,
                        scalarInvocation,
                        containingType,
                        ref snapshot,
                        out var scalarFragment,
                        out var scalarStaticMethods,
                        null)
                    && scalarFragment is Scalar
                    && ScalarFragmentSupportsInlineEnumeration(scalarFragment)) {
                    uniqueNumber = snapshot;
                    fragment = new InlinedScalarFunc(scalarFragment) {
                        ReturnTypeName = lambdaSpec.ReturnTypeName,
                        ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                        ParameterNames = lambdaSpec.ParameterNames,
                        CanGenerateMethod = false,
                        CanInlineAssign = true,
                        RequiresStatementInlining = true,
                        InlineBody = string.Empty,
                        BodyExpression = RenderExpressionForGeneratedMethod(lambdaBodyExpr,semanticModel,containingType),
                        ScalarExpression = RenderExpressionForGeneratedMethod(scalarInvocation,semanticModel,containingType),
                        ScalarReturnTypeName = semanticModel.GetTypeInfo(scalarInvocation).Type?.ToDisplayString(完全修飾形式) ?? scalarFragment.GetType().Name,
                        CapturedLocals = capturedLocals,
                        BodyUnsafeAccessorCodes = bodyAccessorCodes
                            .Concat(scalarFragment.CollectAllUnsafeAccessorCodes())
                            .ToArray(),
                        ScalarStaticMethods = scalarStaticMethods,
                        OuterParameterNames = lambdaSpec.ParameterNames,
                        ContainsAnchor = AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
                        CardinalityUpperBound = CardinalityAnalysis.TryGetUpperBound(lambdaBodyExpr,semanticModel),
                    };
                    return true;
                }

                // シーケンスを返す LINQ は SelectMany などの collection selector として有効なので、
                // スカラー Fragment 化できない場合は通常ラムダとして残す。
            }

            // キャプチャなし（this 含む）なら実質 static → struct ゼロコピーでメソッド生成可能。
            var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
                || (!capturesThis && capturedLocals.Length == 0);
            fragment = new LambdaFunc {
                ReturnTypeName = lambdaSpec.ReturnTypeName,
                ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                ParameterNames = lambdaSpec.ParameterNames,
                CanGenerateMethod = canGenerateMethod,
                CanInlineAssign = true,
                InlineBody = body,
                CapturedLocals = capturedLocals,
                BodyUnsafeAccessorCodes = bodyAccessorCodes,
                ContainsAnchor = AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
                CardinalityUpperBound = lambda.Body is ExpressionSyntax lambdaExpressionBody
                        && semanticModel is not null
                    ? CardinalityAnalysis.TryGetUpperBound(lambdaExpressionBody,semanticModel)
                    : null,
            };
            return true;
        }

        if(expression is AnonymousMethodExpressionSyntax anonymousMethod) {
            // delegate(){ ...; return x; ... }（try-catch 含む複数文）も BlockFunc で文インライン化する。
            if(anonymousMethod.Block is BlockSyntax anonymousBlock
                && !IsSingleReturnBlock(anonymousBlock)
                && TryBuildBlockFunc(anonymousBlock,GetAnonymousMethodParameters(anonymousMethod),delegateType,semanticModel,containingType,out var anonymousBlockFunc)) {
                fragment = anonymousBlockFunc;
                return true;
            }

            if(!TryCreateAnonymousMethodFuncFragment(anonymousMethod,delegateType,out var anonymousSpec,out var bodyExpression)) {
                // delegate 式が匿名型を返す場合も AnonymousDelegateFuncFragment を生成する。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } anonFuncType
                    && anonFuncType.TypeArguments.Any(ContainsAnonymousType)
                    && semanticModel is not null && containingType is not null) {
                    var anonReturnTypeName = ReplaceAnonymousTypesWithTResult(anonFuncType.TypeArguments[^1]);
                    var anonParamTypeNames = anonFuncType.TypeArguments.Take(anonFuncType.TypeArguments.Length - 1)
                        .Select(static t=>t.ToDisplayString(完全修飾形式)).ToArray();
                    var anonParamNames = GetAnonymousMethodParameters(anonymousMethod).ToArray();
                    var anonDelegateTypeName = $"global::System.Func<{string.Join(", ", anonParamTypeNames.Append(anonReturnTypeName))}>";
                    var anonFieldName = $"_anonDel{uniqueNumber++}";
                    var anonCtorArg = RenderExpressionForGeneratedMethod(anonymousMethod, semanticModel, containingType);
                    fragment = new AnonymousDelegateFuncFragment(anonDelegateTypeName, anonFieldName, anonCtorArg) {
                        ReturnTypeName = anonReturnTypeName,
                        ParameterTypeNames = anonParamTypeNames,
                        ParameterNames = anonParamNames,
                        CanGenerateMethod = false,
                        ContainsAnchor = AnchorAnalysis.ContainsAnchor(anonymousMethod,semanticModel),
                        CardinalityUpperBound = CardinalityAnalysis.TryGetUpperBound(bodyExpression,semanticModel)
                    };
                    return true;
                }
                throw new InvalidOperationException($"delegate 式をインライン展開できませんでした。LINQ delegate: '{NormalizeExpressionText(anonymousMethod.ToString())}'。理由: 本体が単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = anonymousSpec.Body;
            var capturedLocals = Array.Empty<(string Name, string TypeName)>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            if(semanticModel is not null && containingType is not null) {
                var bodyCollector = new List<string>();
                body=RenderExpressionForGeneratedMethod(bodyExpression,semanticModel,containingType,bodyCollector);
                bodyAccessorCodes = bodyCollector.ToArray();

                var dataFlow = semanticModel.AnalyzeDataFlow(bodyExpression);
                if(dataFlow?.Succeeded == true) {
                    var paramSet = new HashSet<string>(anonymousSpec.ParameterNames,StringComparer.Ordinal);
                    capturesThis = dataFlow.DataFlowsIn.Any(sym=>sym.Name == "this");
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                }
            }

            fragment = new LambdaFunc {
                ReturnTypeName = anonymousSpec.ReturnTypeName,
                ParameterTypeNames = anonymousSpec.ParameterTypeNames,
                ParameterNames = anonymousSpec.ParameterNames,
                CanGenerateMethod = !capturesThis && capturedLocals.Length == 0,
                CanInlineAssign = true,
                InlineBody = body,
                CapturedLocals = capturedLocals,
                BodyUnsafeAccessorCodes = bodyAccessorCodes,
                ContainsAnchor = AnchorAnalysis.ContainsAnchor(bodyExpression,semanticModel),
                CardinalityUpperBound = semanticModel is not null
                    ? CardinalityAnalysis.TryGetUpperBound(bodyExpression,semanticModel)
                    : null,
            };
            return true;
        }

        if(TryCreateDelegateFuncFragment(expression,delegateType,ref uniqueNumber,out fragment,semanticModel,containingType)) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// delegate 型に匿名型が含まれるラムダを、匿名型→ジェネリック record struct 透過置換で
    /// 名指し可能な型へ写像し、通常の <see cref="LambdaFunc"/>（インライン展開）として構築する。
    /// <br/>・引数型の匿名型 → record struct（メンバーアクセスはプロパティ名一致につき本体は書き換え不要）
    /// <br/>・戻り値の匿名型 → record struct（本体ルートの <c>new {...}</c> を <c>new record(...)</c> へ再帰的に書き換え）
    /// <br/>レジストリ未設定・匿名型なし・本体が匿名生成式でない戻り匿名型などは false を返し、従来経路へ委ねる。
    /// </summary>
    private static bool TryBuildRegistryMappedLambdaFunc(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        out FuncFragment fragment) {
        fragment = null!;
        var registry = _anonRegistry;
        if(registry is null || semanticModel is null || containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            || funcType.TypeArguments.Length==0
            || !funcType.TypeArguments.Any(ContainsAnonymousType)
            || !IsInlineDelegateType(funcType)) {
            return false;
        }
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        var paramTypeSyms = funcType.TypeArguments.Take(funcType.TypeArguments.Length-1).ToArray();
        if(parameters.Count!=paramTypeSyms.Length) {
            return false;
        }
        var returnTypeSym = funcType.TypeArguments[^1];

        var strippedBody = StripParentheses(body);
        if(ContainsAnonymousType(returnTypeSym) && strippedBody is not AnonymousObjectCreationExpressionSyntax) {
            // identity lambda (p=>p) は引数の struct record をそのまま返せるため通過させる。
            // それ以外（本体ルートが匿名生成式でも identity でもない）は struct record
            // ファクトリへ変換できないため、呼び出し元の TryBuildAnonExitDelegateFuncFragment へ委ねる。
            if(strippedBody is not IdentifierNameSyntax identityId || !parameters.Contains(identityId.Identifier.ValueText)) {
                return false;
            }
        }

        var parameterTypeNames = paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        var returnTypeName = registry.ResolveClosedName(returnTypeSym);

        var bodyCollector = new List<string>();
        var bodyText = strippedBody is AnonymousObjectCreationExpressionSyntax anonCreate
            ? registry.RenderAnonymousCreation(anonCreate,semanticModel,
                expr=>RenderExpressionForGeneratedMethod(expr,semanticModel,containingType,bodyCollector))
            : RenderExpressionForGeneratedMethod(body,semanticModel,containingType,bodyCollector);

        var capturedLocals = Array.Empty<(string Name, string TypeName)>();
        var capturesThis = false;
        var dataFlow = semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
            var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym=>sym.Name=="this") as IParameterSymbol;
            capturesThis = thisSymbol is not null;
            capturedLocals = dataFlow.DataFlowsIn
                .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
            // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
            // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）でキャプチャする。
            if(thisSymbol is not null) {
                capturedLocals = capturedLocals
                    .Append(("@this", thisSymbol.Type.ToDisplayString(完全修飾形式)))
                    .ToArray();
            }
        }

        var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            || (!capturesThis && capturedLocals.Length==0);

        fragment = new LambdaFunc {
            ReturnTypeName = returnTypeName,
            ParameterTypeNames = parameterTypeNames,
            ParameterNames = parameters.ToArray(),
            CanGenerateMethod = canGenerateMethod,
            CanInlineAssign = true,
            InlineBody = bodyText,
            CapturedLocals = capturedLocals,
            BodyUnsafeAccessorCodes = bodyCollector.ToArray(),
            ContainsAnchor = AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
            CardinalityUpperBound = lambda.Body is ExpressionSyntax lambdaBody
                ? CardinalityAnalysis.TryGetUpperBound(lambdaBody,semanticModel)
                : null,
        };
        return true;
    }

    /// <summary>
    /// 引数型・戻り値型の両方に匿名型が含まれるラムダを、struct record → 元の匿名型への再構成で
    /// <see cref="AnonymousDelegateFuncFragment"/> として構築する。
    /// <br/>対象: 引数が struct record に置換済みで、戻り値は実際の匿名型を返す必要がある「出口ステップ」。
    /// <br/>例1: GroupBy key selector <c>p=&gt;p</c> (identity) → <c>(struct_rec p)=&gt;new{p.field,...}</c>
    /// <br/>例2: GroupBy element selector <c>p=&gt;new{{x=p}}</c> → <c>(struct_rec p)=&gt;new{{x=new{{p.field,...}}}}</c>
    /// </summary>
    private static bool TryBuildAnonExitDelegateFuncFragment(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType) {
        fragment=null!;
        var registry=_anonRegistry;
        if(registry is null || semanticModel is null || containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            || funcType.TypeArguments.Length==0
            || !IsInlineDelegateType(funcType)) {
            return false;
        }
        var paramTypeSyms=funcType.TypeArguments.Take(funcType.TypeArguments.Length-1).ToArray();
        var returnTypeSym=funcType.TypeArguments[^1];
        // このパスは: 戻り値が匿名型の identity lambda (p=>p) など、
        // TryBuildRegistryMappedLambdaFunc が struct record ファクトリへ変換できなかった場合に限定する。
        if(!ContainsAnonymousType(returnTypeSym)) {
            return false;
        }
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }
        var parameters=GetLambdaParameters(lambda);
        if(parameters.Count!=paramTypeSyms.Length) {
            return false;
        }
        var strippedBody=StripParentheses(body);
        // identity: p=>p → 戻り値の匿名型全体を struct record のメンバーから再構成する。
        if(strippedBody is not IdentifierNameSyntax identId || !parameters.Contains(identId.Identifier.ValueText)) {
            return false;
        }
        var bodyCollector=new List<string>();
        var bodyText=registry.ReconstructAnonymousCreation(returnTypeSym,identId.Identifier.ValueText);
        var paramTypeNames=paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        // キャプチャローカルを収集する。
        var capturedLocals=Array.Empty<(string Name,string TypeName)>();
        var dataFlow=semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet=new HashSet<string>(parameters,StringComparer.Ordinal);
            capturedLocals=dataFlow.DataFlowsIn
                .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
        }
        // 引数を struct record 型で明示したラムダ式を組み立てて、デリゲートとして格納する。
        var paramDecls=parameters.Zip(paramTypeNames,static (n,t) => $"{t} {n}");
        var delegateLambda=$"({string.Join(",",paramDecls)})=>{bodyText}";
        var allTypeNames=paramTypeNames.Append("TResult");
        var delegateTypeName2=$"global::System.Func<{string.Join(",",allTypeNames)}>";
        var delegateFieldName=$"_anonExitDel{uniqueNumber++}";
        fragment=new AnonymousDelegateFuncFragment(delegateTypeName2,delegateFieldName,delegateLambda) {
            ReturnTypeName="TResult",
            ParameterTypeNames=paramTypeNames,
            ParameterNames=parameters.ToArray(),
            CanGenerateMethod=false,
            CapturedLocals=capturedLocals,
            BodyUnsafeAccessorCodes=bodyCollector.ToArray(),
            ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
            CardinalityUpperBound=lambda.Body is ExpressionSyntax lambdaBody
                ? CardinalityAnalysis.TryGetUpperBound(lambdaBody,semanticModel)
                : null,
        };
        return true;
    }

    /// <summary>
    /// LINQ に渡された識別子またはメンバーアクセス式が、宣言時にラムダ／<c>delegate</c> で初期化された
    /// ローカル変数またはフィールドであるかを判定し、そのイニシャライザ式を返す。
    /// <br/>成功条件: シンボルが <c>ILocalSymbol</c> または <c>IFieldSymbol</c> であり、
    /// 宣言の <c>VariableDeclarator.Initializer.Value</c> が <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.LambdaExpressionSyntax"/>
    /// または <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.AnonymousMethodExpressionSyntax"/> であること。
    /// <br/><c>field</c> / <c>this.field</c> / <c>Type.field</c> のいずれの参照形でも対象にする。
    /// <br/>delegate 変数呼び出しを delegate Invoke として残す経路は作らない。
    /// </summary>
    private static bool TryGetInlineableDelegateVariableInitializer(
        ExpressionSyntax expression,
        SemanticModel? semanticModel,
        out ExpressionSyntax initializerExpression) {
        initializerExpression=null!;
        if(semanticModel is null
            || expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)) {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if(symbol is not (ILocalSymbol or IFieldSymbol)
            || symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax {
                Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax
            } variableDeclarator) {
            return false;
        }

        initializerExpression=variableDeclarator.Initializer.Value;
        return true;
    }

    private static InvalidOperationException LambdaInlineException(
        LambdaExpressionSyntax lambda,
        SemanticModel? semanticModel,
        string reason,
        InvocationExpressionSyntax? linqInvocation = null) {
        var linqText = linqInvocation is not null
            ? NormalizeExpressionText(linqInvocation.ToString())
            : semanticModel is not null && TryFindSystemLinqInvocation(lambda,semanticModel,out var foundInvocation,out _)
                ? NormalizeExpressionText(foundInvocation.ToString())
                : NormalizeExpressionText(lambda.ToString());
        return new InvalidOperationException($"ラムダ式をインライン展開できませんでした。LINQ: '{linqText}'。理由: {reason}");
    }

    // スカラー subquery を InlinedScalarFunc（using-enumerator 文インライン）で展開できるかを判定する。
    // Sum/ToList/ToArray/ToDictionary など直接実行のみの PipelineTerminal は enumerator 式を作れず
    // GetEnumeratorExpression が NotSupportedException を投げるため、その場合は InlinedScalarFunc 化を諦め、
    // 呼び出し元で生のラムダ本体テキスト（実行時 LINQ-to-objects）として残す（Method 版と同じ挙動）。
    private static bool ScalarFragmentSupportsInlineEnumeration(Fragment scalarFragment) {
        try {
            _=scalarFragment.GetEnumeratorExpression;
            return true;
        } catch(NotSupportedException) {
            return false;
        }
    }
    private static bool TryFindSystemLinqInvocation(
        SyntaxNode node,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax invocation,
        out IMethodSymbol method) {
        foreach(var candidate in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if(semanticModel.GetSymbolInfo(candidate).Symbol is IMethodSymbol candidateMethod
                && SystemLinqEnumerableメソッド判定(candidateMethod)) {
                invocation=candidate;
                method=candidateMethod;
                return true;
            }
        }

        invocation=null!;
        method=null!;
        return false;
    }

    private static List<string> 未対応Enumerableメソッド一覧取得(Linq傍受対象型 target) {
        // ローカルメソッド全体を再構成する前に、Fragment クラス自体が存在しない Enumerable メソッド名を列挙する。
        // 「System.Linq.Enumerable を含むメソッドは全部 intercept する」が前提なので、
        // 未対応メソッドは target から落とすのではなく、生成 interceptor から例外で見えるようにする。
        return target.LinqAnalysis.UnsupportedMethods.ToList();
    }

    private static bool Enumerableメソッドがフラグメント対応済みか(string methodName)
       =>methodName=="Any" || methodName=="TryGetNonEnumeratedCount" || IsKnownEnumerableRootMethod(methodName) || フラグメント候補一覧.ContainsKey(methodName);

    private static bool IsKnownEnumerableRootMethod(string methodName) =>
        methodName is "Range" or "Repeat" or "Empty" or "InfiniteSequence" or "Sequence" or "AsEnumerable";

    private static bool IsKnownEnumerableRootMethod(IMethodSymbol method) =>
        method.ContainingType.ToDisplayString()=="System.Linq.Enumerable"
        && IsKnownEnumerableRootMethod(method.Name);

    private static Enumerable CreateRootEnumerableFragment(string sourceExpression,string elementType,string enumeratorTypeName,bool useEnumerableInterface,int 番号) {
        return new Root(
            番号,
            sourceExpression,
            elementType,
            enumeratorTypeName,
            useEnumerableInterface);
    }

    private static bool TryBuildKnownEnumerableRoot(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        fragment=null!;
        if(expression is not InvocationExpressionSyntax invocation
            || semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        if(TryBuildKnownEnumerableRootFromSystemInvocation(
            semanticModel,
            invocation,
            method.ReducedFrom ?? method,
            containingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap)) {
            return true;
        }

        return TryBuildKnownEnumerableRootFromOrdinaryMethod(
            semanticModel,
            invocation,
            method,
            containingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap);
    }

    private static bool TryBuildKnownEnumerableRootFromSystemInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        if(method.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            || !IsKnownEnumerableRootMethod(method.Name)) {
            return false;
        }

        if(method.Name=="AsEnumerable"
            && method.Parameters.Length==1
            && TryGetInvocationArgumentExpression(method,invocation,0,out var sourceExpression)) {
            return TryBuildEnumerableRootFromExpressionWithReplacement(
                semanticModel,
                sourceExpression,
                containingType,
                ref uniqueNumber,
                out fragment,
                unsafeAccessorCollector,
                replacementMap);
        }

        if(method.Name=="Empty"
            && invocation.ArgumentList.Arguments.Count==0
            && method.TypeArguments.Length==1) {
            fragment=new EmptyRoot(
                uniqueNumber++,
                method.TypeArguments[0].ToDisplayString(完全修飾形式));
            return true;
        }

        if(method.Name=="Range") {
            if(invocation.ArgumentList.Arguments.Count!=2) {
                return false;
            }
            fragment=new RangeRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[0].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[1].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap));
            return true;
        }

        if(method.Name=="Repeat" && method.TypeArguments.Length==1) {
            if(invocation.ArgumentList.Arguments.Count!=2) {
                return false;
            }
            fragment=new RepeatRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[0].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[1].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                method.TypeArguments[0].ToDisplayString(完全修飾形式));
            return true;
        }

        if(method.Name=="InfiniteSequence" && invocation.ArgumentList.Arguments.Count==2 && method.TypeArguments.Length==1) {
            fragment=new InfiniteSequenceRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[0].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[1].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                method.TypeArguments[0].ToDisplayString(完全修飾形式));
            return true;
        }

        if(method.Name=="Sequence" && invocation.ArgumentList.Arguments.Count==3 && method.TypeArguments.Length==1) {
            fragment=new SequenceRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[0].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[1].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                RenderExpressionForGeneratedMethod(invocation.ArgumentList.Arguments[2].Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                method.TypeArguments[0].ToDisplayString(完全修飾形式));
            return true;
        }

        return false;
    }

    private static bool TryBuildKnownEnumerableRootFromOrdinaryMethod(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? outerReplacementMap = null) {
        fragment=null!;
        var definition = method.ReducedFrom ?? method;
        if(definition.MethodKind!=MethodKind.Ordinary
            || definition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax
            || invocation.ArgumentList.Arguments.Any(static argument=>!argument.RefKindKeyword.IsKind(SyntaxKind.None))
            || !TryGetSingleExpressionBody(methodSyntax,out var bodyExpression)) {
            return false;
        }

        List<string> actualArguments;
        if(definition.IsExtensionMethod
            && invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess
            && invocation.ArgumentList.Arguments.Count<definition.Parameters.Length) {
            if(!TryCreateExtensionActualArgumentList(
                definition,
                invocation,
                extensionMemberAccess,
                semanticModel,
                containingType,
                unsafeAccessorCollector,
                outerReplacementMap,
                out actualArguments)) {
                return false;
            }
        } else if(!TryCreateOrdinaryActualArgumentList(
            method,
            invocation,
            semanticModel,
            containingType,
            unsafeAccessorCollector,
            outerReplacementMap,
            out actualArguments)) {
            return false;
        }

        var bodySemanticModel = semanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        var replacementMap = BuildInlineEnumerableRootReplacementMap(
            definition,
            invocation,
            methodSyntax,
            bodyExpression,
            bodySemanticModel,
            semanticModel,
            containingType,
            actualArguments,
            unsafeAccessorCollector);

        if(TryBuildTransparentParameterEnumerableRoot(
            semanticModel,
            invocation,
            definition,
            containingType,
            bodyExpression,
            bodySemanticModel,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            outerReplacementMap)) {
            return true;
        }

        if(bodyExpression is InvocationExpressionSyntax bodyInvocation
            && bodySemanticModel.GetSymbolInfo(bodyInvocation).Symbol is IMethodSymbol bodyMethod
            && bodyMethod.ContainingType.ToDisplayString()=="System.Linq.Enumerable") {
            return TryBuildKnownEnumerableRootFromSystemInvocation(
                bodySemanticModel,
                bodyInvocation,
                bodyMethod,
                definition.ContainingType,
                ref uniqueNumber,
                out fragment,
                unsafeAccessorCollector,
                replacementMap);
        }

        if(bodyExpression is CollectionExpressionSyntax collectionExpression) {
            return TryBuildKnownEnumerableRootFromCollectionExpression(
                bodySemanticModel,
                collectionExpression,
                definition,
                definition.ContainingType,
                ref uniqueNumber,
                out fragment,
                unsafeAccessorCollector,
                replacementMap);
        }

        return false;
    }

    private static bool TryBuildTransparentParameterEnumerableRoot(
        SemanticModel invocationSemanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodDefinition,
        INamedTypeSymbol invocationContainingType,
        ExpressionSyntax bodyExpression,
        SemanticModel bodySemanticModel,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        bodyExpression=StripParenthesesForRootInlining(bodyExpression);
        if(bodyExpression is not IdentifierNameSyntax identifier
            || bodySemanticModel.GetSymbolInfo(identifier).Symbol is not IParameterSymbol parameter) {
            return false;
        }

        var parameterIndex = -1;
        for(var i=0;i<methodDefinition.Parameters.Length;i++) {
            if(SymbolEqualityComparer.Default.Equals(methodDefinition.Parameters[i],parameter)
                || methodDefinition.Parameters[i].Name==parameter.Name) {
                parameterIndex=i;
                break;
            }
        }
        if(parameterIndex<0
            || !TryGetInvocationArgumentExpression(methodDefinition,invocation,parameterIndex,out var sourceExpression)) {
            return false;
        }

        return TryBuildEnumerableRootFromExpressionWithReplacement(
            invocationSemanticModel,
            sourceExpression,
            invocationContainingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap);
    }

    private static bool TryGetInvocationArgumentExpression(
        IMethodSymbol methodDefinition,
        InvocationExpressionSyntax invocation,
        int parameterIndex,
        out ExpressionSyntax expression) {
        expression=null!;
        if(parameterIndex<0 || parameterIndex>=methodDefinition.Parameters.Length) {
            return false;
        }

        var hasReducedExtensionReceiver =
            methodDefinition.IsExtensionMethod
            && invocation.Expression is MemberAccessExpressionSyntax
            && invocation.ArgumentList.Arguments.Count<methodDefinition.Parameters.Length;
        if(hasReducedExtensionReceiver
            && parameterIndex==0
            && invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess) {
            expression=extensionMemberAccess.Expression;
            return true;
        }

        var nextParameterIndex = hasReducedExtensionReceiver ? 1 : 0;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=0;i<methodDefinition.Parameters.Length;i++) {
                    if(methodDefinition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                targetParameterIndex=nextParameterIndex++;
            }

            if(targetParameterIndex==parameterIndex) {
                expression=argument.Expression;
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax StripParenthesesForRootInlining(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        return expression;
    }

    private static bool TryGetEnumerableElementTypeForRootInlining(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType) && namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate=>IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    useEnumerableInterface=false;
                    return true;
                }
                break;
        }

        elementType=null!;
        useEnumerableInterface=false;
        return false;
    }

    private static (string? typeName, bool isDuckTyping) GetConcreteEnumeratorTypeForRootInlining(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface || sourceType is not INamedTypeSymbol namedSource) {
            return (null, false);
        }

        var getEnumerator = namedSource.GetMembers("GetEnumerator")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.IsEmpty
                && m.DeclaredAccessibility==Accessibility.Public
                && m.MethodKind!=MethodKind.ExplicitInterfaceImplementation
                && !m.IsStatic);
        if(getEnumerator is null
            || getEnumerator.ReturnType is not INamedTypeSymbol returnType
            || returnType.IsAbstract
            || returnType.TypeKind==TypeKind.Interface) {
            return (null, false);
        }

        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var implementsIEnumerator = returnType.AllInterfaces.Any(i =>
            i.ContainingNamespace?.ToDisplayString()=="System.Collections.Generic"
            && i.MetadataName=="IEnumerator`1"
            && i.TypeArguments.Length==1
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0],elementType));
        return (typeName, isDuckTyping: !implementsIEnumerator);
    }

    private static string RenderExpressionForRootInlining(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        var text = RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        if(replacementMap is null) {
            return text;
        }

        foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
            text=text.Replace(NormalizeGeneratedExpressionText(pair.Key),pair.Value,StringComparison.Ordinal);
        }
        return text;
    }

    private static bool TryBuildEnumerableRootFromExpressionWithReplacement(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        expression=StripParenthesesForRootInlining(expression);
        if(TryBuildKnownEnumerableRoot(semanticModel,expression,containingType,ref uniqueNumber,out fragment,unsafeAccessorCollector,replacementMap)) {
            return true;
        }

        var typeInfo = semanticModel.GetTypeInfo(expression);
        var expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if(!TryGetEnumerableElementTypeForRootInlining(expressionType,out var elementType,out var useEnumerableInterface)) {
            if(IsNonGenericIEnumerableType(expressionType)) {
                fragment=new NonGenericRoot(
                    uniqueNumber++,
                    RenderExpressionForRootInlining(expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap));
                return true;
            }

            return false;
        }

        var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var (concreteEnumeratorTypeName, isDuckTyping) = GetConcreteEnumeratorTypeForRootInlining(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ?? $"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        var effectiveUseEnumerableInterface = useEnumerableInterface || concreteEnumeratorTypeName is null;
        var sourceExpression=RenderExpressionForRootInlining(expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        var number=uniqueNumber++;
        fragment = expressionType is IArrayTypeSymbol
            ? new ArrayRoot(number, sourceExpression, elementTypeName)
            : IsListType(expressionType)
                ? new ListRoot(number, sourceExpression, elementTypeName)
                : IsTsavoriteConcreteHashSetType(expressionType,out _)
                    ? new TsavoriteHashSetRoot(number, sourceExpression, elementTypeName, enumeratorTypeName)
                : isDuckTyping && concreteEnumeratorTypeName is not null
                    ? new DuckTypingRoot(number, sourceExpression, elementTypeName, concreteEnumeratorTypeName)
                    : CreateRootEnumerableFragment(sourceExpression, elementTypeName, enumeratorTypeName, effectiveUseEnumerableInterface, number);
        return true;
    }

    private static bool TryBuildKnownEnumerableRootFromCollectionExpression(
        SemanticModel semanticModel,
        CollectionExpressionSyntax collectionExpression,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
#if false
        if(!蛻玲嫌隕∫ｴ蝙句叙蠕・method.ReturnType,out var elementType,out _)) {
            return false;
        }

        var elementTypeName=elementType.ToDisplayString(螳悟・菫ｮ鬟ｾ蠖｢蠑・;
#endif
        if(!TryGetCollectionExpressionElementType(method.ReturnType,out var elementType)) {
            return false;
        }

        var elementTypeName=elementType.ToDisplayString(global::Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        if(collectionExpression.Elements.Count==0) {
            fragment=new EmptyRoot(uniqueNumber++,elementTypeName);
            return true;
        }

        if(collectionExpression.Elements is [
            ExpressionElementSyntax { Expression: var elementExpression }
        ]) {
            fragment=new RepeatRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(elementExpression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                "1",
                elementTypeName);
            return true;
        }

        return false;
    }

    private static bool TryGetCollectionExpressionElementType(ITypeSymbol type,out ITypeSymbol elementType) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType) && namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate=>IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    return true;
                }
                break;
        }

        elementType=null!;
        return false;
    }

    private static bool TryGetSingleExpressionBody(MethodDeclarationSyntax methodSyntax,out ExpressionSyntax expression) {
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            expression=expressionBody;
            return true;
        }

        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            expression=returnExpression;
            return true;
        }

        expression=null!;
        return false;
    }

    private static Dictionary<string,string> BuildInlineEnumerableRootReplacementMap(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax methodSyntax,
        ExpressionSyntax bodyExpression,
        SemanticModel bodySemanticModel,
        SemanticModel invocationSemanticModel,
        INamedTypeSymbol invocationContainingType,
        IReadOnlyList<string> actualArguments,
        List<string>? unsafeAccessorCollector) {
        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=0;i<method.Parameters.Length && i<actualArguments.Count;i++) {
            replacementMap[method.Parameters[i].Name]=actualArguments[i];
        }

        var receiverText = GetInlineEnumerableRootReceiverText(
            invocation,
            invocationSemanticModel,
            invocationContainingType,
            unsafeAccessorCollector);
        foreach(var identifier in bodyExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            if(identifier.Parent is MemberAccessExpressionSyntax { Name: var name } && name==identifier) {
                continue;
            }

            if(bodySemanticModel.GetSymbolInfo(identifier).Symbol is IFieldSymbol field
                && !field.IsStatic
                && field.DeclaredAccessibility!=Accessibility.Public) {
                replacementMap[identifier.Identifier.ValueText]=RenderFieldAccessorCall(field,receiverText,unsafeAccessorCollector);
            }
        }

        foreach(var memberAccess in bodyExpression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
            if(bodySemanticModel.GetSymbolInfo(memberAccess).Symbol is IFieldSymbol field
                && !field.IsStatic
                && field.DeclaredAccessibility!=Accessibility.Public) {
                var memberText = NormalizeGeneratedExpressionText(memberAccess.ToString())
                    .Replace("this.","@this.",StringComparison.Ordinal);
                replacementMap[memberText]=RenderFieldAccessorCall(field,receiverText,unsafeAccessorCollector);
            }
        }

        return replacementMap;
    }

    private static string GetInlineEnumerableRootReceiverText(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector) {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector)
            : "@this";
    }

    private static string RenderFieldAccessorCall(IFieldSymbol field,string receiverText,List<string>? unsafeAccessorCollector) {
        var accessorName = $"__Field_{SanitizeIdentifier(field.ContainingType.Name)}_{SanitizeIdentifier(field.Name)}";
        var targetTypeName = field.ContainingType.ToDisplayString(完全修飾形式);
        var fieldTypeName = field.Type.ToDisplayString(完全修飾形式);
        var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}({targetTypeName} @this);";
        unsafeAccessorCollector?.Add(accessorCode);
        return $"{accessorName}({receiverText})";
    }

    // ブロックが「単一の値付き return 文だけ」かどうか。これに該当するものは式インライン（LambdaFunc）で扱う。
    private static bool IsSingleReturnBlock(BlockSyntax block) =>
        block.Statements is [ReturnStatementSyntax { Expression: not null }];

    /// <summary><c>delegate(params) {}</c> のパラメータ名リストを返す。引数リストがない場合は空リスト。</summary>
    private static List<string> GetAnonymousMethodParameters(AnonymousMethodExpressionSyntax anonymousMethod) =>
        anonymousMethod.ParameterList?.Parameters
            .Select(static parameter=>parameter.Identifier.ValueText)
            .ToList() ?? [];

    // 複数文ブロック本体（try-catch 含む）を文インライン展開する BlockFunc を構築する。
    // return 文は __Tsavorite_DEST__ への代入 + goto __Tsavorite_LABEL__ へ書き換える。
    private static bool TryBuildBlockFunc(
        BlockSyntax block,
        IReadOnlyList<string> parameters,
        ITypeSymbol delegateType,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        out BlockFunc fragment) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        // return をプレースホルダ代入 + goto に書き換える。ネストした lambda/local function には降りない。
        var rewritten = (BlockSyntax)new ReturnToResultRewriter().Visit(block)!;
        var bodyText = string.Join(
            "\n",
            rewritten.Statements.Select(statement=>NormalizeGeneratedExpressionText(statement.ToString())));

        var capturedLocals = Array.Empty<(string Name, string TypeName)>();
        if(semanticModel is not null && block.Statements.Count>0) {
            var dataFlow = semanticModel.AnalyzeDataFlow(block.Statements[0],block.Statements[^1]);
            if(dataFlow?.Succeeded==true) {
                var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
                capturedLocals = dataFlow.DataFlowsIn
                    .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                    .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                        ? loc.Type.ToDisplayString(完全修飾形式)
                        : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                    .Distinct()
                    .ToArray();
            }
        }

        fragment = new BlockFunc {
            ReturnTypeName = typeArguments[^1],
            ParameterTypeNames = parameterTypeNames,
            ParameterNames = parameters.ToArray(),
            CanGenerateMethod = false,
            CanInlineAssign = true,
            RequiresStatementInlining = true,
            BlockBody = bodyText,
            CapturedLocals = capturedLocals,
        };
        return true;
    }

    // ブロック本体内の return 文を __Tsavorite_DEST__ = (expr); goto __Tsavorite_LABEL__; へ書き換える。
    // ネストした lambda / 匿名メソッド / local function の return は対象外（中へ降りない）。
    private sealed class ReturnToResultRewriter : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)=>node;
        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)=>node;
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) {
            var statementText = node.Expression is { } expr
                ? $"{{ __Tsavorite_DEST__ = ({expr}); goto __Tsavorite_LABEL__; }}"
                : "{ goto __Tsavorite_LABEL__; }";
            return SyntaxFactory.ParseStatement(statementText);
        }
    }

    /// <summary>
    /// ラムダ式から <see cref="LambdaSpec"/>（戻り値型・パラメータ型・パラメータ名・本体）を抽出する。
    /// 本体が式本体か単一 <c>return</c> 文の場合のみ成功する。
    /// デリゲート型が <c>Func&lt;…&gt;</c> でない場合・匿名型が混在する場合も失敗を返す。
    /// </summary>
    private static bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,ITypeSymbol delegateType,out LambdaSpec fragment) {
        fragment=null!;
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters.ToArray(),
            NormalizeExpressionText(body.ToString()));
        return true;
    }

    /// <summary>
    /// <c>delegate(params) { return expr; }</c> の単一 return 本体から <see cref="LambdaSpec"/> を抽出する。
    /// 複数文ブロックや戻り値なし本体は失敗する（<see cref="TryBuildBlockFunc"/> の対象になる）。
    /// </summary>
    private static bool TryCreateAnonymousMethodFuncFragment(
        AnonymousMethodExpressionSyntax anonymousMethod,
        ITypeSymbol delegateType,
        out LambdaSpec fragment,
        out ExpressionSyntax body) {
        fragment=null!;
        body=null!;

        if(anonymousMethod.Block.Statements is not [
            ReturnStatementSyntax { Expression: { } returnExpression }
        ]) {
            return false;
        }

        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var parameters = anonymousMethod.ParameterList?.Parameters
            .Select(static parameter=>parameter.Identifier.ValueText)
            .ToArray() ?? [];
        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Length!=parameterTypeNames.Length) {
            return false;
        }

        body=returnExpression;
        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters,
            NormalizeExpressionText(returnExpression.ToString()));
        return true;
    }

    private static bool IsInlineDelegateType(INamedTypeSymbol namedType) {
        // SelectMany では selector が IEnumerable<T> を返すため、
        // 戻り値がシーケンスでも FuncFragment 化できるようにしておく。
        // 実際にどの Fragment で受け入れるかは後段の候補選択側で決まる。
        return namedType.ContainingNamespace.ToDisplayString()=="System"
            && namedType.MetadataName is "Func`2" or "Func`3";
    }

    /// <summary>型が <c>System.Collections.Generic.IEnumerable&lt;T&gt;</c> そのものかを判定する。</summary>
    private static bool IsIEnumerableType(INamedTypeSymbol namedType)
       =>namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            && namedType.MetadataName=="IEnumerable`1";

    /// <summary>型またはその実装インターフェースに非ジェネリック <c>System.Collections.IEnumerable</c> が含まれるかを判定する。</summary>
    private static bool IsNonGenericIEnumerableType(ITypeSymbol? type)
       =>type is INamedTypeSymbol namedType
            && (IsNonGenericIEnumerableType(namedType)
                || namedType.AllInterfaces.Any(IsNonGenericIEnumerableType));

    /// <summary>型が <c>System.Collections.IEnumerable</c>（非ジェネリック）そのものかを判定する。</summary>
    private static bool IsNonGenericIEnumerableType(INamedTypeSymbol namedType)
       =>namedType.ContainingNamespace.ToDisplayString()=="System.Collections"
            && namedType.MetadataName=="IEnumerable";

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type.IsAnonymousType) {
            return true;
        }

        // 配列やジェネリックの内側に匿名型が埋まっていても生成署名には出せないので再帰的に見る。
        return type switch {
            IArrayTypeSymbol arrayType=>ContainsAnonymousType(arrayType.ElementType),
            INamedTypeSymbol namedType=>namedType.TypeArguments.Any(ContainsAnonymousType),
            _=>false,
        };
    }

    // 匿名型を "TResult" ジェネリック型パラメータに置き換えた型名を返す。
    // 例: TAnon → "TResult", IEnumerable<TAnon> → "global::System.Collections.Generic.IEnumerable<TResult>"
    private static string ReplaceAnonymousTypesWithTResult(ITypeSymbol type) {
        if(type.IsAnonymousType) return "TResult";
        if(type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsAnonymousType)) {
            var openName = OpenGenericTypeName(named);
            var args = string.Join(", ", named.TypeArguments.Select(a =>
                ContainsAnonymousType(a) ? ReplaceAnonymousTypesWithTResult(a) : a.ToDisplayString(完全修飾形式)));
            return $"{openName}<{args}>";
        }
        return type.ToDisplayString(完全修飾形式);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) {
        // 開いた型パラメータが残っていると生成メソッド側の署名が閉じないため再帰的に検査する。
        return type switch {
            ITypeParameterSymbol=>true,
            IArrayTypeSymbol arrayType=>ContainsTypeParameter(arrayType.ElementType),
            INamedTypeSymbol namedType=>namedType.TypeArguments.Any(ContainsTypeParameter),
            IPointerTypeSymbol pointerType=>ContainsTypeParameter(pointerType.PointedAtType),
            _=>false,
        };
    }

    // メソッド自身の型パラメーター（ownParams）以外の開いた型パラメーターが型に含まれるか検査する。
    // メソッドレベルのジェネリックパラメーターのみを持つ型は生成シグネチャに出せるため通過させる。
    private static bool ContainsExternalTypeParameter(ITypeSymbol type,ImmutableArray<ITypeParameterSymbol> ownParams) {
        if(ownParams.IsDefaultOrEmpty) {
            return ContainsTypeParameter(type);
        }
        var ownNames = new HashSet<string>(ownParams.Select(static p=>p.Name),StringComparer.Ordinal);
        return ContainsExternalTypeParameterCore(type,ownNames);
    }

    private static bool ContainsExternalTypeParameterCore(ITypeSymbol type,HashSet<string> ownNames) => type switch {
        ITypeParameterSymbol tp=>!ownNames.Contains(tp.Name),
        IArrayTypeSymbol arr=>ContainsExternalTypeParameterCore(arr.ElementType,ownNames),
        INamedTypeSymbol named=>named.TypeArguments.Any(a=>ContainsExternalTypeParameterCore(a,ownNames)),
        _=>false,
    };

    private static bool ContainsGeneratedTypeAccessibilityRisk(ITypeSymbol type) {
        // 生成クラスから見えない型がどこかに混ざると署名生成時点でコンパイル不能になる。
        return type switch {
            IArrayTypeSymbol arrayType=>ContainsGeneratedTypeAccessibilityRisk(arrayType.ElementType),
            IPointerTypeSymbol pointerType=>ContainsGeneratedTypeAccessibilityRisk(pointerType.PointedAtType),
            INamedTypeSymbol namedType=>NamedTypeAccessibilityRisk(namedType) || namedType.TypeArguments.Any(ContainsGeneratedTypeAccessibilityRisk),
            _=>false,
        };
    }

    private static bool NamedTypeAccessibilityRisk(INamedTypeSymbol namedType) {
        if(namedType.IsAnonymousType) {
            return true;
        }

        if(namedType.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal) {
            return true;
        }

        return namedType.ContainingType is not null && NamedTypeAccessibilityRisk(namedType.ContainingType);
    }

    /// <summary>
    /// ラムダの本体を式として取り出す。
    /// 式本体ラムダ（<c>x =&gt; expr</c>）はそのまま、単一 <c>return</c> 文ブロック（<c>x =&gt; { return expr; }</c>）は
    /// return の式を返す。それ以外は false。
    /// </summary>
    private static bool TryGetLambdaExpressionBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        body=null!;
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        if(lambda.Body is BlockSyntax {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            body=returnExpression;
            return true;
        }
        return false;
    }

    /// <summary>ラムダ式のパラメータ名リストを返す。単純ラムダは 1 要素、括弧ラムダは全パラメータ名。</summary>
    private static List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return [simpleLambda.Parameter.Identifier.ValueText];
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter=>parameter.Identifier.ValueText).ToList();
        }
        return [];
    }
}
