using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;
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
    private readonly SemanticModel model;
    private readonly INamedTypeSymbol? enumerableType;
    private static readonly HashSet<string> FusableTerminals = new(StringComparer.Ordinal) {
        "Count","LongCount","Any","First","FirstOrDefault","Last","LastOrDefault","Single","SingleOrDefault",
    };
    private AlgebraicRewriter(SemanticModel model) {
        this.model=model;
        this.enumerableType=model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
    }
    public static TSyntax Rewrite<TSyntax>(TSyntax node,SemanticModel model) where TSyntax : SyntaxNode {
        return new AlgebraicRewriter(model).Visit(node) as TSyntax??node;
    }
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        if(this.TryReorderWhereRun(node,out var reordered)) return reordered;
        if(this.TryReorderWhereBeforeOrder(node,out var reorderedWhere)) return reorderedWhere;
        if(this.TryFuseWherePredicate(node,out var fused)) return fused;
        if(this.TryFuseNonEmptyUnionFirst(node,out var unionFirst)) return unionFirst;
        if(this.TryRemoveRedundantDistinctBeforeUnion(node,out var simplifiedUnion)) return simplifiedUnion;
        if(this.TryRemoveRedundantDistinctBeforeMinMax(node,out var simplifiedMinMax)) return simplifiedMinMax;
        if(this.TryRemoveDoubleReverse(node,out var singleRev)) return singleRev;
        if(this.TryFuseConsecutiveTake(node,out var singleTake)) return singleTake;
        if(this.TryFuseConsecutiveSkip(node,out var singleSkip)) return singleSkip;
        if(this.TryFuseReverseTerminal(node,out var revTerminal)) return revTerminal;
        if(this.TryFuseConsecutiveExcept(node,out var fusedExcept)) return fusedExcept;
        if(this.TryFuseConsecutiveUnion(node,out var fusedUnion)) return fusedUnion;
        if(this.TryRemoveRedundantOrderBeforeTerminal(node,out var strippedOrder)) return strippedOrder;
        if(this.TryFuseOrderByFirst(node,out var minmax)) return minmax;
        if(this.TryRemoveIdentityStage(node,out var stripped)) return stripped;
        return base.VisitInvocationExpression(node);
    }
    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node) {
        if(this.TryRewriteCountComparison(node,out var rewritten)) return rewritten;
        return base.VisitBinaryExpression(node);
    }
    // src.Where(pred).Op() -> src.Op(pred)。Op は無引数オーバーロードのみ、pred は単一引数ラムダ。
    private bool TryFuseWherePredicate(InvocationExpressionSyntax node,out SyntaxNode result) {
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
    private bool TryFuseOrderByFirst(InvocationExpressionSyntax node,out SyntaxNode result) {
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
        result=CreateCall(orderSource,descending ? "MaxBy" : "MinBy",oArgs[0].Expression);
        return true;
    }
    // 非空保証のあるソースに対する Union/UnionBy の First/FirstOrDefault を簡約。
    // first.Union(second).First() / first.UnionBy(second,...).First() において、
    // first が非空（Prepend/Append/DefaultIfEmpty 等を含む）であれば、first の先頭要素は必ず重複せず出力され、
    // second は一切参照されないため first.First() と等価。
    private bool TryFuseNonEmptyUnionFirst(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var fName,out var firstReceiver,out var firstArgs)){
            return false;
        }else{
        }
        if(firstArgs.Count!=0){
            return false;
        }else{
        }
        if(fName!="First"&&fName!="FirstOrDefault"){
            return false;
        }else{
        }
        if(Strip(firstReceiver) is not InvocationExpressionSyntax unionInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(unionInv,out var uName,out var unionSource,out _)){
            return false;
        }else{
        }
        if(uName!="Union"&&uName!="UnionBy"){
            return false;
        }else{
        }
        if(!this.IsGuaranteedNonEmpty(unionSource)){
            return false;
        }else{
        }
        result=CreateCall(unionSource,fName);
        return true;
    }
    private bool IsGuaranteedNonEmpty(ExpressionSyntax expr){
        var cur=Strip(expr);
        while(cur is InvocationExpressionSyntax inv){
            if(this.TryGetEnumerableExtensionCall(inv,out var name,out var receiver,out _)){
                if(name=="Prepend"||name=="Append"||name=="DefaultIfEmpty"){
                    return true;
                }else{
                }
                if(name=="OrderBy"||name=="OrderByDescending"||name=="ThenBy"||name=="ThenByDescending"||name=="Select"||name=="Cast"||name=="AsEnumerable"||name=="Reverse"){
                    cur=Strip(receiver);
                    continue;
                }else{
                }
                break;
            }else{
                break;
            }
        }
        return false;
    }
    private static readonly HashSet<string> OrderInsensitiveTerminals = new(StringComparer.Ordinal){
        "Contains","Any","All","Count","LongCount","Sum","Min","Max",
    };
    private static readonly HashSet<string> OrderPreservingTransparentStages = new(StringComparer.Ordinal){
        "Where","Select","Cast","OfType",
        "Distinct","DistinctBy",
        "Intersect","IntersectBy",
        "Except","ExceptBy",
        "Union","UnionBy",
        "Concat",
    };
    // 順序非依存な終端操作（Contains, Any, All, Count, LongCount）の上流にある冗長な OrderBy/OrderByDescending/ThenBy を除去。
    private bool TryRemoveRedundantOrderBeforeTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out _)){
            return false;
        }else{
        }
        if(!OrderInsensitiveTerminals.Contains(terminalName)){
            return false;
        }else{
        }
        var cur=Strip(terminalReceiver);
        var strippedAny=false;
        var intermediateStages=new List<(string MethodName,ArgumentListSyntax Args)>();
        while(cur is InvocationExpressionSyntax inv){
            if(!this.TryGetEnumerableExtensionCall(inv,out var methodName,out var methodSource,out _)){
                break;
            }else{
            }
            if(methodName=="OrderBy"||methodName=="OrderByDescending"||methodName=="ThenBy"||methodName=="ThenByDescending"){
                cur=Strip(methodSource);
                strippedAny=true;
                continue;
            }else{
            }
            if(OrderPreservingTransparentStages.Contains(methodName)){
                intermediateStages.Add((methodName,inv.ArgumentList));
                cur=Strip(methodSource);
                continue;
            }else{
            }
            break;
        }
        if(!strippedAny){
            return false;
        }else{
        }
        var rewritten=cur;
        for(var i=intermediateStages.Count-1;i>=0;i--){
            var stage=intermediateStages[i];
            rewritten=CreateCallWithArgumentList(rewritten,stage.MethodName,stage.Args);
        }
        result=CreateCallWithArgumentList(rewritten,terminalName,node.ArgumentList);
        return true;
    }
    // src.Distinct().Union(second) -> src.Union(second)
    // src.DistinctBy(k).UnionBy(second, k) -> src.UnionBy(second, k)
    // （Union/UnionBy 自体が内部で Set を構築して重複排除するため、第1引数の事前 Distinct は冗長）
    private bool TryRemoveRedundantDistinctBeforeUnion(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var unionName,out var unionReceiver,out var unionArgs)){
            return false;
        }else{
        }
        if(unionName is not ("Union" or "UnionBy")){
            return false;
        }else{
        }
        if(Strip(unionReceiver) is not InvocationExpressionSyntax distinctInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(distinctInv,out var distinctName,out var distinctSource,out var distinctArgs)){
            return false;
        }else{
        }
        if(unionName=="Union"&&distinctName=="Distinct"){
            if(distinctArgs.Count==0&&unionArgs.Count==1){
                result=CreateCallWithArgumentList(distinctSource,unionName,node.ArgumentList);
                return true;
            }else{
            }
        }else if(unionName=="UnionBy"&&distinctName=="DistinctBy"){
            if(distinctArgs.Count==1&&unionArgs.Count==2){
                if(distinctArgs[0].Expression.ToString()==unionArgs[1].Expression.ToString()){
                    result=CreateCallWithArgumentList(distinctSource,unionName,node.ArgumentList);
                    return true;
                }else{
                }
            }else{
            }
        }else{
        }
        return false;
    }
    // src.Distinct().Min() -> src.Min()
    // src.Distinct().Max() -> src.Max()
    // src.DistinctBy(k).MinBy(k) -> src.MinBy(k)
    // src.DistinctBy(k).MaxBy(k) -> src.MaxBy(k)
    private bool TryRemoveRedundantDistinctBeforeMinMax(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out var terminalArgs)){
            return false;
        }else{
        }
        if(terminalName is not ("Min" or "Max" or "MinBy" or "MaxBy")){
            return false;
        }else{
        }
        if(Strip(terminalReceiver) is not InvocationExpressionSyntax distinctInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(distinctInv,out var distinctName,out var distinctSource,out var distinctArgs)){
            return false;
        }else{
        }
        if((terminalName is "Min" or "Max")&&distinctName=="Distinct"){
            if(distinctArgs.Count==0&&terminalArgs.Count==0){
                result=CreateCallWithArgumentList(distinctSource,terminalName,node.ArgumentList);
                return true;
            }else{
            }
        }else if((terminalName is "MinBy" or "MaxBy")&&distinctName=="DistinctBy"){
            if(distinctArgs.Count==1&&terminalArgs.Count==1){
                if(distinctArgs[0].Expression.ToString()==terminalArgs[0].Expression.ToString()){
                    result=CreateCallWithArgumentList(distinctSource,terminalName,node.ArgumentList);
                    return true;
                }else{
                }
            }else{
            }
        }else{
        }
        return false;
    }
    // src.Except(s1).Except(s2) -> src.Except(s1.Concat(s2))
    // src.ExceptBy(s1, k).ExceptBy(s2, k) -> src.ExceptBy(s1.Concat(s2), k)
    private bool TryFuseConsecutiveExcept(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName is not ("Except" or "ExceptBy")){
            return false;
        }else{
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(outerName=="Except"&&innerName=="Except"){
            if(outerArgs.Count==1&&innerArgs.Count==1){
                var concatSecond=SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        Parenthesize(innerArgs[0].Expression),
                        SyntaxFactory.IdentifierName("Concat")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(outerArgs[0])));
                var newArgs=SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(concatSecond)));
                result=CreateCallWithArgumentList(innerSource,"Except",newArgs);
                return true;
            }else{
            }
        }else if(outerName=="ExceptBy"&&innerName=="ExceptBy"){
            if(outerArgs.Count==2&&innerArgs.Count==2){
                if(outerArgs[1].Expression.ToString()==innerArgs[1].Expression.ToString()){
                    var concatSecond=SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            Parenthesize(innerArgs[0].Expression),
                            SyntaxFactory.IdentifierName("Concat")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(outerArgs[0])));
                    var newArgs=SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[]{
                            SyntaxFactory.Argument(concatSecond),
                            outerArgs[1]
                        }));
                    result=CreateCallWithArgumentList(innerSource,"ExceptBy",newArgs);
                    return true;
                }else{
                }
            }else{
            }
        }else{
        }
        return false;
    }
    private bool TryFuseConsecutiveUnion(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName is not ("Union" or "UnionBy")){
            return false;
        }else{
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(outerName=="Union"&&innerName=="Union"){
            if(outerArgs.Count==1&&innerArgs.Count==1){
                var concatSecond=SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        Parenthesize(innerArgs[0].Expression),
                        SyntaxFactory.IdentifierName("Concat")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(outerArgs[0])));
                var newArgs=SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(concatSecond)));
                result=CreateCallWithArgumentList(innerSource,"Union",newArgs);
                return true;
            }else{
            }
        }else if(outerName=="UnionBy"&&innerName=="UnionBy"){
            if(outerArgs.Count==2&&innerArgs.Count==2){
                if(outerArgs[1].Expression.ToString()==innerArgs[1].Expression.ToString()){
                    var concatSecond=SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            Parenthesize(innerArgs[0].Expression),
                            SyntaxFactory.IdentifierName("Concat")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(outerArgs[0])));
                    var newArgs=SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[]{
                            SyntaxFactory.Argument(concatSecond),
                            outerArgs[1]
                        }));
                    result=CreateCallWithArgumentList(innerSource,"UnionBy",newArgs);
                    return true;
                }else{
                }
            }else{
            }
        }else{
        }
        return false;
    }
    // src.Reverse().Reverse() -> src。二重反転を除去して恒等展開。
    private bool TryRemoveDoubleReverse(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="Reverse"||outerArgs.Count!=0){
            return false;
        }else{
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(innerName!="Reverse"||innerArgs.Count!=0){
            return false;
        }else{
        }
        result=innerSource;
        return true;
    }
    // src.Take(n1).Take(n2) -> src.Take(min(n1, n2))
    private bool TryFuseConsecutiveTake(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="Take"||outerArgs.Count!=1){
            return false;
        }else{
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(innerName!="Take"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int outerVal&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int innerVal){
            var minVal=Math.Min(outerVal,innerVal);
            var minLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(minVal));
            result=CreateCall(innerSource,"Take",minLit);
            return true;
        }else{
            var minInvocation=SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseTypeName("global::System.Math"),
                    SyntaxFactory.IdentifierName("Min")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]{
                    outerArgs[0],
                    innerArgs[0]
                })));
            result=CreateCall(innerSource,"Take",minInvocation);
            return true;
        }
    }
    // src.Skip(n1).Skip(n2) -> src.Skip(n1 + n2)
    private bool TryFuseConsecutiveSkip(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="Skip"||outerArgs.Count!=1){
            return false;
        }else{
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(innerName!="Skip"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int outerVal&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int innerVal){
            var sumVal=innerVal+outerVal;
            var sumLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(sumVal));
            result=CreateCall(innerSource,"Skip",sumLit);
            return true;
        }else{
            var addExpr=SyntaxFactory.BinaryExpression(
                SyntaxKind.AddExpression,
                Parenthesize(innerArgs[0].Expression),
                Parenthesize(outerArgs[0].Expression));
            result=CreateCall(innerSource,"Skip",addExpr);
            return true;
        }
    }
    // src.Reverse().First() -> src.Last()
    // src.Reverse().FirstOrDefault() -> src.LastOrDefault()
    // src.Reverse().Last() -> src.First()
    // src.Reverse().LastOrDefault() -> src.FirstOrDefault()
    private bool TryFuseReverseTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerArgs.Count!=0){
            return false;
        }else{
        }
        string targetTerminal;
        if(outerName=="First"){
            targetTerminal="Last";
        }else if(outerName=="FirstOrDefault"){
            targetTerminal="LastOrDefault";
        }else if(outerName=="Last"){
            targetTerminal="First";
        }else if(outerName=="LastOrDefault"){
            targetTerminal="FirstOrDefault";
        }else{
            return false;
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }else{
        }
        if(innerName!="Reverse"||innerArgs.Count!=0){
            return false;
        }else{
        }
        result=CreateCall(innerSource,targetTerminal);
        return true;
    }
    private static InvocationExpressionSyntax CreateCallWithArgumentList(ExpressionSyntax receiver,string method,ArgumentListSyntax argumentList){
        var ma=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Parenthesize(receiver),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(ma,argumentList);
    }
    // src.Select(x=>x) / src.AsEnumerable() を除去して src（再帰訪問済み）へ。
    // ただし「後続に別の Enumerable 拡張呼び出しがある（=チェーンの最外段でない）」場合のみ除去する。
    // 最外段（例 Source().AsEnumerable().Consume() の AsEnumerable）を消すと LINQ の足場が無くなり
    // intercept 認識が壊れるため。最外段の AsEnumerable/恒等 Select は元々ゼロコストの素通しなので残す。
    private bool TryRemoveIdentityStage(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var name,out var receiver,out var args)) return false;
        if(!this.IsReceiverOfEnumerableCall(node)) return false;
        if(name=="AsEnumerable"&&args.Count==0) {
            var receiverType = this.model.GetTypeInfo(receiver).Type??this.model.GetTypeInfo(receiver).ConvertedType;
            if(UnorderedSourceSafety.IsUnorderedSourceType(receiverType)) {
                return false;
            }
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
    private bool IsReceiverOfEnumerableCall(ExpressionSyntax node) {
        if(node.Parent is not MemberAccessExpressionSyntax ma||ma.Expression!=node) return false;
        if(ma.Parent is not InvocationExpressionSyntax outer) return false;
        return this.TryGetEnumerableExtensionCall(outer,out _,out _,out _);
    }
    // Count()/LongCount() と整数リテラル 0/1 の比較を Any()/!Any() へ。Count(pred) は Any(pred)。
    private bool TryRewriteCountComparison(BinaryExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        var op = node.Kind();
        if(op is not (SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)) return false;
        var left = Strip(node.Left);
        var right = Strip(node.Right);
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
        var normalizedOp = countOnLeft ? op : Reverse(op);
        if(!TryMapToAny(normalizedOp,literal,out var negate)) return false;
        this.TryGetEnumerableExtensionCall(countInv,out _,out var countSource,out var countArgs);
        ExpressionSyntax any = countArgs.Count==1
            ? CreateCall(countSource,"Any",countArgs[0].Expression)
            : CreateCall(countSource,"Any");
        if(negate) {
            any=SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,Parenthesize(any));
        }
        result=any;
        return true;
    }
    // 連続 Where を実行順に集め、純粋・全域な場合のみ (passRate,cost,元index) で安定整列して再構築。
    private bool TryReorderWhereRun(InvocationExpressionSyntax node,out SyntaxNode result) {
        result=null!;
        if(!this.TryGetWhereRunMember(node,out _,out var receiver)) return false;
        if(Strip(receiver) is not InvocationExpressionSyntax innerInv||!this.IsWhereCall(innerInv)) return false; // run 長 >=2
        var preds = new List<ExpressionSyntax>();
        ExpressionSyntax current = node;
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
        var ordered = preds
            .Select(static (p,i) => (p,i))
            .OrderBy(static x => PassRateRank((LambdaExpressionSyntax)x.p))
            .ThenBy(static x => Cost((LambdaExpressionSyntax)x.p))
            .ThenBy(static x => x.i)
            .Select(static x => x.p)
            .ToList();
        if(ordered.SequenceEqual(preds)) return false;   // 既に整列済み（無変化ならループ停止）
        ExpressionSyntax rebuilt = baseSource;
        foreach(var p in ordered) {
            rebuilt=CreateCall(rebuilt,"Where",p);
        }
        result=rebuilt;
        return true;
    }
    // src.OrderBy(k).Where(pred) -> src.Where(pred).OrderBy(k)
    // src.OrderByDescending(k).Where(pred) -> src.Where(pred).OrderByDescending(k)
    // 要素フィルタWhereをソート前に移動することで、ソート対象要素数を削減し計算量・メモリを大幅低減。
    private bool TryReorderWhereBeforeOrder(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var whereName,out var whereReceiver,out var whereArgs)){
            return false;
        }else{
        }
        if(whereName!="Where"||whereArgs.Count!=1||!TryGetSingleParamLambda(whereArgs[0],out var whereLambda)){
            return false;
        }else{
        }
        if(!this.IsReorderSafe(whereLambda)){
            return false;
        }else{
        }
        if(Strip(whereReceiver) is not InvocationExpressionSyntax orderInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(orderInv,out var orderName,out var orderSource,out var orderArgs)){
            return false;
        }else{
        }
        if(orderName is not ("OrderBy" or "OrderByDescending")){
            return false;
        }else{
        }
        if(orderArgs.Count!=1){
            return false;
        }else{
        }
        var newWhere=CreateCallWithArgumentList(orderSource,"Where",node.ArgumentList);
        result=CreateCallWithArgumentList(newWhere,orderName,orderInv.ArgumentList);
        return true;
    }
    // --- reorder safety / heuristics ---
    // 純粋かつ全域: パラメータ参照/リテラル/算術・比較・論理・ビット・条件演算/括弧/プリミティブキャストのみ。
    // メンバ/プロパティアクセスは受信側が非null値型(struct)のときだけ許可（NRE 不能）。
    // メソッド/デリゲート呼び出し・インデクサ・代入・生成・throw 等を含めば不可。
    private bool IsReorderSafe(LambdaExpressionSyntax lambda) {
        if(lambda.Body is not ExpressionSyntax body) return false;
        return !AnchorAnalysis.ContainsAnchor(body,this.model)
               &&!this.HasNullSensitiveMemberAccess(body);
    }
    private bool HasNullSensitiveMemberAccess(ExpressionSyntax expression) {
        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
            var receiver = Strip(memberAccess.Expression);
            if(!this.IsNonNullableValueReceiver(receiver)) {
                return true;
            }
        }
        return false;
    }
    private bool IsNonNullableValueReceiver(ExpressionSyntax expr) {
        if(this.model.GetTypeInfo(expr).Type is not { } t||!t.IsValueType) return false;
        return !(t is INamedTypeSymbol n&&n.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T);
    }
    private static int Cost(LambdaExpressionSyntax lambda) => lambda.Body is ExpressionSyntax b ? b.DescendantNodesAndSelf().Count() : int.MaxValue;
    // 等値 == を含めば選択性高(0)、範囲比較を含めば中(1)、それ以外(否定/その他)は高め(2)。
    private static int PassRateRank(LambdaExpressionSyntax lambda) {
        if(lambda.Body is not ExpressionSyntax body) return 2;
        var kinds = body.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Select(static b => b.Kind()).ToList();
        if(kinds.Contains(SyntaxKind.EqualsExpression)) return 0;
        if(kinds.Any(static k => k is SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression)) return 1;
        return 2;
    }
    // --- helpers ---
    // reduced extension 形の System.Linq.Enumerable 呼び出しなら true。レシーバ式と引数を返す。
    private bool TryGetEnumerableExtensionCall(InvocationExpressionSyntax node,out string name,out ExpressionSyntax receiver,out SeparatedSyntaxList<ArgumentSyntax> args) {
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
    private bool TryGetWhereRunMember(InvocationExpressionSyntax inv,out ExpressionSyntax pred,out ExpressionSyntax receiver) {
        pred=null!;
        receiver=null!;
        if(!this.TryGetEnumerableExtensionCall(inv,out var name,out var recv,out var args)) return false;
        if(name!="Where"||args.Count!=1||!TryGetSingleParamLambda(args[0],out _)) return false;
        pred=args[0].Expression;
        receiver=recv;
        return true;
    }
    private bool IsWhereCall(InvocationExpressionSyntax inv) => this.TryGetWhereRunMember(inv,out _,out _);
    private bool TryGetCountInvocation(ExpressionSyntax e,out InvocationExpressionSyntax inv) {
        inv=null!;
        if(e is not InvocationExpressionSyntax i) return false;
        if(!this.TryGetEnumerableExtensionCall(i,out var name,out _,out var args)) return false;
        if(name is not ("Count" or "LongCount")||args.Count>1) return false;
        inv=i;
        return true;
    }
    private static bool TryGetSingleParamLambda(ArgumentSyntax arg,out LambdaExpressionSyntax lambda) {
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
    private static bool IsIdentityLambda(LambdaExpressionSyntax lambda) {
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
    private static bool TryGetIntLiteral(ExpressionSyntax e,out int value) {
        value=0;
        if(Strip(e) is LiteralExpressionSyntax { Token.Value: int v }&&(v==0||v==1)) {
            value=v;
            return true;
        }
        return false;
    }
    private static SyntaxKind Reverse(SyntaxKind op) => op switch {
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        _ => op,
    };
    // count（左辺）op literal を Any/!Any へ。負論理なら negate=true。
    private static bool TryMapToAny(SyntaxKind op,int literal,out bool negate) {
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
    private static InvocationExpressionSyntax CreateCall(ExpressionSyntax receiver,string method,params ExpressionSyntax[] args) {
        var ma = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Parenthesize(receiver),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(ma,SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Select(static a => SyntaxFactory.Argument(a)))));
    }
    private static ExpressionSyntax Strip(ExpressionSyntax e) {
        while(e is ParenthesizedExpressionSyntax p) {
            e=p.Expression;
        }
        return e;
    }
    private static ExpressionSyntax Parenthesize(ExpressionSyntax e) {
        var s = Strip(e);
        return s is IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
            or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            ? s : SyntaxFactory.ParenthesizedExpression(s);
    }
}
