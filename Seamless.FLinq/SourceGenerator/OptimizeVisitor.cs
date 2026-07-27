using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq;
// method-chain を翻訳しやすい形に正規化する syntax rewriter。
// 役割は2つ。
//   1. QueryToExtensionMethodRewriter: from a in xs where ... select ...→xs.Where(a=>...).Select(a=>...)
//   2. OptimizeVisitor 本体: Where を carrier の逆解析で葉へ押し下げる
// 解析できないケースでは式を変えずに返す(安全側)。
internal sealed class OptimizeVisitor:CSharpSyntaxRewriter {
    // クエリ式→拡張メソッドチェーン。
    // 元の SemanticModel で新規ノードを直接束縛できないため、interceptor 側では下げた構文木を一時 Compilation に入れてから使う。
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
        // 以前は多段 join / 複数 from / group+継続 / let / this ソースを封印していたが、
        // 各 Lower* は state.Bindings をフラット再射影する作りで原理的に多段連鎖可能であり、
        // 匿名型は record struct registry で名指し可能化されるため、サポート節種別の検査
        //（CanLowerQueryBody）が通れば lowering を試みる。
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
    private static readonly HashSet<string> PushWhereThroughUnaryMethods = new(StringComparer.Ordinal){
        "Distinct","DistinctBy","Order","OrderBy","OrderDescending","OrderByDescending","Reverse",
    };
    private static readonly HashSet<string> OrderMethods = new(StringComparer.Ordinal){
        "Order","OrderBy","OrderDescending","OrderByDescending",
    };
    private static readonly HashSet<string> PushWhereThroughFirstSourceMethods = new(StringComparer.Ordinal){
        "Except","ExceptBy","Intersect","IntersectBy",
    };
    private static readonly HashSet<string> PushWhereThroughBothSourceMethods = new(StringComparer.Ordinal){
        "Concat","Union",
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
    /// Where を carrier の逆解析で葉へ押し下げる。1 パスにつき経路ごと 1 書換だけ行い、
    /// 合成ノードには意味論ゲートを掛けられないため収束は呼び出し側の再束縛ループへ委ねる。
    /// </summary>
    public static TSyntax Optimize<TSyntax>(TSyntax node,SemanticModel model) where TSyntax : SyntaxNode {
        return new OptimizeVisitor(model).Visit(node) as TSyntax??node;
    }
    // 元ノードの上で先に書換を試し、成立しなければ子へ降りる(AlgebraicRewriter と同じ形)。
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var optimized = this.TryOptimizeInvocation(node);
        if(optimized is not null) return optimized;
        return base.VisitInvocationExpression(node);
    }
    // Where直下のsequence callの種類で押し下げ戦略を分岐。
    private InvocationExpressionSyntax? TryOptimizeInvocation(InvocationExpressionSyntax invocation) {
        if(!this.TryGetLinqSequenceCall(invocation,out var wc)
            ||wc.MethodName!="Where"||wc.Arguments.Count!=1
            ||wc.Arguments[0].Expression is not LambdaExpressionSyntax wl
            ||GetLambdaParameterNames(wl).Count!=1
            ||wc.Source is not InvocationExpressionSyntax ui
            ||!this.TryGetLinqSequenceCall(ui,out var uc)) return null;
        if(uc.MethodName=="Select") return TryPushWhereThroughSelect(uc,wl);
        if(uc.MethodName=="SelectMany") return TryPushWhereThroughSelectMany(uc,wl);
        if(uc.MethodName is "Join" or "LeftJoin") return TryPushWhereThroughBinaryCarrier(uc,wl,3);
        if(PushWhereThroughUnaryMethods.Contains(uc.MethodName)||PushWhereThroughFirstSourceMethods.Contains(uc.MethodName)) {
            if(OrderMethods.Contains(uc.MethodName)&&this.KeepsSortedSpanFastPath(uc)) return null;
            return PushWhereBeforeSource(uc,wl);
        }
        if(PushWhereThroughBothSourceMethods.Contains(uc.MethodName)) return TryPushWhereThroughBothSources(uc,wl);
        return null;
    }
    // Order がパイプライン先頭でソースがスパンを出せるとき、生成器はソート済みスパン高速パス
    // (PipelineEnumerable.OrderLoop の CanUseSortedDefaultOrderFastPathCore) を選ぶ。
    // Where を前へ挿すと Order が先頭でなくなり、バッファ+間接インデックスソートへ落ちて
    // 「ソート済みなら並べ替えを丸ごと省略する」判定も失う。その場合だけ押し下げない。
    private bool KeepsSortedSpanFastPath(SequenceCallInfo order) {
        if(order.Source is InvocationExpressionSyntax inv&&this.TryGetLinqSequenceCall(inv,out _)) return false;
        return SourceCanTryProvideSpan(this.model.GetTypeInfo(order.Source).Type);
    }
    // FragmentBuilder.CreateSourceRoot が span 提供可能な Root を選ぶ型の写し。
    private static bool SourceCanTryProvideSpan(ITypeSymbol? type) {
        if(type is IArrayTypeSymbol) return true;
        if(type is not INamedTypeSymbol named) return false;
        if(named.TypeKind==TypeKind.Interface) return true;
        return named.OriginalDefinition.ToDisplayString()
            is "System.Collections.Generic.List<T>" or "Seamless.Collections.PersistedList<T>";
    }
    // reduced extension 形の System.Linq.Enumerable 呼び出しに限って押し下げ対象にする。
    // 静的形 SystemEnumerable.X(...) は intercept されない参照なので書き換えない。
    private bool TryGetLinqSequenceCall(InvocationExpressionSyntax invocation,out SequenceCallInfo call) {
        call=default;
        if(invocation.Expression is not MemberAccessExpressionSyntax ma) return false;
        if(this.model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol m||m.ReducedFrom is null) return false;
        if(this.enumerableType is null||!SymbolEqualityComparer.Default.Equals(m.ContainingType,this.enumerableType)) return false;
        call=new(invocation,m.Name,Strip(ma.Expression),invocation.ArgumentList.Arguments.ToArray());
        return true;
    }
    // Select結果carrierからpredicateを持ち上げてsource側へ戻す。
    private static InvocationExpressionSyntax? TryPushWhereThroughSelect(SequenceCallInfo sc,LambdaExpressionSyntax wl) {
        if(sc.Arguments.Count<1||sc.Arguments[0].Expression is not LambdaExpressionSyntax sl) return null;
        if(!TryLiftPredicate(wl,sl,out var lb,out var refs)) return null;
        var sp = GetLambdaParameterNames(sl);
        if(sp.Count!=1||refs.Count>1) return null;
        if(refs.Count==1&&!refs.Contains(sp[0])) return null;
        return RebuildCall(sc,CreateSequenceCall(sc.Source,"Where",RecreateLambda(sl,lb)),sc.Arguments.Select(static a => a.Expression));
    }
    // SelectManyはouter/innerどちらだけの依存かで押し下げ先を決定。
    private static InvocationExpressionSyntax? TryPushWhereThroughSelectMany(SequenceCallInfo smc,LambdaExpressionSyntax wl) {
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
    // Join/LeftJoinのresult selectorを逆解析して片側依存predicateをsource側へ戻す。
    // LeftJoinはinner側へ押し下げるとinnerが空になった行がdefaultとペアで残り行数が変わるためouter側だけ許可する。
    private static InvocationExpressionSyntax? TryPushWhereThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,int rsi) {
        if(c.Arguments.Count<=rsi||c.Arguments[rsi].Expression is not LambdaExpressionSyntax rs) return null;
        var rp = GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        var canPushInner = string.Equals(c.MethodName,"Join",StringComparison.Ordinal);
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
        if(refs.Count==0||refs.SetEquals([rp[0]]))
            return RebuildCall(c,CreateSequenceCall(c.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),c.Arguments.Select(static a => a.Expression));
        if(canPushInner&&refs.SetEquals([rp[1]])) {
            var ra = c.Arguments.Select(static a => a.Expression).ToArray();
            ra[0]=CreateSequenceCall(ra[0],"Where",RecreateLambdaWithParameter(rp[1],lb));
            return RebuildCall(c,c.Source,ra);
        }
        return TryPushConjunctsThroughBinaryCarrier(c,wl,rs,canPushInner);
    }
    /// <summary>
    /// predicate を AND 連言へ分解し、片側依存の連言だけを葉へ押し下げる。両側参照や逆解析できない連言は
    /// Join 後の Where に残す(部分押し下げ)。1 回の書換で残る連言が必ず 1 個以上減るので再訪は有限回で止まる。
    /// 短絡が壊れて評価回数・評価順が変わるため、Anchor を含む式では分割しない。
    /// </summary>
    private static InvocationExpressionSyntax? TryPushConjunctsThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,LambdaExpressionSyntax rs,bool canPushInner) {
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
    // トップレベル&&を平坦化。括弧は「&&か」の判定にだけ剥がし、連言自体は元ノードのまま残して優先順位を保つ。
    // &(非短絡)は両辺を必ず評価させる意図的な記法なので対象にしない。
    private static void CollectConjuncts(ExpressionSyntax e,List<ExpressionSyntax> into) {
        if(Strip(e) is BinaryExpressionSyntax b&&b.IsKind(SyntaxKind.LogicalAndExpression)) {
            CollectConjuncts(b.Left,into);
            CollectConjuncts(b.Right,into);
        } else {
            into.Add(e);
        }
    }
    private static ExpressionSyntax CombineConjuncts(IReadOnlyList<ExpressionSyntax> parts) {
        var combined = Wrap(parts[0]);
        for(var i = 1;i<parts.Count;i++) {
            combined=SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression,combined,Wrap(parts[i]));
        }
        return combined;
    }
    // Unary系/FirstSource系: sourceの前にWhereを挿入。
    private static InvocationExpressionSyntax PushWhereBeforeSource(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),c.Arguments.Select(static a => a.Expression));
    }
    // Concat/Union: 左右両sourceにWhereを適用。
    private static InvocationExpressionSyntax TryPushWhereThroughBothSources(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        if(c.Arguments.Count==0) return c.Invocation;
        var ra = c.Arguments.Select(static a => a.Expression).ToArray();
        ra[0]=CreateSequenceCall(ra[0],"Where",wl);
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),ra);
    }
    // carrierからpredicateを持ち上げるコア。sentinel名へrenameしてからmember展開。
    private static bool TryLiftPredicate(LambdaExpressionSyntax pl,LambdaExpressionSyntax sl,out ExpressionSyntax lb,out HashSet<string> refs) {
        lb=null!; refs=new HashSet<string>(StringComparer.Ordinal);
        var pp = GetLambdaParameterNames(pl);
        if(pp.Count!=1||!TryGetLambdaBody(pl,out var pb)||!TryGetLambdaBody(sl,out var sb)||!CarrierShape.TryCreate(sb,out var cs)) return false;
        const string sentinel = "__Tsavorite_predicate_root";
        if(new ParameterRenameRewriter(pp[0],sentinel).Visit(pb) is not ExpressionSyntax rpb) return false;
        if(new CarrierAccessRewriter(sentinel,cs).Visit(rpb) is not ExpressionSyntax rb) return false;
        if(ContainsIdentifier(rb,sentinel)) return false;
        refs=CollectParameterReferences(rb,GetLambdaParameterNames(sl));
        lb=rb; return true;
    }
    private static HashSet<string> CollectParameterReferences(ExpressionSyntax e,IReadOnlyList<string> pn) {
        var c = new HashSet<string>(pn,StringComparer.Ordinal);
        var m = new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in e.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            if(c.Contains(id.Identifier.ValueText)) m.Add(id.Identifier.ValueText);
        return m;
    }
    private static bool ContainsIdentifier(ExpressionSyntax e,string name) =>
        e.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => string.Equals(id.Identifier.ValueText,name,StringComparison.Ordinal));
    private static LambdaExpressionSyntax RecreateLambda(LambdaExpressionSyntax t,ExpressionSyntax body) {
        var p = GetLambdaParameterNames(t);
        if(p.Count==1&&t is SimpleLambdaExpressionSyntax)
            return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(p[0])),body);
        return SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(p.Select(static n => SyntaxFactory.Parameter(SyntaxFactory.Identifier(n))))),body);
    }
    private static LambdaExpressionSyntax RecreateLambdaWithParameter(string name,ExpressionSyntax body) =>
        SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)),body);
    private static bool TryGetLambdaBody(LambdaExpressionSyntax l,out ExpressionSyntax body) {
        if(l.Body is ExpressionSyntax eb) { body=eb; return true; }
        body=null!; return false;
    }
    private static IReadOnlyList<string> GetLambdaParameterNames(LambdaExpressionSyntax l) {
        if(l is SimpleLambdaExpressionSyntax s) return [s.Parameter.Identifier.ValueText];
        if(l is ParenthesizedLambdaExpressionSyntax p) return p.ParameterList.Parameters.Select(static x => x.Identifier.ValueText).ToArray();
        return [];
    }
    private static InvocationExpressionSyntax RebuildCall(SequenceCallInfo c,ExpressionSyntax src,IEnumerable<ExpressionSyntax> args) => CreateSequenceCall(src,c.MethodName,args);
    private static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax src,string method,IEnumerable<ExpressionSyntax> args) {
        var ma = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Wrap(src),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(ma,SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Select(static a => SyntaxFactory.Argument(a)))));
    }
    private static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax src,string method,params ExpressionSyntax[] args) => CreateSequenceCall(src,method,(IEnumerable<ExpressionSyntax>)args);
    private static ExpressionSyntax Wrap(ExpressionSyntax e) {
        e=Strip(e);
        return e is IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
            or LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax
            or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            ? e : SyntaxFactory.ParenthesizedExpression(e);
    }
    private static ExpressionSyntax Strip(ExpressionSyntax e) {
        while(e is ParenthesizedExpressionSyntax p) e=p.Expression;
        return e;
    }
    // 匿名型プロパティ名を取得。NameEquals優先、なければ式から推論。
    private static string MemberName(AnonymousObjectMemberDeclaratorSyntax i) {
        if(i.NameEquals is not null) return i.NameEquals.Name.Identifier.ValueText;
        return InferName(i.Expression,out var n) ? n : string.Empty;
    }
    private static bool InferName(ExpressionSyntax e,out string name) {
        e=Strip(e);
        if(e is IdentifierNameSyntax id) { name=id.Identifier.ValueText; return true; }
        if(e is MemberAccessExpressionSyntax m) { name=m.Name.Identifier.ValueText; return true; }
        name=string.Empty; return false;
    }
    private readonly struct SequenceCallInfo(InvocationExpressionSyntax invocation,string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments) {
        public InvocationExpressionSyntax Invocation { get; } = invocation;
        public string MethodName { get; } = methodName;
        public ExpressionSyntax Source { get; } = source;
        public IReadOnlyList<ArgumentSyntax> Arguments { get; } = arguments;
    }
    // selector/result selectorが作るcarrierの形を表す抽象モデル。
    // anonymous type, struct record(__R), tuple等「データ保持だけの形」を対象にする。
    private abstract class CarrierShape {
        public abstract bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax expression);
        public static bool TryCreate(ExpressionSyntax expression,out CarrierShape shape) {
            expression=Strip(expression);
            // 匿名型: new{p,q}
            if(expression is AnonymousObjectCreationExpressionSyntax anon) {
                var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
                foreach(var i in anon.Initializers) {
                    var name = MemberName(i);
                    if(string.IsNullOrWhiteSpace(name)||!TryCreate(i.Expression,out var ms)) { shape=null!; return false; }
                    members[name]=ms;
                }
                shape=new ObjectCarrierShape(members); return true;
            }
            // tuple: (a,b)
            if(expression is TupleExpressionSyntax te)
                return TryCreateTupleShape(te.Arguments.Select(static a => a.Expression).ToArray(),te.Arguments.Select(GetTupleAliases).ToArray(),out shape);
            // ValueTuple/Tupleコンストラクタ
            if(expression is ObjectCreationExpressionSyntax oc2&&oc2.ArgumentList is not null&&TryGetTupleTypeAliases(oc2.Type,oc2.ArgumentList.Arguments.Count,out var ta))
                return TryCreateTupleShape(oc2.ArgumentList.Arguments.Select(static a => a.Expression).ToArray(),ta,out shape);
            // implicit new()
            if(expression is ImplicitObjectCreationExpressionSyntax io) {
                var aliases = System.Linq.Enumerable.Range(0,io.ArgumentList.Arguments.Count).Select(static _ => Array.Empty<string>()).ToArray();
                return TryCreateTupleShape(io.ArgumentList.Arguments.Select(static a => a.Expression).ToArray(),aliases,out shape);
            }
            if(IsLiftableLeaf(expression)) { shape=new LeafCarrierShape(expression); return true; }
            shape=null!; return false;
        }
        private static bool TryCreateTupleShape(ExpressionSyntax[] elems,string[][] aliases,out CarrierShape shape) {
            var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
            for(var i = 0;i<elems.Length;i++) {
                if(!TryCreate(elems[i],out var es)) { shape=null!; return false; }
                members[$"Item{i+1}"]=es;
                foreach(var a in aliases[i]) if(!string.IsNullOrWhiteSpace(a)) members[a]=es;
            }
            shape=new ObjectCarrierShape(members); return true;
        }
        private static string[] GetTupleAliases(ArgumentSyntax a) {
            if(a.NameColon is not null) return [a.NameColon.Name.Identifier.ValueText];
            return InferName(a.Expression,out var n) ? [n] : [];
        }
        private static bool TryGetTupleTypeAliases(TypeSyntax type,int count,out string[][] aliases) {
            aliases=System.Linq.Enumerable.Range(0,count).Select(static _ => Array.Empty<string>()).ToArray();
            type=type switch {
                QualifiedNameSyntax q => q.Right,
                AliasQualifiedNameSyntax aq => aq.Name,
                _ => type,
            };
            if(type is TupleTypeSyntax tt) {
                aliases=tt.Elements.Select(static e => string.IsNullOrWhiteSpace(e.Identifier.ValueText) ? [] : new[] { e.Identifier.ValueText }).ToArray();
                return tt.Elements.Count==count;
            }
            if(type is GenericNameSyntax gn&&gn.Identifier.ValueText is "ValueTuple" or "Tuple")
                return gn.TypeArgumentList.Arguments.Count==count;
            if(type is IdentifierNameSyntax idn&&idn.Identifier.ValueText is "ValueTuple" or "Tuple") return true;
            return false;
        }
        private static bool IsLiftableLeaf(ExpressionSyntax e) {
            e=Strip(e);
            return e switch {
                IdentifierNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax
                    or BaseExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax => true,
                MemberAccessExpressionSyntax m => IsLiftableLeaf(m.Expression),
                ElementAccessExpressionSyntax ea => IsLiftableLeaf(ea.Expression)&&ea.ArgumentList.Arguments.All(static a => IsLiftableLeaf(a.Expression)),
                CastExpressionSyntax c => IsLiftableLeaf(c.Expression),
                _ => false,
            };
        }
    }
    private sealed class LeafCarrierShape(ExpressionSyntax expression):CarrierShape {
        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax e) { e=expression; return path.Count==0; }
    }
    private sealed class ObjectCarrierShape(Dictionary<string,CarrierShape> members):CarrierShape {
        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax e) {
            if(path.Count==0||!members.TryGetValue(path[0],out var next)) { e=null!; return path.Count==0&&false; }
            return next.TryResolve(path.Skip(1).ToArray(),out e);
        }
    }
    // predicate中のcarrier member accessを元expressionに戻すrewriter。
    private sealed class CarrierAccessRewriter(string rootName,CarrierShape shape):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            if(this.TryGetPath(node,out var path)&&shape.TryResolve(path,out var rep)) return Wrap(rep);
            return base.VisitMemberAccessExpression(node);
        }
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,rootName,StringComparison.Ordinal)
                &&!(node.Parent is MemberAccessExpressionSyntax ma&&ma.Expression==node)
                &&shape.TryResolve([],out var rep)) return Wrap(rep);
            return base.VisitIdentifierName(node);
        }
        private bool TryGetPath(ExpressionSyntax e,out IReadOnlyList<string> path) {
            e=Strip(e);
            if(e is IdentifierNameSyntax id&&string.Equals(id.Identifier.ValueText,rootName,StringComparison.Ordinal)) { path= []; return true; }
            if(e is MemberAccessExpressionSyntax ma&&this.TryGetPath(ma.Expression,out var pp)) {
                var parts = pp.ToList(); parts.Add(ma.Name.Identifier.ValueText); path=parts; return true;
            }
            path= []; return false;
        }
    }
    // parameter名のrename rewriter。
    private sealed class ParameterRenameRewriter(string from,string to):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            return string.Equals(node.Identifier.ValueText,from,StringComparison.Ordinal) ? SyntaxFactory.IdentifierName(to) : base.VisitIdentifierName(node);
        }
    }
}
