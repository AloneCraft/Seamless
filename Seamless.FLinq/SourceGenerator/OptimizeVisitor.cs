using Microsoft.CodeAnalysis.CSharp;
using static Seamless.FLinq.CarrierAnalysis;
namespace Seamless.FLinq;
// method-chain 繧堤ｿｻ險ｳ縺励ｄ縺吶＞蠖｢縺ｫ豁｣隕丞喧縺吶ｋ syntax rewriter縲・
// 蠖ｹ蜑ｲ縺ｯ2縺､縲・
//   1. QueryToExtensionMethodRewriter: from a in xs where ... select ...竊遅s.Where(a=>...).Select(a=>...)
//   2. OptimizeVisitor 譛ｬ菴・ Where 繧・carrier 縺ｮ騾・ｧ｣譫舌〒闡峨∈謚ｼ縺嶺ｸ九￡繧・
// carrier 縺ｮ騾・ｧ｣譫舌◎縺ｮ繧ゅ・縺ｯ CarrierAnalysis 縺梧戟縺､(蟆・ｽｱ陞榊粋 ProjectionMergeVisitor 縺ｨ蜈ｱ譛・縲・
// 隗｣譫舌〒縺阪↑縺・こ繝ｼ繧ｹ縺ｧ縺ｯ蠑上ｒ螟峨∴縺壹↓霑斐☆(螳牙・蛛ｴ)縲・
internal sealed class OptimizeVisitor:CSharpSyntaxRewriter {
    // 繧ｯ繧ｨ繝ｪ蠑鞘・諡｡蠑ｵ繝｡繧ｽ繝・ラ繝√ぉ繝ｼ繝ｳ縲・
    // 蜈・・ SemanticModel 縺ｧ譁ｰ隕上ヮ繝ｼ繝峨ｒ逶ｴ謗･譚溽ｸ帙〒縺阪↑縺・◆繧√（nterceptor 蛛ｴ縺ｧ縺ｯ荳九￡縺滓ｧ区枚譛ｨ繧剃ｸ譎・Compilation 縺ｫ蜈･繧後※縺九ｉ菴ｿ縺・・
    private sealed class QueryToExtensionMethodRewriter:CSharpSyntaxRewriter {
        private int transparentNumber;
        public override SyntaxNode? VisitQueryExpression(QueryExpressionSyntax node) =>
            CanLowerQuery(node) ? this.LowerQueryExpression(node) : base.VisitQueryExpression(node);
        private static bool CanLowerQuery(QueryExpressionSyntax query) => CanLowerQueryBody(query.Body)&&CanLowerAdvancedShape(query);
        private static bool CanLowerQueryBody(QueryBodySyntax body) {
            if(body.SelectOrGroup is not SelectClauseSyntax and not GroupClauseSyntax) {
                return false;
            }
            if(body.Clauses.Any(static clause => clause is not WhereClauseSyntax and not OrderByClauseSyntax and not LetClauseSyntax and not FromClauseSyntax and not JoinClauseSyntax)) {
                return false;
            }
            return body.Continuation is null||CanLowerQueryBody(body.Continuation.Body);
        }
        // 莉･蜑阪・螟壽ｮｵ join / 隍・焚 from / group+邯咏ｶ・/ let / this 繧ｽ繝ｼ繧ｹ繧貞ｰ∝魂縺励※縺・◆縺後・
        // 蜷・Lower* 縺ｯ state.Bindings 繧偵ヵ繝ｩ繝・ヨ蜀榊ｰ・ｽｱ縺吶ｋ菴懊ｊ縺ｧ蜴溽炊逧・↓螟壽ｮｵ騾｣骼門庄閭ｽ縺ｧ縺ゅｊ縲・
        // 蛹ｿ蜷榊梛縺ｯ record struct registry 縺ｧ蜷肴欠縺怜庄閭ｽ蛹悶＆繧後ｋ縺溘ａ縲√し繝昴・繝育ｯ遞ｮ蛻･縺ｮ讀懈渊
        //・・anLowerQueryBody・峨′騾壹ｌ縺ｰ lowering 繧定ｩｦ縺ｿ繧九・
        private static bool CanLowerAdvancedShape(QueryExpressionSyntax query) => true;
        private ExpressionSyntax LowerQueryExpression(QueryExpressionSyntax query) {
            var from = query.FromClause;
            var source = (ExpressionSyntax)this.Visit(from.Expression)!;
            var state = QueryState.FromRange(from.Identifier.ValueText,from.Type);
            return this.LowerQueryBody(source,state,query.Body);
        }
        private ExpressionSyntax LowerQueryBody(ExpressionSyntax source,QueryState state,QueryBodySyntax body) {
            foreach(var clause in body.Clauses) {
                switch(clause) {
                    case WhereClauseSyntax whereClause:
                        source=CreateSequenceCall(
                            source,
                            "Where",
                            CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)this.Visit(whereClause.Condition)!)));
                        break;
                    case OrderByClauseSyntax orderByClause:
                        source=this.LowerOrderByClause(source,state,orderByClause);
                        break;
                    case LetClauseSyntax letClause:
                        source=this.LowerLetClause(source,ref state,letClause);
                        break;
                    case FromClauseSyntax fromClause:
                        source=this.LowerFromClause(source,ref state,fromClause);
                        break;
                    case JoinClauseSyntax joinClause:
                        source=this.LowerJoinClause(source,ref state,joinClause);
                        break;
                    default:
                        return source;
                }
            }

            source=this.LowerSelectOrGroup(source,state,body.SelectOrGroup);
            if(body.Continuation is not { } continuation) {
                return source;
            }

            return this.LowerQueryBody(
                source,
                QueryState.FromRange(continuation.Identifier.ValueText,null),
                continuation.Body);
        }
        private ExpressionSyntax LowerOrderByClause(ExpressionSyntax source,QueryState state,OrderByClauseSyntax orderByClause) {
            var orderings = orderByClause.Orderings;
            for(var i = 0;i<orderings.Count;i++) {
                var ordering = orderings[i];
                var descending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                var method = i==0
                    ? descending ? "OrderByDescending" : "OrderBy"
                    : descending ? "ThenByDescending" : "ThenBy";
                source=CreateSequenceCall(
                    source,
                    method,
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)this.Visit(ordering.Expression)!)));
            }
            return source;
        }
        private ExpressionSyntax LowerLetClause(ExpressionSyntax source,ref QueryState state,LetClauseSyntax letClause) {
            var newBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            newBindings[letClause.Identifier.ValueText]=state.Rewrite((ExpressionSyntax)this.Visit(letClause.Expression)!);
            var selectorBody = CreateTransparentTuple(newBindings);
            source=CreateSequenceCall(source,"Select",CreateLambda(state.ParameterName,state.ParameterType,selectorBody.Expression));
            state=QueryState.FromTransparent(this.FreshTransparentName(),selectorBody.Members);
            return source;
        }
        private ExpressionSyntax LowerFromClause(ExpressionSyntax source,ref QueryState state,FromClauseSyntax fromClause) {
            var collectionSelector = CreateLambda(
                state.ParameterName,
                state.ParameterType,
                state.Rewrite((ExpressionSyntax)this.Visit(fromClause.Expression)!));
            var resultBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            resultBindings[fromClause.Identifier.ValueText]=SyntaxFactory.IdentifierName(fromClause.Identifier);
            var resultTuple = CreateTransparentTuple(resultBindings);
            var resultSelector = CreateLambda(
                [
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(fromClause.Identifier.ValueText,fromClause.Type)
                ],
                resultTuple.Expression);
            source=CreateSequenceCall(source,"SelectMany",collectionSelector,resultSelector);
            state=QueryState.FromTransparent(this.FreshTransparentName(),resultTuple.Members);
            return source;
        }
        private ExpressionSyntax LowerJoinClause(ExpressionSyntax source,ref QueryState state,JoinClauseSyntax joinClause) {
            var innerSource = (ExpressionSyntax)this.Visit(joinClause.InExpression)!;
            var outerKey = CreateLambda(
                state.ParameterName,
                state.ParameterType,
                state.Rewrite((ExpressionSyntax)this.Visit(joinClause.LeftExpression)!));
            var innerKey = CreateLambda(
                joinClause.Identifier.ValueText,
                joinClause.Type,
                (ExpressionSyntax)this.Visit(joinClause.RightExpression)!);
            var resultBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            var method = joinClause.Into is null ? "Join" : "GroupJoin";
            var resultName = joinClause.Into?.Identifier.ValueText??joinClause.Identifier.ValueText;
            resultBindings[resultName]=SyntaxFactory.IdentifierName(resultName);
            var resultTuple = CreateTransparentTuple(resultBindings);
            var resultSelector = CreateLambda(
                [
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(resultName,joinClause.Into is null ? joinClause.Type : null)
                ],
                resultTuple.Expression);
            source=CreateSequenceCall(source,method,innerSource,outerKey,innerKey,resultSelector);
            state=QueryState.FromTransparent(this.FreshTransparentName(),resultTuple.Members);
            return source;
        }
        private ExpressionSyntax LowerSelectOrGroup(ExpressionSyntax source,QueryState state,SelectOrGroupClauseSyntax selectOrGroup) {
            return selectOrGroup switch {
                SelectClauseSyntax selectClause => CreateSequenceCall(
                    source,
                    "Select",
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)this.Visit(selectClause.Expression)!))),
                GroupClauseSyntax groupClause => CreateSequenceCall(
                    source,
                    "GroupBy",
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)this.Visit(groupClause.ByExpression)!)),
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)this.Visit(groupClause.GroupExpression)!))),
                _ => source,
            };
        }
        private string FreshTransparentName() => $"__q{this.transparentNumber++}";
        private static (TupleExpressionSyntax Expression,Dictionary<string,string> Members) CreateTransparentTuple(IReadOnlyDictionary<string,ExpressionSyntax> bindings) {
            var arguments = new List<ArgumentSyntax>();
            var members = new Dictionary<string,string>(StringComparer.Ordinal);
            var index = 1;
            foreach(var pair in bindings) {
                var memberName = $"Item{index++}";
                var argument = IsReservedTupleElementName(pair.Key)
                    ? SyntaxFactory.Argument(SyntaxFactory.ParenthesizedExpression(pair.Value))
                    : SyntaxFactory.Argument(pair.Value);
                if(!IsReservedTupleElementName(pair.Key)) {
                    argument=argument.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(pair.Key)));
                    memberName=pair.Key;
                }
                arguments.Add(argument);
                members[pair.Key]=memberName;
            }
            return (SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(arguments)),members);
        }
        private static bool IsReservedTupleElementName(string name) =>
            name=="Rest"
            ||name.Length>4
            &&name.StartsWith("Item",StringComparison.Ordinal)
            &&name[4] is >='1' and <='9'
            &&name.Skip(5).All(static c => c is >='0' and <='9');
        private static LambdaExpressionSyntax CreateLambda(string parameterName,TypeSyntax? parameterType,ExpressionSyntax body) {
            if(parameterType is null) {
                return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),body);
            }
            return SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(CreateParameter(parameterName,parameterType))),
                body);
        }
        private static LambdaExpressionSyntax CreateLambda(IEnumerable<ParameterSyntax> parameters,ExpressionSyntax body) =>
            SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)),
                body);
        private static ParameterSyntax CreateParameter(string name,TypeSyntax? type) =>
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)).WithType(type);
        private sealed class QueryState(string parameterName,TypeSyntax? parameterType,Dictionary<string,ExpressionSyntax> bindings) {
            public string ParameterName { get; } = parameterName;
            public TypeSyntax? ParameterType { get; } = parameterType;
            public Dictionary<string,ExpressionSyntax> Bindings { get; } = bindings;
            public static QueryState FromRange(string parameterName,TypeSyntax? parameterType) =>
                new(parameterName,parameterType,new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                    [parameterName]=SyntaxFactory.IdentifierName(parameterName),
                });
            public static QueryState FromTransparent(string parameterName,IReadOnlyDictionary<string,string> members) =>
                new(parameterName,null,members.ToDictionary(
                    static pair => pair.Key,
                    pair => (ExpressionSyntax)SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(parameterName),
                        SyntaxFactory.IdentifierName(pair.Value)),
                    StringComparer.Ordinal));
            public ExpressionSyntax Rewrite(ExpressionSyntax expression) =>
                new QueryBindingRewriter(this.Bindings).Visit(expression) as ExpressionSyntax??expression;
        }
        private sealed class QueryBindingRewriter(IReadOnlyDictionary<string,ExpressionSyntax> bindings):CSharpSyntaxRewriter {
            public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
                if(!bindings.TryGetValue(node.Identifier.ValueText,out var replacement)||IsNonValueIdentifier(node)) {
                    return base.VisitIdentifierName(node);
                }
                return replacement.WithTriviaFrom(node);
            }
            private static bool IsNonValueIdentifier(IdentifierNameSyntax node) =>
                node.Parent is MemberAccessExpressionSyntax memberAccess&&memberAccess.Name==node
                ||node.Parent is NameEqualsSyntax
                ||node.Parent is NameColonSyntax
                ||node.Parent is MemberBindingExpressionSyntax;
        }
    }
    // Distinct は要素そのものをキーにするため、同じキーの要素は必ず同じ述語結果を持つ。
    // どれが代表として残っても結果は変わらないので押し下げてよい。
    private static readonly HashSet<string> PushWhereThroughUnaryMethods = new(StringComparer.Ordinal){
        "Distinct","Order","OrderBy","OrderDescending","OrderByDescending","Reverse",
    };
    private static readonly HashSet<string> OrderMethods = new(StringComparer.Ordinal){
        "Order","OrderBy","OrderDescending","OrderByDescending",
    };
    // 【再追加禁止】DistinctBy/ExceptBy/IntersectBy/UnionBy への押し下げは実装しない。
    // DistinctBy は一度実装していたが不健全だったため除去した(SetOperationPushdown.
    // DistinctByDuplicateKeyRegression が退行テスト)。By系はキーで代表を1つ選ぶ操作
    // (FIRST_VALUE() OVER (PARTITION BY key) 相当)を内包しており、同じキーでも値・述語の
    // 結果が異なる要素が存在しうるため、フィルタを先に通すと代表選択自体が変わり結果が変わる。
    //   src=[(Key:1,Val:1),(Key:1,Val:2)] に対し
    //   src.DistinctBy(x=>x.Key).Where(x=>x.Val==2) は 代表(1,1) が述語で落ちて []
    //   src.Where(x=>x.Val==2).DistinctBy(x=>x.Key) は (1,2) が代表になり [(1,2)]
    // keySelector の単射性は静的に判定できないため、By系は一律に対象外とする。
    // Except/Intersect(非By)は要素そのものがキーなので選択の分配則が成り立ち、押し下げてよい。
    private static readonly HashSet<string> PushWhereThroughFirstSourceMethods = new(StringComparer.Ordinal){
        "Except","Intersect",
    };
    // 【再追加禁止】Concat への押し下げは実装しない。一度実装して計測したが利得がゼロだった。
    // σ_p(A++B) = σ_p(A)++σ_p(B) は成り立つものの、述語評価回数も通過要素数も変わらないため
    // 実行時間は原理的に改善しない。一方で述語が両辺へ複製されて生成コードが倍増し、
    // 第2ソース側は引数位置にあってフラグメント化されないので素の System.Linq.Where
    // (boxing + デリゲート + interface dispatch)へ落ちる。差し引きで損になる。
    // Union は残す。ハッシュ集合へ入る要素数が実際に減るので利得がある。
    private static readonly HashSet<string> PushWhereThroughBothSourceMethods = new(StringComparer.Ordinal){
        "Union",
    };
    public static TSyntax RewriteQueriesToExtensionMethods<TSyntax>(TSyntax syntax) where TSyntax : SyntaxNode {
        return new QueryToExtensionMethodRewriter().Visit(syntax) as TSyntax??syntax;
    }
    private readonly SemanticModel model;
    private readonly INamedTypeSymbol? enumerableType;
    private OptimizeVisitor(SemanticModel model) {
        this.model=model;
        this.enumerableType=model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
    }
    /// <summary>
    /// Where 繧・carrier 縺ｮ騾・ｧ｣譫舌〒闡峨∈謚ｼ縺嶺ｸ九￡繧九・ 繝代せ縺ｫ縺､縺咲ｵ瑚ｷｯ縺斐→ 1 譖ｸ謠帙□縺題｡後＞縲・
    /// 蜷域・繝弱・繝峨↓縺ｯ諢丞袖隲悶ご繝ｼ繝医ｒ謗帙￠繧峨ｌ縺ｪ縺・◆繧∝庶譚溘・蜻ｼ縺ｳ蜃ｺ縺怜・縺ｮ蜀肴據邵帙Ν繝ｼ繝励∈蟋斐・繧九・
    /// </summary>
    public static TSyntax Optimize<TSyntax>(TSyntax node,SemanticModel model) where TSyntax : SyntaxNode {
        return new OptimizeVisitor(model).Visit(node) as TSyntax??node;
    }
    // 蜈・ヮ繝ｼ繝峨・荳翫〒蜈医↓譖ｸ謠帙ｒ隧ｦ縺励∵・遶九＠縺ｪ縺代ｌ縺ｰ蟄舌∈髯阪ｊ繧・AlgebraicRewriter 縺ｨ蜷後§蠖｢)縲・
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var optimized = this.TryOptimizeInvocation(node);
        if(optimized is not null) return optimized;
        return base.VisitInvocationExpression(node);
    }
    // Where逶ｴ荳九・sequence call縺ｮ遞ｮ鬘槭〒謚ｼ縺嶺ｸ九￡謌ｦ逡･繧貞・蟯舌・
    private InvocationExpressionSyntax? TryOptimizeInvocation(InvocationExpressionSyntax invocation) {
        if(!this.TryGetLinqSequenceCall(invocation,out var wc)
            ||wc.MethodName!="Where"||wc.Arguments.Count!=1
            ||wc.Arguments[0].Expression is not LambdaExpressionSyntax wl
            ||GetLambdaParameterNames(wl).Count!=1
            ||wc.Source is not InvocationExpressionSyntax ui
            ||!this.TryGetLinqSequenceCall(ui,out var uc)) return null;
        if(uc.MethodName=="Select") return this.TryPushWhereThroughSelect(uc,wl);
        if(uc.MethodName=="SelectMany") return this.TryPushWhereThroughSelectMany(uc,wl);
        if(uc.MethodName is "Join" or "LeftJoin") return this.TryPushWhereThroughBinaryCarrier(uc,wl,3);
        if(PushWhereThroughUnaryMethods.Contains(uc.MethodName)||PushWhereThroughFirstSourceMethods.Contains(uc.MethodName)) {
            if(OrderMethods.Contains(uc.MethodName)&&this.KeepsSortedSpanFastPath(uc)) return null;
            return PushWhereBeforeSource(uc,wl);
        }
        if(PushWhereThroughBothSourceMethods.Contains(uc.MethodName)) return TryPushWhereThroughBothSources(uc,wl);
        return null;
    }
    // Order 縺後ヱ繧､繝励Λ繧､繝ｳ蜈磯ｭ縺ｧ繧ｽ繝ｼ繧ｹ縺後せ繝代Φ繧貞・縺帙ｋ縺ｨ縺阪∫函謌仙勣縺ｯ繧ｽ繝ｼ繝域ｸ医∩繧ｹ繝代Φ鬮倬溘ヱ繧ｹ
    // (PipelineEnumerable.OrderLoop 縺ｮ CanUseSortedDefaultOrderFastPathCore) 繧帝∈縺ｶ縲・
    // Where 繧貞燕縺ｸ謖ｿ縺吶→ Order 縺悟・鬆ｭ縺ｧ縺ｪ縺上↑繧翫√ヰ繝・ヵ繧｡+髢捺磁繧､繝ｳ繝・ャ繧ｯ繧ｹ繧ｽ繝ｼ繝医∈關ｽ縺｡縺ｦ
    // 縲後た繝ｼ繝域ｸ医∩縺ｪ繧我ｸｦ縺ｹ譖ｿ縺医ｒ荳ｸ縺斐→逵∫払縺吶ｋ縲榊愛螳壹ｂ螟ｱ縺・ゅ◎縺ｮ蝣ｴ蜷医□縺第款縺嶺ｸ九￡縺ｪ縺・・
    private bool KeepsSortedSpanFastPath(SequenceCallInfo order) {
        if(order.Source is InvocationExpressionSyntax inv&&this.TryGetLinqSequenceCall(inv,out _)) return false;
        return SourceCanTryProvideSpan(this.model.GetTypeInfo(order.Source).Type);
    }
    // FragmentBuilder.CreateSourceRoot 縺・span 謠蝉ｾ帛庄閭ｽ縺ｪ Root 繧帝∈縺ｶ蝙九・蜀吶＠縲・
    private static bool SourceCanTryProvideSpan(ITypeSymbol? type) {
        if(type is IArrayTypeSymbol) return true;
        if(type is not INamedTypeSymbol named) return false;
        if(named.TypeKind==TypeKind.Interface) return true;
        return named.OriginalDefinition.ToDisplayString()
            is "System.Collections.Generic.List<T>" or "Seamless.Collections.PersistedList<T>";
    }
    // reduced extension 蠖｢縺ｮ System.Linq.Enumerable 蜻ｼ縺ｳ蜃ｺ縺励↓髯舌▲縺ｦ謚ｼ縺嶺ｸ九￡蟇ｾ雎｡縺ｫ縺吶ｋ縲・
    // 髱咏噪蠖｢ SystemEnumerable.X(...) 縺ｯ intercept 縺輔ｌ縺ｪ縺・盾辣ｧ縺ｪ縺ｮ縺ｧ譖ｸ縺肴鋤縺医↑縺・・
    private bool TryGetLinqSequenceCall(InvocationExpressionSyntax invocation,out SequenceCallInfo call) =>
        CarrierAnalysis.TryGetLinqSequenceCall(invocation,this.model,this.enumerableType,out call);
    // Select邨先棡carrier縺九ｉpredicate繧呈戟縺｡荳翫￡縺ｦsource蛛ｴ縺ｸ謌ｻ縺吶・
    private InvocationExpressionSyntax? TryPushWhereThroughSelect(SequenceCallInfo sc,LambdaExpressionSyntax wl) {
        if(sc.Arguments.Count<1||sc.Arguments[0].Expression is not LambdaExpressionSyntax sl) return null;
        if(!TryLiftPredicate(wl,sl,out var lb,out var refs)) return null;
        var sp = GetLambdaParameterNames(sl);
        if(sp.Count!=1||refs.Count>1) return null;
        if(refs.Count==1&&!refs.Contains(sp[0])) return null;
        return RebuildCall(sc,CreateSequenceCall(sc.Source,"Where",RecreateLambda(sl,lb)),sc.Arguments.Select(static a => a.Expression));
    }
    // SelectMany縺ｯouter/inner縺ｩ縺｡繧峨□縺代・萓晏ｭ倥°縺ｧ謚ｼ縺嶺ｸ九￡蜈医ｒ豎ｺ螳壹・
    private InvocationExpressionSyntax? TryPushWhereThroughSelectMany(SequenceCallInfo smc,LambdaExpressionSyntax wl) {
        if(smc.Arguments.Count<2
            ||smc.Arguments[0].Expression is not LambdaExpressionSyntax cs
            ||smc.Arguments[1].Expression is not LambdaExpressionSyntax rs) return null;
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return null;
        var rp = GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        if(refs.Count==0||refs.SetEquals([rp[0]])) {
            return RebuildCall(smc,CreateSequenceCall(smc.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),smc.Arguments.Select(static a => a.Expression));
        }
        if(!refs.SetEquals([rp[1]])||!TryGetLambdaBody(cs,out var cb)) return null;
        var ra = smc.Arguments.Select(static a => a.Expression).ToArray();
        ra[0]=RecreateLambda(cs,CreateSequenceCall(cb,"Where",RecreateLambdaWithParameter(rp[1],lb)));
        return RebuildCall(smc,smc.Source,ra);
    }
    // Join/LeftJoin縺ｮresult selector繧帝・ｧ｣譫舌＠縺ｦ迚・・萓晏ｭ湾redicate繧痴ource蛛ｴ縺ｸ謌ｻ縺吶・
    // LeftJoin縺ｯinner蛛ｴ縺ｸ謚ｼ縺嶺ｸ九￡繧九→inner縺檎ｩｺ縺ｫ縺ｪ縺｣縺溯｡後′default縺ｨ繝壹い縺ｧ谿九ｊ陦梧焚縺悟､峨ｏ繧九◆繧｛uter蛛ｴ縺縺題ｨｱ蜿ｯ縺吶ｋ縲・
    private InvocationExpressionSyntax? TryPushWhereThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,int rsi) {
        if(c.Arguments.Count<=rsi||c.Arguments[rsi].Expression is not LambdaExpressionSyntax rs) return null;
        var rp = GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        var canPushInner = string.Equals(c.MethodName,"Join",StringComparison.Ordinal);
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return this.TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
        if(refs.Count==0||refs.SetEquals([rp[0]]))
            return RebuildCall(c,CreateSequenceCall(c.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),c.Arguments.Select(static a => a.Expression));
        if(canPushInner&&refs.SetEquals([rp[1]])) {
            var ra = c.Arguments.Select(static a => a.Expression).ToArray();
            ra[0]=CreateSequenceCall(ra[0],"Where",RecreateLambdaWithParameter(rp[1],lb));
            return RebuildCall(c,c.Source,ra);
        }
        return this.TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
    }
    /// <summary>
    /// predicate 繧・AND 騾｣險縺ｸ蛻・ｧ｣縺励∫援蛛ｴ萓晏ｭ倥・騾｣險縺縺代ｒ闡峨∈謚ｼ縺嶺ｸ九￡繧九ゆｸ｡蛛ｴ蜿ら・繧・・ｧ｣譫舌〒縺阪↑縺・｣險縺ｯ
    /// Join 蠕後・ Where 縺ｫ谿九☆(驛ｨ蛻・款縺嶺ｸ九￡)縲・ 蝗槭・譖ｸ謠帙〒谿九ｋ騾｣險縺悟ｿ・★ 1 蛟倶ｻ･荳頑ｸ帙ｋ縺ｮ縺ｧ蜀崎ｨｪ縺ｯ譛蛾剞蝗槭〒豁｢縺ｾ繧九・
    /// 遏ｭ邨｡縺悟｣翫ｌ縺ｦ隧穂ｾ｡蝗樊焚繝ｻ隧穂ｾ｡鬆・′螟峨ｏ繧九◆繧√、nchor 繧貞性繧蠑上〒縺ｯ蛻・牡縺励↑縺・・
    /// </summary>
    private InvocationExpressionSyntax? TryPushConjunctsThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,LambdaExpressionSyntax rs,bool canPushInner) {
        var rp = GetLambdaParameterNames(rs);
        if(!TryGetLambdaBody(wl,out var pb)||!TryGetLambdaBody(rs,out var rb)) return null;
        if(AnchorAnalysis.ContainsAnchor(pb,null)||AnchorAnalysis.ContainsAnchor(rb,null)) return null;
        var parts = new List<ExpressionSyntax>();
        CollectConjuncts(pb,parts);
        if(parts.Count<2) return null;
        var outerParts = new List<ExpressionSyntax>();
        var innerParts = new List<ExpressionSyntax>();
        var restParts = new List<ExpressionSyntax>();
        foreach(var part in parts) {
            if(!TryLiftPredicate(RecreateLambda(wl,part),rs,out var lb,out var refs)) {
                restParts.Add(part);
            } else if(refs.Count==0||refs.SetEquals([rp[0]])) {
                outerParts.Add(lb);
            } else if(canPushInner&&refs.SetEquals([rp[1]])) {
                innerParts.Add(lb);
            } else {
                restParts.Add(part);
            }
        }
        if(outerParts.Count==0&&innerParts.Count==0) return null;
        var ra = c.Arguments.Select(static a => a.Expression).ToArray();
        var source = c.Source;
        if(outerParts.Count>0) {
            source=CreateSequenceCall(source,"Where",RecreateLambdaWithParameter(rp[0],CombineConjuncts(outerParts)));
        }
        if(innerParts.Count>0) {
            ra[0]=CreateSequenceCall(ra[0],"Where",RecreateLambdaWithParameter(rp[1],CombineConjuncts(innerParts)));
        }
        var joined = RebuildCall(c,source,ra);
        if(restParts.Count==0) return joined;
        return CreateSequenceCall(joined,"Where",RecreateLambda(wl,CombineConjuncts(restParts)));
    }
    // Unary邉ｻ/FirstSource邉ｻ: source縺ｮ蜑阪↓Where繧呈諺蜈･縲・
    private static InvocationExpressionSyntax PushWhereBeforeSource(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),c.Arguments.Select(static a => a.Expression));
    }
    // Concat/Union: 蟾ｦ蜿ｳ荳｡source縺ｫWhere繧帝←逕ｨ縲・
    private static InvocationExpressionSyntax TryPushWhereThroughBothSources(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        if(c.Arguments.Count==0) return c.Invocation;
        var ra = c.Arguments.Select(static a => a.Expression).ToArray();
        ra[0]=CreateSequenceCall(ra[0],"Where",wl);
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),ra);
    }
    // carrier から predicate を持ち上げるコア。carrier の逆解析は CarrierAnalysis と共有する。
    // 述語プッシュダウンは carrier メンバを IsLiftableLeaf に限る LeafOnly 方針で使う。
    private bool TryLiftPredicate(LambdaExpressionSyntax pl,LambdaExpressionSyntax sl,out ExpressionSyntax lb,out HashSet<string> refs) =>
        TryLift(pl,sl,this.model,LiftPolicy.LeafOnly,out lb,out refs);
    private static InvocationExpressionSyntax RebuildCall(SequenceCallInfo c,ExpressionSyntax src,IEnumerable<ExpressionSyntax> args) => CreateSequenceCall(src,c.MethodName,args);
}
