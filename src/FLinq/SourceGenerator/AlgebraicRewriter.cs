using Microsoft.CodeAnalysis.CSharp;
namespace Tsavorite.SourceGenerator;
// §1「構文結合・アルゴリズムレベルの最適化（置換パターン）」を実装する構文 rewriter。
// interceptor.cs の query lowering 直後（生成パイプライン前段）で intercept 対象メソッド本体へ適用し、
// 書換後に再束縛してから既存の Fragment/Pipeline 生成へ渡す。匿名型の往復は行わない（registry 機構を温存）。
//
// 対象は「拡張メソッド形（reduced extension）の System.Linq.Enumerable 呼び出し」だけ。
// 静的形 SystemEnumerable.X(...) は intercept されない参照なので書き換えない（テストの expected を保つ）。
//
// 実装ルール:
//   - Where(p).Op()                 -> Op(p)        Op∈{Count,LongCount,Any,First,FirstOrDefault,Last,LastOrDefault,Single,SingleOrDefault}
//   - OrderBy(k).First()            -> MinBy(k)     / OrderByDescending(k).First()             -> MaxBy(k)
//     OrderBy(k).FirstOrDefault()   -> MinBy(k)     / OrderByDescending(k).FirstOrDefault()    -> MaxBy(k)
//        空列/同値の意味保存ゲート: First→Min/MaxBy は非null値型のみ、FirstOrDefault→Min/MaxBy はnull既定(参照/Nullable)のみ。
//        Last 系は tie 時に末尾を返すため対象外、comparer 付き/ThenBy 連結も対象外。
//   - Count()>0 / >=1 / !=0         -> Any()        （==0 / <1 / <=0 -> !Any()。0 op Count() の逆順、Count(pred) も同様。LongCount 含む）
//   - Select(x=>x)                  -> （除去）      恒等射影
//   - AsEnumerable()                -> （除去）      冗長
//   - Where(a).Where(b)...          -> 連続 Where を静的コスト＋selectivity で安全に順入替（純粋・全域な述語のみ）
//
// 単発の書換のみ行い、合成（Count()>0 -> Any() の後の Where(p).Any() -> Any(p) 等）は
// interceptor 側の再束縛ループ（TryRewriteScalarAlgebra）が収束させる。
internal sealed class AlgebraicRewriter:CSharpSyntaxRewriter {
    readonly SemanticModel model;
    readonly INamedTypeSymbol? enumerableType;
    static readonly HashSet<string> FusableTerminals=new(StringComparer.Ordinal) {
        "Count","LongCount","Any","First","FirstOrDefault","Last","LastOrDefault","Single","SingleOrDefault",
    };
    AlgebraicRewriter(SemanticModel model) {
        this.model=model;
        this.enumerableType=model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
    }
    public static TSyntax Rewrite<TSyntax>(TSyntax node,SemanticModel model) where TSyntax:SyntaxNode {
        return new AlgebraicRewriter(model).Visit(node) as TSyntax??node;
    }
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        if(this.TryReorderWhereRun(node,out var reordered)) return reordered;
        if(this.TryFuseWherePredicate(node,out var fused)) return fused;
        if(this.TryFuseOrderByFirst(node,out var minmax)) return minmax;
        if(this.TryRemoveIdentityStage(node,out var stripped)) return stripped;
        return base.VisitInvocationExpression(node);
    }
    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node) {
        if(this.TryRewriteCountComparison(node,out var rewritten)) return rewritten;
        return base.VisitBinaryExpression(node);
    }
    // src.Where(pred).Op() -> src.Op(pred)。Op は無引数オーバーロードのみ、pred は単一引数ラムダ。
    bool TryFuseWherePredicate(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var opName,out var opReceiver,out var opArgs)) return false;
        if(!FusableTerminals.Contains(opName)||opArgs.Count!=0) return false;
        if(Strip(opReceiver) is not InvocationExpressionSyntax whereInv) return false;
        if(!this.TryGetWhereRunMember(whereInv,out var pred,out var whereSource)) return false;
        result=CreateCall(whereSource,opName,pred);
        return true;
    }
    // src.OrderBy(k).First() -> src.MinBy(k)（降順は MaxBy）。全ソート O(n log n) を O(n) スキャンへ。
    // First のみ対象（全要素型で安全：空列は First も生成器の MinBy/MaxBy も "Sequence contains no elements" を throw）。
    // FirstOrDefault は対象外（生成器の MinBy/MaxBy は空列で throw するが FirstOrDefault は default を返すため意味が異なる）。
    // tie は OrderBy 安定先頭＝MinBy/MaxBy の先頭（最初の最小/最大）一致で安全。Last 系/comparer 付き/ThenBy 連結は対象外。
    bool TryFuseOrderByFirst(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var fName,out var firstReceiver,out var firstArgs)) return false;
        if(fName!="First"||firstArgs.Count!=0) return false;
        if(Strip(firstReceiver) is not InvocationExpressionSyntax orderInv) return false;
        if(!this.TryGetEnumerableExtensionCall(orderInv,out var oName,out var orderSource,out var oArgs)) return false;
        bool descending;
        if(oName=="OrderBy") {
            descending=false;
        } else if(oName=="OrderByDescending") {
            descending=true;
        } else {
            return false;
        }
        if(oArgs.Count!=1||!TryGetSingleParamLambda(oArgs[0],out _)) return false;   // keySelector のみ・index 無し
        result=CreateCall(orderSource,descending?"MaxBy":"MinBy",oArgs[0].Expression);
        return true;
    }
    // src.Select(x=>x) / src.AsEnumerable() を除去して src（再帰訪問済み）へ。
    // ただし「後続に別の Enumerable 拡張呼び出しがある（=チェーンの最外段でない）」場合のみ除去する。
    // 最外段（例 Source().AsEnumerable().Consume() の AsEnumerable）を消すと LINQ の足場が無くなり
    // intercept 認識が壊れるため。最外段の AsEnumerable/恒等 Select は元々ゼロコストの素通しなので残す。
    bool TryRemoveIdentityStage(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var name,out var receiver,out var args)) return false;
        if(!this.IsReceiverOfEnumerableCall(node)) return false;
        if(name=="AsEnumerable"&&args.Count==0) {
            result=this.Visit(receiver) as SyntaxNode??receiver;
            return true;
        }
        if(name=="Select"&&args.Count==1&&TryGetSingleParamLambda(args[0],out var lambda)&&IsIdentityLambda(lambda)) {
            result=this.Visit(receiver) as SyntaxNode??receiver;
            return true;
        }
        return false;
    }
    // node が後続 Enumerable 拡張呼び出しのレシーバなら true（node.Method(...) の形でさらに LINQ が続く）。
    bool IsReceiverOfEnumerableCall(ExpressionSyntax node) {
        if(node.Parent is not MemberAccessExpressionSyntax ma||ma.Expression!=node) return false;
        if(ma.Parent is not InvocationExpressionSyntax outer) return false;
        return this.TryGetEnumerableExtensionCall(outer,out _,out _,out _);
    }
    // Count()/LongCount() と整数リテラル 0/1 の比較を Any()/!Any() へ。Count(pred) は Any(pred)。
    bool TryRewriteCountComparison(BinaryExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        var op=node.Kind();
        if(op is not(SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)) return false;
        var left=Strip(node.Left);
        var right=Strip(node.Right);
        InvocationExpressionSyntax countInv;
        int literal;
        bool countOnLeft;
        if(this.TryGetCountInvocation(left,out countInv)&&TryGetIntLiteral(right,out literal)) {
            countOnLeft=true;
        } else if(this.TryGetCountInvocation(right,out countInv)&&TryGetIntLiteral(left,out literal)) {
            countOnLeft=false;
        } else {
            return false;
        }
        var normalizedOp=countOnLeft?op:Reverse(op);
        if(!TryMapToAny(normalizedOp,literal,out var negate)) return false;
        this.TryGetEnumerableExtensionCall(countInv,out _,out var countSource,out var countArgs);
        ExpressionSyntax any=countArgs.Count==1
            ?CreateCall(countSource,"Any",countArgs[0].Expression)
            :CreateCall(countSource,"Any");
        if(negate) {
            any=SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,Parenthesize(any));
        }
        result=any;
        return true;
    }
    // 連続 Where を実行順に集め、純粋・全域な場合のみ (passRate,cost,元index) で安定整列して再構築。
    bool TryReorderWhereRun(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetWhereRunMember(node,out _,out var receiver)) return false;
        if(Strip(receiver) is not InvocationExpressionSyntax innerInv||!this.IsWhereCall(innerInv)) return false; // run 長 >=2
        var preds=new List<ExpressionSyntax>();
        ExpressionSyntax current=node;
        ExpressionSyntax baseSource;
        while(true) {
            if(Strip(current) is InvocationExpressionSyntax inv&&this.TryGetWhereRunMember(inv,out var pred,out var recv)) {
                preds.Add(pred);   // outer -> inner の順
                current=recv;
            } else {
                baseSource=current;
                break;
            }
        }
        preds.Reverse();           // 実行順（innermost first）
        foreach(var p in preds) {
            if(p is not LambdaExpressionSyntax l||!this.IsReorderSafe(l)) return false;
        }
        var ordered=preds
            .Select(static (p,i)=>(p,i))
            .OrderBy(static x=>PassRateRank((LambdaExpressionSyntax)x.p))
            .ThenBy(static x=>Cost((LambdaExpressionSyntax)x.p))
            .ThenBy(static x=>x.i)
            .Select(static x=>x.p)
            .ToList();
        if(ordered.SequenceEqual(preds)) return false;   // 既に整列済み（無変化ならループ停止）
        ExpressionSyntax rebuilt=baseSource;
        foreach(var p in ordered) {
            rebuilt=CreateCall(rebuilt,"Where",p);
        }
        result=rebuilt;
        return true;
    }
    // --- reorder safety / heuristics ---
    // 純粋かつ全域: パラメータ参照/リテラル/算術・比較・論理・ビット・条件演算/括弧/プリミティブキャストのみ。
    // メンバ/プロパティアクセスは受信側が非null値型(struct)のときだけ許可（NRE 不能）。
    // メソッド/デリゲート呼び出し・インデクサ・代入・生成・throw 等を含めば不可。
    bool IsReorderSafe(LambdaExpressionSyntax lambda) {
        if(lambda.Body is not ExpressionSyntax body) return false;
        return !AnchorAnalysis.ContainsAnchor(body,this.model)
               && !this.HasNullSensitiveMemberAccess(body);
    }
    bool HasNullSensitiveMemberAccess(ExpressionSyntax expression) {
        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
            var receiver = Strip(memberAccess.Expression);
            if(!this.IsNonNullableValueReceiver(receiver)) {
                return true;
            }
        }
        return false;
    }
    bool IsNonNullableValueReceiver(ExpressionSyntax expr) {
        if(this.model.GetTypeInfo(expr).Type is not { } t||!t.IsValueType) return false;
        return !(t is INamedTypeSymbol n&&n.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T);
    }
    static int Cost(LambdaExpressionSyntax lambda)=>lambda.Body is ExpressionSyntax b?b.DescendantNodesAndSelf().Count():int.MaxValue;
    // 等値 == を含めば選択性高(0)、範囲比較を含めば中(1)、それ以外(否定/その他)は高め(2)。
    static int PassRateRank(LambdaExpressionSyntax lambda) {
        if(lambda.Body is not ExpressionSyntax body) return 2;
        var kinds=body.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Select(static b=>b.Kind()).ToList();
        if(kinds.Contains(SyntaxKind.EqualsExpression)) return 0;
        if(kinds.Any(static k=>k is SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression)) return 1;
        return 2;
    }
    // --- helpers ---
    // reduced extension 形の System.Linq.Enumerable 呼び出しなら true。レシーバ式と引数を返す。
    bool TryGetEnumerableExtensionCall(InvocationExpressionSyntax node,out string name,out ExpressionSyntax receiver,out SeparatedSyntaxList<ArgumentSyntax> args) {
        name=null!;
        receiver=null!;
        args=default;
        if(node.Expression is not MemberAccessExpressionSyntax ma) return false;
        if(this.model.GetSymbolInfo(node).Symbol is not IMethodSymbol m||m.ReducedFrom is null) return false;
        if(this.enumerableType is null||!SymbolEqualityComparer.Default.Equals(m.ContainingType,this.enumerableType)) return false;
        name=m.Name;
        receiver=ma.Expression;
        args=node.ArgumentList.Arguments;
        return true;
    }
    bool TryGetWhereRunMember(InvocationExpressionSyntax inv,out ExpressionSyntax pred,out ExpressionSyntax receiver) {
        pred=null!;
        receiver=null!;
        if(!this.TryGetEnumerableExtensionCall(inv,out var name,out var recv,out var args)) return false;
        if(name!="Where"||args.Count!=1||!TryGetSingleParamLambda(args[0],out _)) return false;
        pred=args[0].Expression;
        receiver=recv;
        return true;
    }
    bool IsWhereCall(InvocationExpressionSyntax inv)=>this.TryGetWhereRunMember(inv,out _,out _);
    bool TryGetCountInvocation(ExpressionSyntax e,out InvocationExpressionSyntax inv) {
        inv=null!;
        if(e is not InvocationExpressionSyntax i) return false;
        if(!this.TryGetEnumerableExtensionCall(i,out var name,out _,out var args)) return false;
        if(name is not("Count" or "LongCount")||args.Count>1) return false;
        inv=i;
        return true;
    }
    static bool TryGetSingleParamLambda(ArgumentSyntax arg,out LambdaExpressionSyntax lambda) {
        lambda=null!;
        if(arg.Expression is SimpleLambdaExpressionSyntax sl) {
            lambda=sl;
            return true;
        }
        if(arg.Expression is ParenthesizedLambdaExpressionSyntax pl&&pl.ParameterList.Parameters.Count==1) {
            lambda=pl;
            return true;
        }
        return false;
    }
    static bool IsIdentityLambda(LambdaExpressionSyntax lambda) {
        string p;
        if(lambda is SimpleLambdaExpressionSyntax sl) {
            p=sl.Parameter.Identifier.ValueText;
        } else if(lambda is ParenthesizedLambdaExpressionSyntax pl&&pl.ParameterList.Parameters.Count==1) {
            p=pl.ParameterList.Parameters[0].Identifier.ValueText;
        } else {
            return false;
        }
        if(lambda.Body is not ExpressionSyntax body) return false;
        return Strip(body) is IdentifierNameSyntax id&&id.Identifier.ValueText==p;
    }
    static bool TryGetIntLiteral(ExpressionSyntax e,out int value) {
        value=0;
        if(Strip(e) is LiteralExpressionSyntax { Token.Value:int v }&&(v==0||v==1)) {
            value=v;
            return true;
        }
        return false;
    }
    static SyntaxKind Reverse(SyntaxKind op)=>op switch {
        SyntaxKind.GreaterThanExpression=>SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanExpression=>SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression=>SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.LessThanOrEqualExpression=>SyntaxKind.GreaterThanOrEqualExpression,
        _=>op,
    };
    // count（左辺）op literal を Any/!Any へ。負論理なら negate=true。
    static bool TryMapToAny(SyntaxKind op,int literal,out bool negate) {
        negate=false;
        switch(op) {
            case SyntaxKind.GreaterThanExpression when literal==0:
            case SyntaxKind.GreaterThanOrEqualExpression when literal==1:
            case SyntaxKind.NotEqualsExpression when literal==0:
                negate=false;
                return true;
            case SyntaxKind.EqualsExpression when literal==0:
            case SyntaxKind.LessThanExpression when literal==1:
            case SyntaxKind.LessThanOrEqualExpression when literal==0:
                negate=true;
                return true;
            default:
                return false;
        }
    }
    static InvocationExpressionSyntax CreateCall(ExpressionSyntax receiver,string method,params ExpressionSyntax[] args) {
        var ma=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Parenthesize(receiver),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(ma,SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Select(static a=>SyntaxFactory.Argument(a)))));
    }
    static ExpressionSyntax Strip(ExpressionSyntax e) {
        while(e is ParenthesizedExpressionSyntax p) {
            e=p.Expression;
        }
        return e;
    }
    static ExpressionSyntax Parenthesize(ExpressionSyntax e) {
        var s=Strip(e);
        return s is IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
            or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            ?s:SyntaxFactory.ParenthesizedExpression(s);
    }
}
