using Microsoft.CodeAnalysis.CSharp;
using static Seamless.FLinq.Generator.CarrierAnalysis;
namespace Seamless.FLinq.Generator;
// Query lowering and carrier optimization details.
// Query lowering and carrier optimization details.
// Query lowering and carrier optimization details.
// Query lowering and carrier optimization details.
// Query lowering and carrier optimization details.
// Query lowering and carrier optimization details.
internal sealed class OptimizeVisitor:CSharpSyntaxRewriter {
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
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
        // Query lowering and carrier optimization details.
        // Query lowering and carrier optimization details.
        // Query lowering and carrier optimization details.
        // Query lowering and carrier optimization details.
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
                new ParameterSyntax[] {
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(fromClause.Identifier.ValueText,fromClause.Type)
                },
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
                new ParameterSyntax[] {
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(resultName,joinClause.Into is null ? joinClause.Type : null)
                },
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
    // 実行時間は原理的に改善しない。一方で述語が両辺へ複製されて生成コードが倍増する。
    // 差し引きで損になる。
    // Union は残す。ハッシュ集合へ入る要素数が実際に減るので利得がある。
    // 注: 以前ここに「第2ソースは引数位置なのでフラグメント化されず素の System.Linq.Where へ落ちる」
    // と書いていたが、FragmentBuilder が引数位置の列挙ソースも 列挙式フラグメント生成 でほどくように
    // なったため成り立たない。除外の根拠は上の「利得ゼロ」だけである。
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
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    /// </summary>
    public static TSyntax Optimize<TSyntax>(TSyntax node,SemanticModel model) where TSyntax : SyntaxNode {
        return new OptimizeVisitor(model).Visit(node) as TSyntax??node;
    }
    // Query lowering and carrier optimization details.
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var optimized = this.TryOptimizeInvocation(node);
        if(optimized is not null) return optimized;
        return base.VisitInvocationExpression(node);
    }
    // Query lowering and carrier optimization details.
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
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    private bool KeepsSortedSpanFastPath(SequenceCallInfo order) {
        if(order.Source is InvocationExpressionSyntax inv&&this.TryGetLinqSequenceCall(inv,out _)) return false;
        return SourceCanTryProvideSpan(this.model.GetTypeInfo(order.Source).Type);
    }
    // Query lowering and carrier optimization details.
    private static bool SourceCanTryProvideSpan(ITypeSymbol? type) {
        if(type is IArrayTypeSymbol) return true;
        if(type is not INamedTypeSymbol named) return false;
        if(named.TypeKind==TypeKind.Interface) return true;
        return named.OriginalDefinition.ToDisplayString()
            is "System.Collections.Generic.List<T>" or "Seamless.Collections.PersistedList<T>";
    }
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    private bool TryGetLinqSequenceCall(InvocationExpressionSyntax invocation,out SequenceCallInfo call) =>
        CarrierAnalysis.TryGetLinqSequenceCall(invocation,this.model,this.enumerableType,out call);
    // Query lowering and carrier optimization details.
    private InvocationExpressionSyntax? TryPushWhereThroughSelect(SequenceCallInfo sc,LambdaExpressionSyntax wl) {
        if(sc.Arguments.Count<1||sc.Arguments[0].Expression is not LambdaExpressionSyntax sl) return null;
        if(!TryLiftPredicate(wl,sl,out var lb,out var refs)) return null;
        var sp = GetLambdaParameterNames(sl);
        if(sp.Count!=1||refs.Count>1) return null;
        if(refs.Count==1&&!refs.Contains(sp[0])) return null;
        return RebuildCall(sc,CreateSequenceCall(sc.Source,"Where",RecreateLambda(sl,lb)),sc.Arguments.Select(static a => a.Expression));
    }
    // Query lowering and carrier optimization details.
    private InvocationExpressionSyntax? TryPushWhereThroughSelectMany(SequenceCallInfo smc,LambdaExpressionSyntax wl) {
        if(smc.Arguments.Count<2
            ||smc.Arguments[0].Expression is not LambdaExpressionSyntax cs
            ||smc.Arguments[1].Expression is not LambdaExpressionSyntax rs) return null;
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return null;
        var rp = GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        if(refs.Count==0||refs.SetEquals(new[] { rp[0] })) {
            return RebuildCall(smc,CreateSequenceCall(smc.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),smc.Arguments.Select(static a => a.Expression));
        }
        if(!refs.SetEquals(new[] { rp[1] })||!TryGetLambdaBody(cs,out var cb)) return null;
        var ra = smc.Arguments.Select(static a => a.Expression).ToArray();
        ra[0]=RecreateLambda(cs,CreateSequenceCall(cb,"Where",RecreateLambdaWithParameter(rp[1],lb)));
        return RebuildCall(smc,smc.Source,ra);
    }
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    private InvocationExpressionSyntax? TryPushWhereThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,int rsi) {
        if(c.Arguments.Count<=rsi||c.Arguments[rsi].Expression is not LambdaExpressionSyntax rs) return null;
        var rp = GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        var canPushInner = string.Equals(c.MethodName,"Join",StringComparison.Ordinal);
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return this.TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
        if(refs.Count==0||refs.SetEquals(new[] { rp[0] }))
            return RebuildCall(c,CreateSequenceCall(c.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),c.Arguments.Select(static a => a.Expression));
        if(canPushInner&&refs.SetEquals(new[] { rp[1] })) {
            var ra = c.Arguments.Select(static a => a.Expression).ToArray();
            ra[0]=CreateSequenceCall(ra[0],"Where",RecreateLambdaWithParameter(rp[1],lb));
            return RebuildCall(c,c.Source,ra);
        }
        return this.TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
    }
    /// <summary>
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
    // Query lowering and carrier optimization details.
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
            } else if(refs.Count==0||refs.SetEquals(new[] { rp[0] })) {
                outerParts.Add(lb);
            } else if(canPushInner&&refs.SetEquals(new[] { rp[1] })) {
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
    // Query lowering and carrier optimization details.
    private static InvocationExpressionSyntax PushWhereBeforeSource(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),c.Arguments.Select(static a => a.Expression));
    }
    // Query lowering and carrier optimization details.
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
