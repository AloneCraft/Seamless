using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.SourceGenerator;
// method-chain を翻訳しやすい形に正規化する syntax rewriter。
// 仕様: 匿名型→struct record(内部最適化用)→最適化→匿名型(最終出力用)
//   1. AnonymousToRecordRewriter: new{p,q}→new __R(p:p,q:q) / new{p}→p(unwrap)
//   2. QueryToExtensionMethodRewriter: from a in xs where ... select ...→xs.Where(a=>...).Select(a=>...)
//   3. ExtensionToStaticRewriter: a.Select(b)→Enumerable.Select(a,b) (global::二重化回避)
//   4. OptimizeVisitor: Where押し下げ等をstruct record上で実行
//   5. RecordToAnonymousRewriter: new __R(p:expr1,q:expr2)→new{p=expr1,q=expr2}
// 解析できないケースでは式を変えずに返す(安全側)。
internal sealed class OptimizeVisitor:CSharpSyntaxRewriter {
    const string R="__R";
    // 匿名型→struct record。new{p,q}→new __R(p:p,q:q)。1要素はunwrap。
    private sealed class AnonymousToRecordRewriter:CSharpSyntaxRewriter {
        public override SyntaxNode? VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node) {
            var init=node.Initializers;
            if(init.Count==1) return Visit(init[0].Expression);
            var args=init.Select(i=>{
                var name=MemberName(i);
                var expr=(ExpressionSyntax)Visit(i.Expression)!;
                var arg=SyntaxFactory.Argument(expr);
                return string.IsNullOrEmpty(name)?arg:arg.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(name)));
            });
            return SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName(R),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args)),null);
        }
    }
    // struct record→匿名型。new __R(p:expr1,q:expr2)→new{p=expr1,q=expr2}。
    // 名前と式が一致する場合(p:p)は暗黙命名new{p}を使う。
    private sealed class RecordToAnonymousRewriter:CSharpSyntaxRewriter {
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) {
            if(node.Type is not IdentifierNameSyntax id||id.Identifier.ValueText!=R) return base.VisitObjectCreationExpression(node);
            var members=node.ArgumentList!.Arguments.Select(a=>{
                var expr=(ExpressionSyntax)Visit(a.Expression)!;
                if(a.NameColon is null) return SyntaxFactory.AnonymousObjectMemberDeclarator(expr);
                var name=a.NameColon.Name.Identifier.ValueText;
                if(expr is IdentifierNameSyntax eid&&eid.Identifier.ValueText==name)
                    return SyntaxFactory.AnonymousObjectMemberDeclarator(expr);
                return SyntaxFactory.AnonymousObjectMemberDeclarator(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(name)),expr);
            });
            return SyntaxFactory.AnonymousObjectCreationExpression(SyntaxFactory.SeparatedList(members));
        }
    }
    // クエリ式→拡張メソッドチェーン。
    // 元の SemanticModel で新規ノードを直接束縛できないため、interceptor 側では下げた構文木を一時 Compilation に入れてから使う。
    private sealed class QueryToExtensionMethodRewriter:CSharpSyntaxRewriter {
        int transparentNumber;
        public override SyntaxNode? VisitQueryExpression(QueryExpressionSyntax node) =>
            CanLowerQuery(node) ? LowerQueryExpression(node) : base.VisitQueryExpression(node);
        static bool CanLowerQuery(QueryExpressionSyntax query) => CanLowerQueryBody(query.Body);
        static bool CanLowerQueryBody(QueryBodySyntax body) {
            if(body.SelectOrGroup is not SelectClauseSyntax) {
                return false;
            }
            if(body.Clauses.Any(static clause => clause is not WhereClauseSyntax and not OrderByClauseSyntax)) {
                return false;
            }
            return body.Continuation is null || CanLowerQueryBody(body.Continuation.Body);
        }
        ExpressionSyntax LowerQueryExpression(QueryExpressionSyntax query) {
            var from = query.FromClause;
            var source = (ExpressionSyntax)Visit(from.Expression)!;
            var state = QueryState.FromRange(from.Identifier.ValueText,from.Type);
            return LowerQueryBody(source,state,query.Body);
        }
        ExpressionSyntax LowerQueryBody(ExpressionSyntax source,QueryState state,QueryBodySyntax body) {
            foreach(var clause in body.Clauses) {
                switch(clause) {
                    case WhereClauseSyntax whereClause:
                        source=CreateSequenceCall(
                            source,
                            "Where",
                            CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)Visit(whereClause.Condition)!)));
                        break;
                    case OrderByClauseSyntax orderByClause:
                        source=LowerOrderByClause(source,state,orderByClause);
                        break;
                    case LetClauseSyntax letClause:
                        source=LowerLetClause(source,ref state,letClause);
                        break;
                    case FromClauseSyntax fromClause:
                        source=LowerFromClause(source,ref state,fromClause);
                        break;
                    case JoinClauseSyntax joinClause:
                        source=LowerJoinClause(source,ref state,joinClause);
                        break;
                    default:
                        return source;
                }
            }

            source=LowerSelectOrGroup(source,state,body.SelectOrGroup);
            if(body.Continuation is not { } continuation) {
                return source;
            }

            return LowerQueryBody(
                source,
                QueryState.FromRange(continuation.Identifier.ValueText,null),
                continuation.Body);
        }
        ExpressionSyntax LowerOrderByClause(ExpressionSyntax source,QueryState state,OrderByClauseSyntax orderByClause) {
            var orderings = orderByClause.Orderings;
            for(var i=0;i<orderings.Count;i++) {
                var ordering = orderings[i];
                var descending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                var method = i==0
                    ? descending ? "OrderByDescending" : "OrderBy"
                    : descending ? "ThenByDescending" : "ThenBy";
                source=CreateSequenceCall(
                    source,
                    method,
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)Visit(ordering.Expression)!)));
            }
            return source;
        }
        ExpressionSyntax LowerLetClause(ExpressionSyntax source,ref QueryState state,LetClauseSyntax letClause) {
            var newBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            newBindings[letClause.Identifier.ValueText]=state.Rewrite((ExpressionSyntax)Visit(letClause.Expression)!);
            var selectorBody = CreateTransparentObject(newBindings);
            source=CreateSequenceCall(source,"Select",CreateLambda(state.ParameterName,state.ParameterType,selectorBody));
            state=QueryState.FromTransparent(FreshTransparentName(),newBindings.Keys);
            return source;
        }
        ExpressionSyntax LowerFromClause(ExpressionSyntax source,ref QueryState state,FromClauseSyntax fromClause) {
            var collectionSelector = CreateLambda(
                state.ParameterName,
                state.ParameterType,
                state.Rewrite((ExpressionSyntax)Visit(fromClause.Expression)!));
            var resultBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            resultBindings[fromClause.Identifier.ValueText]=SyntaxFactory.IdentifierName(fromClause.Identifier);
            var resultSelector = CreateLambda(
                [
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(fromClause.Identifier.ValueText,fromClause.Type)
                ],
                CreateTransparentObject(resultBindings));
            source=CreateSequenceCall(source,"SelectMany",collectionSelector,resultSelector);
            state=QueryState.FromTransparent(FreshTransparentName(),resultBindings.Keys);
            return source;
        }
        ExpressionSyntax LowerJoinClause(ExpressionSyntax source,ref QueryState state,JoinClauseSyntax joinClause) {
            var innerSource = (ExpressionSyntax)Visit(joinClause.InExpression)!;
            var outerKey = CreateLambda(
                state.ParameterName,
                state.ParameterType,
                state.Rewrite((ExpressionSyntax)Visit(joinClause.LeftExpression)!));
            var innerKey = CreateLambda(
                joinClause.Identifier.ValueText,
                joinClause.Type,
                (ExpressionSyntax)Visit(joinClause.RightExpression)!);
            var resultBindings = state.Bindings.ToDictionary(static pair => pair.Key,static pair => pair.Value,StringComparer.Ordinal);
            var method = joinClause.Into is null ? "Join" : "GroupJoin";
            var resultName = joinClause.Into?.Identifier.ValueText ?? joinClause.Identifier.ValueText;
            resultBindings[resultName]=SyntaxFactory.IdentifierName(resultName);
            var resultSelector = CreateLambda(
                [
                    CreateParameter(state.ParameterName,state.ParameterType),
                    CreateParameter(resultName,joinClause.Into is null ? joinClause.Type : null)
                ],
                CreateTransparentObject(resultBindings));
            source=CreateSequenceCall(source,method,innerSource,outerKey,innerKey,resultSelector);
            state=QueryState.FromTransparent(FreshTransparentName(),resultBindings.Keys);
            return source;
        }
        ExpressionSyntax LowerSelectOrGroup(ExpressionSyntax source,QueryState state,SelectOrGroupClauseSyntax selectOrGroup) {
            return selectOrGroup switch {
                SelectClauseSyntax selectClause => CreateSequenceCall(
                    source,
                    "Select",
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)Visit(selectClause.Expression)!))),
                GroupClauseSyntax groupClause => CreateSequenceCall(
                    source,
                    "GroupBy",
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)Visit(groupClause.ByExpression)!)),
                    CreateLambda(state.ParameterName,state.ParameterType,state.Rewrite((ExpressionSyntax)Visit(groupClause.GroupExpression)!))),
                _ => source,
            };
        }
        string FreshTransparentName() => $"__q{transparentNumber++}";
        static AnonymousObjectCreationExpressionSyntax CreateTransparentObject(IReadOnlyDictionary<string,ExpressionSyntax> bindings) {
            return SyntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.SeparatedList(bindings.Select(static pair =>
                    SyntaxFactory.AnonymousObjectMemberDeclarator(
                        SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(pair.Key)),
                        pair.Value))));
        }
        static LambdaExpressionSyntax CreateLambda(string parameterName,TypeSyntax? parameterType,ExpressionSyntax body) {
            if(parameterType is null) {
                return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),body);
            }
            return SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(CreateParameter(parameterName,parameterType))),
                body);
        }
        static LambdaExpressionSyntax CreateLambda(IEnumerable<ParameterSyntax> parameters,ExpressionSyntax body) =>
            SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)),
                body);
        static ParameterSyntax CreateParameter(string name,TypeSyntax? type) =>
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)).WithType(type);
        sealed class QueryState(string parameterName,TypeSyntax? parameterType,Dictionary<string,ExpressionSyntax> bindings) {
            public string ParameterName { get; } = parameterName;
            public TypeSyntax? ParameterType { get; } = parameterType;
            public Dictionary<string,ExpressionSyntax> Bindings { get; } = bindings;
            public static QueryState FromRange(string parameterName,TypeSyntax? parameterType) =>
                new(parameterName,parameterType,new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                    [parameterName]=SyntaxFactory.IdentifierName(parameterName),
                });
            public static QueryState FromTransparent(string parameterName,IEnumerable<string> names) =>
                new(parameterName,null,names.ToDictionary(
                    static name => name,
                    name => (ExpressionSyntax)SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(parameterName),
                        SyntaxFactory.IdentifierName(name)),
                    StringComparer.Ordinal));
            public ExpressionSyntax Rewrite(ExpressionSyntax expression) =>
                new QueryBindingRewriter(Bindings).Visit(expression) as ExpressionSyntax ?? expression;
        }
        sealed class QueryBindingRewriter(IReadOnlyDictionary<string,ExpressionSyntax> bindings):CSharpSyntaxRewriter {
            public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
                if(!bindings.TryGetValue(node.Identifier.ValueText,out var replacement) || IsNonValueIdentifier(node)) {
                    return base.VisitIdentifierName(node);
                }
                return replacement.WithTriviaFrom(node);
            }
            static bool IsNonValueIdentifier(IdentifierNameSyntax node) =>
                node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name==node
                || node.Parent is NameEqualsSyntax
                || node.Parent is NameColonSyntax
                || node.Parent is MemberBindingExpressionSyntax;
        }
    }
    // 拡張メソッド→静的メソッド。a.Select(b)→Enumerable.Select(a,b)。
    private sealed class ExtensionToStaticRewriter:CSharpSyntaxRewriter {
        static readonly HashSet<string> Methods=new(StringComparer.Ordinal){
            "Select","SelectMany","Where","OrderBy","OrderByDescending","ThenBy","ThenByDescending",
            "GroupBy","Join","GroupJoin","Zip","Distinct","DistinctBy","Union","UnionBy",
            "Intersect","IntersectBy","Except","ExceptBy","Concat","Append","Prepend",
            "Skip","SkipWhile","Take","TakeWhile","First","FirstOrDefault","Last","LastOrDefault",
            "Single","SingleOrDefault","ElementAt","ElementAtOrDefault","Any","All","Count","LongCount",
            "Min","MinBy","Max","MaxBy","Sum","Average","Aggregate","AggregateBy","CountBy",
            "Reverse","Cast","OfType","ToArray","ToList","ToDictionary","ToHashSet","ToLookup",
            "DefaultIfEmpty","SequenceEqual","Contains"
        };
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
            if(node.Expression is MemberAccessExpressionSyntax m&&Methods.Contains(m.Name.Identifier.ValueText)) {
                var recv=(ExpressionSyntax)Visit(m.Expression)!;
                var args=node.ArgumentList.Arguments.Select(a=>(ArgumentSyntax)Visit(a)!);
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,SyntaxFactory.IdentifierName("Enumerable"),m.Name),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]{SyntaxFactory.Argument(recv)}.Concat(args))));
            }
            return base.VisitInvocationExpression(node);
        }
    }
    static readonly HashSet<string> PushWhereThroughUnaryMethods=new(StringComparer.Ordinal){
        "Distinct","DistinctBy","Order","OrderBy","OrderDescending","OrderByDescending","Reverse",
    };
    static readonly HashSet<string> PushWhereThroughFirstSourceMethods=new(StringComparer.Ordinal){
        "Except","ExceptBy","Intersect","IntersectBy",
    };
    static readonly HashSet<string> PushWhereThroughBothSourceMethods=new(StringComparer.Ordinal){
        "Concat","Union",
    };
    /// <summary>
    /// 完全最適化エントリポイント。
    /// 匿名型→struct record→拡張→静的変換→Where押し下げ→struct record→匿名型。
    /// </summary>
    public static ExpressionSyntax FullOptimize(ExpressionSyntax expression) {
        var e=new AnonymousToRecordRewriter().Visit(expression) as ExpressionSyntax??expression;
        e=new QueryToExtensionMethodRewriter().Visit(e) as ExpressionSyntax??e;
        e=new ExtensionToStaticRewriter().Visit(e) as ExpressionSyntax??e;
        e=new OptimizeVisitor().Visit(e) as ExpressionSyntax??e;
        return new RecordToAnonymousRewriter().Visit(e) as ExpressionSyntax??e;
    }
    public static TSyntax RewriteQueriesToExtensionMethods<TSyntax>(TSyntax syntax) where TSyntax:SyntaxNode {
        return new QueryToExtensionMethodRewriter().Visit(syntax) as TSyntax ?? syntax;
    }
    /// <summary>匿名型→struct record→匿名型の往復変換（最適化なし）。</summary>
    public static ExpressionSyntax RewriteAnonymousTypes(ExpressionSyntax expression) {
        var e=new AnonymousToRecordRewriter().Visit(expression) as ExpressionSyntax??expression;
        return new RecordToAnonymousRewriter().Visit(e) as ExpressionSyntax??e;
    }
    // Where押し下げのみ。
    public static ExpressionSyntax Optimize(ExpressionSyntax expression) {
        return new OptimizeVisitor().Visit(expression) as ExpressionSyntax??expression;
    }
    // 子ノードを先に処理し変化がある限り再訪する。
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var rewritten=(InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        var optimized=TryOptimizeInvocation(rewritten);
        if(optimized is null||SyntaxFactory.AreEquivalent(optimized,rewritten)) return rewritten;
        return VisitInvocationExpression(optimized);
    }
    // Where直下のsequence callの種類で押し下げ戦略を分岐。
    static InvocationExpressionSyntax? TryOptimizeInvocation(InvocationExpressionSyntax invocation) {
        if(!TryGetSequenceCallInfo(invocation,out var wc)
            ||wc.MethodName!="Where"||wc.Arguments.Count!=1
            ||wc.Arguments[0].Expression is not LambdaExpressionSyntax wl
            ||wc.Source is not InvocationExpressionSyntax ui
            ||!TryGetSequenceCallInfo(ui,out var uc)) return null;
        if(uc.MethodName=="Select") return TryPushWhereThroughSelect(uc,wl);
        if(uc.MethodName=="SelectMany") return TryPushWhereThroughSelectMany(uc,wl);
        if(uc.MethodName is "Join" or "LeftJoin") return TryPushWhereThroughBinaryCarrier(uc,wl,3);
        if(PushWhereThroughUnaryMethods.Contains(uc.MethodName)||PushWhereThroughFirstSourceMethods.Contains(uc.MethodName))
            return PushWhereBeforeSource(uc,wl);
        if(PushWhereThroughBothSourceMethods.Contains(uc.MethodName)) return TryPushWhereThroughBothSources(uc,wl);
        return null;
    }
    // Select結果carrierからpredicateを持ち上げてsource側へ戻す。
    static InvocationExpressionSyntax? TryPushWhereThroughSelect(SequenceCallInfo sc,LambdaExpressionSyntax wl) {
        if(sc.Arguments.Count<1||sc.Arguments[0].Expression is not LambdaExpressionSyntax sl) return null;
        if(!TryLiftPredicate(wl,sl,out var lb,out var refs)) return null;
        var sp=GetLambdaParameterNames(sl);
        if(sp.Count!=1||refs.Count>1) return null;
        if(refs.Count==1&&!refs.Contains(sp[0])) return null;
        return RebuildCall(sc,CreateSequenceCall(sc.Source,"Where",RecreateLambda(sl,lb)),sc.Arguments.Select(static a=>a.Expression));
    }
    // SelectManyはouter/innerどちらだけの依存かで押し下げ先を決定。
    static InvocationExpressionSyntax? TryPushWhereThroughSelectMany(SequenceCallInfo smc,LambdaExpressionSyntax wl) {
        if(smc.Arguments.Count<2
            ||smc.Arguments[0].Expression is not LambdaExpressionSyntax cs
            ||smc.Arguments[1].Expression is not LambdaExpressionSyntax rs) return null;
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return null;
        var rp=GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        if(refs.Count==0||refs.SetEquals([rp[0]])) {
            return RebuildCall(smc,CreateSequenceCall(smc.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),smc.Arguments.Select(static a=>a.Expression));
        }
        if(!refs.SetEquals([rp[1]])||!TryGetLambdaBody(cs,out var cb)) return null;
        var ra=smc.Arguments.Select(static a=>a.Expression).ToArray();
        ra[0]=RecreateLambda(cs,CreateSequenceCall(cb,"Where",RecreateLambdaWithParameter(rp[1],lb)));
        return RebuildCall(smc,smc.Source,ra);
    }
    // Join/LeftJoinのresult selectorを逆解析して片側依存predicateをsource側へ戻す。
    static InvocationExpressionSyntax? TryPushWhereThroughBinaryCarrier(SequenceCallInfo c,LambdaExpressionSyntax wl,int rsi) {
        if(c.Arguments.Count<=rsi||c.Arguments[rsi].Expression is not LambdaExpressionSyntax rs) return null;
        if(!TryLiftPredicate(wl,rs,out var lb,out var refs)) return null;
        var rp=GetLambdaParameterNames(rs);
        if(rp.Count!=2) return null;
        if(refs.Count==0||refs.SetEquals([rp[0]]))
            return RebuildCall(c,CreateSequenceCall(c.Source,"Where",RecreateLambdaWithParameter(rp[0],lb)),c.Arguments.Select(static a=>a.Expression));
        if(!refs.SetEquals([rp[1]])) return null;
        var ra=c.Arguments.Select(static a=>a.Expression).ToArray();
        ra[0]=CreateSequenceCall(ra[0],"Where",RecreateLambdaWithParameter(rp[1],lb));
        return RebuildCall(c,c.Source,ra);
    }
    // Unary系/FirstSource系: sourceの前にWhereを挿入。
    static InvocationExpressionSyntax PushWhereBeforeSource(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),c.Arguments.Select(static a=>a.Expression));
    }
    // Concat/Union: 左右両sourceにWhereを適用。
    static InvocationExpressionSyntax TryPushWhereThroughBothSources(SequenceCallInfo c,LambdaExpressionSyntax wl) {
        if(c.Arguments.Count==0) return c.Invocation;
        var ra=c.Arguments.Select(static a=>a.Expression).ToArray();
        ra[0]=CreateSequenceCall(ra[0],"Where",wl);
        return RebuildCall(c,CreateSequenceCall(c.Source,"Where",wl),ra);
    }
    // carrierからpredicateを持ち上げるコア。sentinel名へrenameしてからmember展開。
    static bool TryLiftPredicate(LambdaExpressionSyntax pl,LambdaExpressionSyntax sl,out ExpressionSyntax lb,out HashSet<string> refs) {
        lb=null!;refs=new HashSet<string>(StringComparer.Ordinal);
        var pp=GetLambdaParameterNames(pl);
        if(pp.Count!=1||!TryGetLambdaBody(pl,out var pb)||!TryGetLambdaBody(sl,out var sb)||!CarrierShape.TryCreate(sb,out var cs)) return false;
        const string sentinel="__seamless_predicate_root";
        if(new ParameterRenameRewriter(pp[0],sentinel).Visit(pb) is not ExpressionSyntax rpb) return false;
        if(new CarrierAccessRewriter(sentinel,cs).Visit(rpb) is not ExpressionSyntax rb) return false;
        if(ContainsIdentifier(rb,sentinel)) return false;
        refs=CollectParameterReferences(rb,GetLambdaParameterNames(sl));
        lb=rb;return true;
    }
    static HashSet<string> CollectParameterReferences(ExpressionSyntax e,IReadOnlyList<string> pn) {
        var c=new HashSet<string>(pn,StringComparer.Ordinal);
        var m=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in e.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            if(c.Contains(id.Identifier.ValueText)) m.Add(id.Identifier.ValueText);
        return m;
    }
    static bool ContainsIdentifier(ExpressionSyntax e,string name)=>
        e.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id=>string.Equals(id.Identifier.ValueText,name,StringComparison.Ordinal));
    static LambdaExpressionSyntax RecreateLambda(LambdaExpressionSyntax t,ExpressionSyntax body) {
        var p=GetLambdaParameterNames(t);
        if(p.Count==1&&t is SimpleLambdaExpressionSyntax)
            return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(p[0])),body);
        return SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(p.Select(static n=>SyntaxFactory.Parameter(SyntaxFactory.Identifier(n))))),body);
    }
    static LambdaExpressionSyntax RecreateLambdaWithParameter(string name,ExpressionSyntax body)=>
        SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)),body);
    static bool TryGetLambdaBody(LambdaExpressionSyntax l,out ExpressionSyntax body) {
        if(l.Body is ExpressionSyntax eb){body=eb;return true;}
        body=null!;return false;
    }
    static IReadOnlyList<string> GetLambdaParameterNames(LambdaExpressionSyntax l) {
        if(l is SimpleLambdaExpressionSyntax s) return[s.Parameter.Identifier.ValueText];
        if(l is ParenthesizedLambdaExpressionSyntax p) return p.ParameterList.Parameters.Select(static x=>x.Identifier.ValueText).ToArray();
        return[];
    }
    static InvocationExpressionSyntax RebuildCall(SequenceCallInfo c,ExpressionSyntax src,IEnumerable<ExpressionSyntax> args)=>CreateSequenceCall(src,c.MethodName,args);
    static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax src,string method,IEnumerable<ExpressionSyntax> args) {
        var ma=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Wrap(src),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(ma,SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Select(static a=>SyntaxFactory.Argument(a)))));
    }
    static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax src,string method,params ExpressionSyntax[] args)=>CreateSequenceCall(src,method,(IEnumerable<ExpressionSyntax>)args);
    static ExpressionSyntax Wrap(ExpressionSyntax e) {
        e=Strip(e);
        return e is IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
            or LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax
            or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            ?e:SyntaxFactory.ParenthesizedExpression(e);
    }
    static ExpressionSyntax Strip(ExpressionSyntax e) {
        while(e is ParenthesizedExpressionSyntax p) e=p.Expression;
        return e;
    }
    static bool TryGetSequenceCallInfo(InvocationExpressionSyntax inv,out SequenceCallInfo call) {
        if(inv.Expression is not MemberAccessExpressionSyntax ma){call=default;return false;}
        var name=ma.Name.Identifier.ValueText;
        if(IsStaticEnumerableReceiver(ma.Expression)) {
            if(inv.ArgumentList.Arguments.Count==0){call=default;return false;}
            call=new(inv,name,Strip(inv.ArgumentList.Arguments[0].Expression),inv.ArgumentList.Arguments.Skip(1).ToArray());
            return true;
        }
        call=new(inv,name,Strip(ma.Expression),inv.ArgumentList.Arguments.ToArray());
        return true;
    }
    static bool IsStaticEnumerableReceiver(ExpressionSyntax e) {
        var t=Strip(e).ToString();
        return string.Equals(t,"Enumerable",StringComparison.Ordinal)
            ||string.Equals(t,"SystemEnumerable",StringComparison.Ordinal)
            ||string.Equals(t,"SeamlessEnumerable",StringComparison.Ordinal)
            ||t.EndsWith(".Enumerable",StringComparison.Ordinal);
    }
    // 匿名型プロパティ名を取得。NameEquals優先、なければ式から推論。
    static string MemberName(AnonymousObjectMemberDeclaratorSyntax i) {
        if(i.NameEquals is not null) return i.NameEquals.Name.Identifier.ValueText;
        return InferName(i.Expression,out var n)?n:string.Empty;
    }
    static bool InferName(ExpressionSyntax e,out string name) {
        e=Strip(e);
        if(e is IdentifierNameSyntax id){name=id.Identifier.ValueText;return true;}
        if(e is MemberAccessExpressionSyntax m){name=m.Name.Identifier.ValueText;return true;}
        name=string.Empty;return false;
    }
    readonly struct SequenceCallInfo(InvocationExpressionSyntax invocation,string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments) {
        public InvocationExpressionSyntax Invocation{get;}=invocation;
        public string MethodName{get;}=methodName;
        public ExpressionSyntax Source{get;}=source;
        public IReadOnlyList<ArgumentSyntax> Arguments{get;}=arguments;
    }
    // selector/result selectorが作るcarrierの形を表す抽象モデル。
    // anonymous type, struct record(__R), tuple等「データ保持だけの形」を対象にする。
    abstract class CarrierShape {
        public abstract bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax expression);
        public static bool TryCreate(ExpressionSyntax expression,out CarrierShape shape) {
            expression=Strip(expression);
            // 匿名型: new{p,q}
            if(expression is AnonymousObjectCreationExpressionSyntax anon) {
                var members=new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
                foreach(var i in anon.Initializers) {
                    var name=MemberName(i);
                    if(string.IsNullOrWhiteSpace(name)||!TryCreate(i.Expression,out var ms)){shape=null!;return false;}
                    members[name]=ms;
                }
                shape=new ObjectCarrierShape(members);return true;
            }
            // struct record: new __R(p:expr,q:expr)
            if(expression is ObjectCreationExpressionSyntax oc
                &&oc.Type is IdentifierNameSyntax rid&&rid.Identifier.ValueText==R
                &&oc.ArgumentList is not null) {
                var members=new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
                foreach(var a in oc.ArgumentList.Arguments) {
                    var name=a.NameColon?.Name.Identifier.ValueText??string.Empty;
                    if(string.IsNullOrEmpty(name)||!TryCreate(a.Expression,out var ms)){shape=null!;return false;}
                    members[name]=ms;
                }
                shape=new ObjectCarrierShape(members);return true;
            }
            // tuple: (a,b)
            if(expression is TupleExpressionSyntax te)
                return TryCreateTupleShape(te.Arguments.Select(static a=>a.Expression).ToArray(),te.Arguments.Select(GetTupleAliases).ToArray(),out shape);
            // ValueTuple/Tupleコンストラクタ
            if(expression is ObjectCreationExpressionSyntax oc2&&oc2.ArgumentList is not null&&TryGetTupleTypeAliases(oc2.Type,oc2.ArgumentList.Arguments.Count,out var ta))
                return TryCreateTupleShape(oc2.ArgumentList.Arguments.Select(static a=>a.Expression).ToArray(),ta,out shape);
            // implicit new()
            if(expression is ImplicitObjectCreationExpressionSyntax io) {
                var aliases=Enumerable.Range(0,io.ArgumentList.Arguments.Count).Select(static _=>Array.Empty<string>()).ToArray();
                return TryCreateTupleShape(io.ArgumentList.Arguments.Select(static a=>a.Expression).ToArray(),aliases,out shape);
            }
            if(IsLiftableLeaf(expression)){shape=new LeafCarrierShape(expression);return true;}
            shape=null!;return false;
        }
        static bool TryCreateTupleShape(ExpressionSyntax[] elems,string[][] aliases,out CarrierShape shape) {
            var members=new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
            for(var i=0;i<elems.Length;i++) {
                if(!TryCreate(elems[i],out var es)){shape=null!;return false;}
                members[$"Item{i+1}"]=es;
                foreach(var a in aliases[i]) if(!string.IsNullOrWhiteSpace(a)) members[a]=es;
            }
            shape=new ObjectCarrierShape(members);return true;
        }
        static string[] GetTupleAliases(ArgumentSyntax a) {
            if(a.NameColon is not null) return[a.NameColon.Name.Identifier.ValueText];
            return InferName(a.Expression,out var n)?[n]:[];
        }
        static bool TryGetTupleTypeAliases(TypeSyntax type,int count,out string[][] aliases) {
            aliases=Enumerable.Range(0,count).Select(static _=>Array.Empty<string>()).ToArray();
            type=type switch {
                QualifiedNameSyntax q=>q.Right,
                AliasQualifiedNameSyntax aq=>aq.Name,
                _=>type,
            };
            if(type is TupleTypeSyntax tt) {
                aliases=tt.Elements.Select(static e=>string.IsNullOrWhiteSpace(e.Identifier.ValueText)?[]:new[]{e.Identifier.ValueText}).ToArray();
                return tt.Elements.Count==count;
            }
            if(type is GenericNameSyntax gn&&gn.Identifier.ValueText is "ValueTuple" or "Tuple")
                return gn.TypeArgumentList.Arguments.Count==count;
            if(type is IdentifierNameSyntax idn&&idn.Identifier.ValueText is "ValueTuple" or "Tuple") return true;
            return false;
        }
        static bool IsLiftableLeaf(ExpressionSyntax e) {
            e=Strip(e);
            return e switch {
                IdentifierNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax
                    or BaseExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax=>true,
                MemberAccessExpressionSyntax m=>IsLiftableLeaf(m.Expression),
                ElementAccessExpressionSyntax ea=>IsLiftableLeaf(ea.Expression)&&ea.ArgumentList.Arguments.All(static a=>IsLiftableLeaf(a.Expression)),
                CastExpressionSyntax c=>IsLiftableLeaf(c.Expression),
                _=>false,
            };
        }
    }
    sealed class LeafCarrierShape(ExpressionSyntax expression):CarrierShape {
        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax e) {e=expression;return path.Count==0;}
    }
    sealed class ObjectCarrierShape(Dictionary<string,CarrierShape> members):CarrierShape {
        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax e) {
            if(path.Count==0||!members.TryGetValue(path[0],out var next)){e=null!;return path.Count==0&&false;}
            return next.TryResolve(path.Skip(1).ToArray(),out e);
        }
    }
    // predicate中のcarrier member accessを元expressionに戻すrewriter。
    sealed class CarrierAccessRewriter(string rootName,CarrierShape shape):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            if(TryGetPath(node,out var path)&&shape.TryResolve(path,out var rep)) return Wrap(rep);
            return base.VisitMemberAccessExpression(node);
        }
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,rootName,StringComparison.Ordinal)
                &&!(node.Parent is MemberAccessExpressionSyntax ma&&ma.Expression==node)
                &&shape.TryResolve([],out var rep)) return Wrap(rep);
            return base.VisitIdentifierName(node);
        }
        bool TryGetPath(ExpressionSyntax e,out IReadOnlyList<string> path) {
            e=Strip(e);
            if(e is IdentifierNameSyntax id&&string.Equals(id.Identifier.ValueText,rootName,StringComparison.Ordinal)){path=[];return true;}
            if(e is MemberAccessExpressionSyntax ma&&TryGetPath(ma.Expression,out var pp)) {
                var parts=pp.ToList();parts.Add(ma.Name.Identifier.ValueText);path=parts;return true;
            }
            path=[];return false;
        }
    }
    // parameter名のrename rewriter。
    sealed class ParameterRenameRewriter(string from,string to):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            return string.Equals(node.Identifier.ValueText,from,StringComparison.Ordinal)?SyntaxFactory.IdentifierName(to):base.VisitIdentifierName(node);
        }
    }
}
