using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
namespace Seamless.FLinq.Generator;
// §1「構文結合・アルゴリズムレベルの最適化（置換パターン）」を実装する構文 rewriter。
// interceptor.cs の query lowering 直後（生成パイプライン前段）で intercept 対象メソッド本体へ適用し、
// 書換後に再束縛してから既存の Fragment/Pipeline 生成へ渡す。匿名型の往復は行わない（registry 機構を温存）。
//
// 対象は「拡張メソッド形（reduced extension）の System.Linq.Enumerable 呼び出し」だけ。
// 静的形 SystemEnumerable.X(...) は intercept されない参照なので書き換えない（テストの expected を保つ）。
//
// 実装ルール:
//   - Where(p).Op()                 -> Op(p)        Op∈{Count,LongCount,Any,First,FirstOrDefault,Single,SingleOrDefault}
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
        "Count","LongCount","Any","First","FirstOrDefault","Single","SingleOrDefault",
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
        if(this.TryRemoveRedundantPrependAppendBeforeTerminal(node,out var simplifiedPrependAppend)) return simplifiedPrependAppend;
        if(this.TryRemoveRedundantDistinctBeforeUnion(node,out var simplifiedUnion)) return simplifiedUnion;
        if(this.TryRemoveRedundantDistinctBeforeMinMax(node,out var simplifiedMinMax)) return simplifiedMinMax;
        if(this.TryRemoveRedundantDistinctBeforeTerminal(node,out var simplifiedDistinct)) return simplifiedDistinct;
        if(this.TryDowngradeUnionToConcatBeforeTerminal(node,out var downgradedUnion)) return downgradedUnion;
        if(this.TryRemoveDoubleReverse(node,out var singleRev)) return singleRev;
        if(this.TryFuseConsecutiveTake(node,out var singleTake)) return singleTake;
        if(this.TryFuseConsecutiveSkip(node,out var singleSkip)) return singleSkip;
        if(this.TrySkipBeforeSelect(node,out var skipBeforeSelect)) return skipBeforeSelect;
        if(this.TryDropSelectBeforeAny(node,out var anyWithoutSelect)) return anyWithoutSelect;
        if(this.TrySplitConcatAny(node,out var splitConcatAny)) return splitConcatAny;
        if(this.TryDropInnerSelectInSelectManyBeforeAny(node,out var selectManyAny)) return selectManyAny;
        if(this.TryFuseConsecutiveTakeLast(node,out var singleTakeLast)) return singleTakeLast;
        if(this.TryFuseConsecutiveSkipLast(node,out var singleSkipLast)) return singleSkipLast;
        if(this.TryFuseTakeSkip(node,out var fusedTakeSkip)) return fusedTakeSkip;
        if(this.TryFuseTakeLastSkipLast(node,out var fusedTakeLastSkipLast)) return fusedTakeLastSkipLast;
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
    // Last系は無match例外とSystem iteratorの走査方向が異なるため融合しない。
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
    // 非空保証のあるソースに対する Prepend(x).Last() / Append(x).First() を簡約。
    // src が非空なら、末尾は src.Last() と一致し、先頭は src.First() と一致する。
    private bool TryRemoveRedundantPrependAppendBeforeTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out var terminalArgs)){
            return false;
        }else{
        }
        if(terminalArgs.Count!=0){
            return false;
        }else{
        }
        if(Strip(terminalReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out _)){
            return false;
        }else{
        }
        if((terminalName is "First" or "FirstOrDefault")&&innerName=="Append"){
            if(this.IsGuaranteedNonEmpty(innerSource)){
                result=CreateCall(innerSource,terminalName);
                return true;
            }else{
            }
        }else if((terminalName is "Last" or "LastOrDefault")&&innerName=="Prepend"){
            if(this.IsGuaranteedNonEmpty(innerSource)){
                result=CreateCall(innerSource,terminalName);
                return true;
            }else{
            }
        }else{
        }
        return false;
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
    // 上流の順序を捨てても結果が変わらない段。値だけで決まる段(比較子なしの Distinct / Union / Intersect / Except と Concat)と、
    // 添字を使わない Where / Select だけ。次の段は順序に依存するので含めない(2026-10-22 の全組み合わせ検証で見つかった)。
    //  - DistinctBy / UnionBy / IntersectBy / ExceptBy: キーが同じ要素のうちどれを残すかが順序で変わる(Sum や後続の Where の結果が変わる)。
    //  - 添字つきの Where / Select: 添字が順序で変わる。
    private static readonly HashSet<string> OrderPreservingTransparentStages = new(StringComparer.Ordinal){
        "Where","Select","Cast","OfType",
        "Distinct",
        "Intersect",
        "Except",
        "Union",
        "Concat",
    };
    private static bool IsOrderIndependentStage(string methodName,ArgumentListSyntax args){
        switch(methodName){
            case "Cast":
            case "OfType":
                return true;
            case "Distinct":
                return args.Arguments.Count==0;
            case "Union":
            case "Intersect":
            case "Except":
            case "Concat":
                return args.Arguments.Count==1;
            case "Where":
            case "Select":
                if(args.Arguments.Count!=1){
                    return false;
                }
                return args.Arguments[0].Expression switch{
                    SimpleLambdaExpressionSyntax=>true,
                    ParenthesizedLambdaExpressionSyntax lambda=>lambda.ParameterList.Parameters.Count==1,
                    _=>false,
                };
            default:
                return false;
        }
    }
    // 順序非依存な終端操作（Contains, Any, All, Count, LongCount）の上流にある冗長な OrderBy/OrderByDescending/ThenBy を除去。
    private bool TryRemoveRedundantOrderBeforeTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out var terminalArgs)){
            return false;
        }else{
        }
        // 件数だけを使う終端(引数なしの Count / LongCount / Any)は、Take / Skip の手前の順序も要らない(どの要素が残るかは件数に影響しない)。
        var countOnlyTerminal=terminalName is "Count" or "LongCount" or "Any"&&terminalArgs.Count==0;
        if(!OrderInsensitiveTerminals.Contains(terminalName)){
            return false;
        }else{
        }
        // 整数/decimalのSumは途中の和で停止する。最終和が同じでもOrder/Reverseを消せない。
        if(terminalName=="Sum"&&Sum.RequiresCheckedAccumulation(
            this.model.GetTypeInfo(node).Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)??string.Empty)){
            return false;
        }
        var cur=Strip(terminalReceiver);
        var strippedAny=false;
        var intermediateStages=new List<(string MethodName,ArgumentListSyntax Args)>();
        while(cur is InvocationExpressionSyntax inv){
            if(!this.TryGetEnumerableExtensionCall(inv,out var methodName,out var methodSource,out _)){
                break;
            }else{
            }
            if(methodName=="OrderBy"||methodName=="OrderByDescending"||methodName=="ThenBy"||methodName=="ThenByDescending"||methodName=="Reverse"){
                cur=Strip(methodSource);
                strippedAny=true;
                continue;
            }else{
            }
            // OrderBy(k).Take(n).Count() -> Take(n).Count()。Take / Skip と終端の間が、件数を変えない段(Select など)だけのときに限る。
            // (間に Where があると、Take / Skip が選ぶ要素で件数が変わる。)System.Linq は並べ替えの鍵を評価せずに件数を答える。
            if(countOnlyTerminal&&methodName is "Take" or "Skip"&&inv.ArgumentList.Arguments.Count==1
                &&intermediateStages.All(static stage=>stage.MethodName is "Select" or "AsEnumerable" or "Cast")){
                intermediateStages.Add((methodName,inv.ArgumentList));
                cur=Strip(methodSource);
                continue;
            }else{
            }
            if(OrderPreservingTransparentStages.Contains(methodName)&&IsOrderIndependentStage(methodName,inv.ArgumentList)){
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
        // 途中で止まる終端(Any / All / Contains)は、止まるまでにラムダを評価するなら(述語つきの終端、Where、Contains の手前の Select など)、
        // 並べ替え・Reverse を消すと System.Linq と別の要素にラムダを評価する(例外の有無が変わる)。要素ごとに System.Linq に合わせるので消さない。
        // 整数/decimal Sumの途中overflowによる停止は上の型判定で順序を保持する。
        if(terminalName is "Any" or "All" or "Contains"
            &&(terminalName=="All"
                ||terminalName=="Any"&&terminalArgs.Count!=0
                ||intermediateStages.Any(stage=>stage.MethodName=="Where"
                    ||stage.MethodName=="Select"&&(terminalName!="Any"||AnchorAnalysis.ContainsAnchor(stage.Args.Arguments[0].Expression,this.model))))){
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
    // src.Distinct().First() -> src.First()
    // src.Distinct().FirstOrDefault() -> src.FirstOrDefault()
    // src.DistinctBy(k).First() -> src.First()
    // src.DistinctBy(k).FirstOrDefault() -> src.FirstOrDefault()
    // src.Distinct().Any() -> src.Any()
    // src.DistinctBy(k).Any() -> src.Any()
    // src.Distinct().Contains(x) -> src.Contains(x)
    private bool TryRemoveRedundantDistinctBeforeTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out var terminalArgs)){
            return false;
        }else{
        }
        if(terminalName is not ("First" or "FirstOrDefault" or "Any" or "Contains")){
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
        if(distinctName=="Distinct"&&distinctArgs.Count==0){
            if(terminalName is "First" or "FirstOrDefault"&&terminalArgs.Count==0){
                result=CreateCall(distinctSource,terminalName);
                return true;
            }else if(terminalName=="Any"){
                result=CreateCallWithArgumentList(distinctSource,"Any",node.ArgumentList);
                return true;
            }else if(terminalName=="Contains"&&terminalArgs.Count==1){
                result=CreateCallWithArgumentList(distinctSource,"Contains",node.ArgumentList);
                return true;
            }else{
            }
        }else if(distinctName=="DistinctBy"){
            if(terminalName is "First" or "FirstOrDefault"&&terminalArgs.Count==0){
                result=CreateCall(distinctSource,terminalName);
                return true;
            }else if(terminalName=="Any"&&terminalArgs.Count==0){
                result=CreateCall(distinctSource,"Any");
                return true;
            }else{
            }
        }else{
        }
        return false;
    }
    // src.Union(second).Any() -> src.Concat(second).Any()
    // src.Union(second).First() -> src.Concat(second).First()
    // src.Union(second).FirstOrDefault() -> src.Concat(second).FirstOrDefault()
    // src.Union(second).Contains(x) -> src.Concat(second).Contains(x)
    // src.UnionBy(second, k).Any() -> src.Concat(second).Any()
    private bool TryDowngradeUnionToConcatBeforeTerminal(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var terminalName,out var terminalReceiver,out var terminalArgs)){
            return false;
        }else{
        }
        if(terminalName is not ("First" or "FirstOrDefault" or "Any" or "Contains")){
            return false;
        }else{
        }
        if(Strip(terminalReceiver) is not InvocationExpressionSyntax unionInv){
            return false;
        }else{
        }
        if(!this.TryGetEnumerableExtensionCall(unionInv,out var unionName,out var unionSource,out var unionArgs)){
            return false;
        }else{
        }
        if(unionName=="Union"&&unionArgs.Count==1){
            if(terminalName is "First" or "FirstOrDefault"&&terminalArgs.Count==0){
                var concatCall=CreateCall(unionSource,"Concat",unionArgs[0].Expression);
                result=CreateCall(concatCall,terminalName);
                return true;
            }else if(terminalName=="Any"&&terminalArgs.Count==0){
                var concatCall=CreateCall(unionSource,"Concat",unionArgs[0].Expression);
                result=CreateCall(concatCall,"Any");
                return true;
            }else if(terminalName=="Contains"&&terminalArgs.Count==1){
                var concatCall=CreateCall(unionSource,"Concat",unionArgs[0].Expression);
                result=CreateCallWithArgumentList(concatCall,"Contains",node.ArgumentList);
                return true;
            }else{
            }
        }else if(unionName=="UnionBy"&&unionArgs.Count==2){
            if(terminalName=="Any"&&terminalArgs.Count==0){
                var concatCall=CreateCall(unionSource,"Concat",unionArgs[0].Expression);
                result=CreateCall(concatCall,"Any");
                return true;
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
    // src.Select(f).Any() -> src.Any()
    // Any は要素があるかだけを見るので、選択子の値は使わない。System.Linq は先頭の1件にだけ選択子を呼ぶ(呼ばないこともある)。
    // 融合した GroupBy の集約などは、全グループの結果選択子を評価していたので、System.Linq より評価の回数が増えていた。
    // 選択子は 1:1 の写像で、.Anchor を含まなければ評価の回数を変えてよいので、除く(Select の位置の例外は、先頭の1件が例外になる形が無くなるだけ)。
    private bool TryDropSelectBeforeAny(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }
        if(outerName!="Any"||outerArgs.Count!=0){
            return false;
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }
        if(innerName!="Select"||innerArgs.Count!=1||!TryGetSingleParamLambda(innerArgs[0],out var lambda)){
            return false;
        }
        if(AnchorAnalysis.ContainsAnchor(lambda,this.model)){
            return false;
        }
        result=CreateCall(innerSource,"Any");
        return true;
    }
    // src.Select(f).Concat(other).Any() -> (src.Select(f).Any() || other.Any())
    // System.Linq は Concat の Any を、前半に要素があれば後半を見ずに、選択子も呼ばずに答える。Select の Any は選択子を落とせる(TryDropSelectBeforeAny)ので、
    // 先に前半だけで Any を調べる形に分ける。後半の式が副作用を持つと評価の時点が変わるので、識別子・メンバーだけ(呼び出し・添字なし)のときに限る。
    private bool TrySplitConcatAny(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }
        if(outerName!="Any"||outerArgs.Count!=0){
            return false;
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax concatInv){
            return false;
        }
        if(!this.TryGetEnumerableExtensionCall(concatInv,out var concatName,out var first,out var concatArgs)){
            return false;
        }
        if(concatName!="Concat"||concatArgs.Count!=1||!IsPlainMemberChain(concatArgs[0].Expression)){
            return false;
        }
        if(Strip(first) is not InvocationExpressionSyntax selectInv
            ||!this.TryGetEnumerableExtensionCall(selectInv,out var selectName,out _,out var selectArgs)
            ||selectName!="Select"||selectArgs.Count!=1||!TryGetSingleParamLambda(selectArgs[0],out var lambda)
            ||AnchorAnalysis.ContainsAnchor(lambda,this.model)){
            return false;
        }
        var left = CreateCall(first,"Any");
        var right = CreateCall(concatArgs[0].Expression,"Any");
        result=SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression,left,right));
        return true;
    }
    // src.SelectMany(p => inner.Select(f)).Any() -> src.SelectMany(p => inner).Any()
    // Any は要素があるかだけを見るので、内側の選択子の値は使わない(System.Linq は内側の先頭の1件にだけ選択子を呼ぶ)。内側を全部評価していた。
    private bool TryDropInnerSelectInSelectManyBeforeAny(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)
            ||outerName!="Any"||outerArgs.Count!=0
            ||Strip(outerReceiver) is not InvocationExpressionSyntax manyInv
            ||!this.TryGetEnumerableExtensionCall(manyInv,out var manyName,out var manySource,out var manyArgs)
            ||manyName!="SelectMany"||manyArgs.Count!=1
            ||manyArgs[0].Expression is not SimpleLambdaExpressionSyntax manyLambda
            ||manyLambda.ExpressionBody is not { } body
            ||Strip(TransparentCalls.Peel(body,this.model)) is not InvocationExpressionSyntax innerSelectInv
            ||!this.TryGetEnumerableExtensionCall(innerSelectInv,out var innerName,out var innerSource,out var innerArgs)
            ||innerName!="Select"||innerArgs.Count!=1||!TryGetSingleParamLambda(innerArgs[0],out var selector)
            ||selector is not SimpleLambdaExpressionSyntax
            ||AnchorAnalysis.ContainsAnchor(selector,this.model)){
            return false;
        }
        // 恒等の包みがあれば残し、包まれた Select だけを内側の列に置き換える。
        var newBody = ReferenceEquals(Strip(body),innerSelectInv) ? innerSource : body.ReplaceNode(innerSelectInv,innerSource.WithTriviaFrom(innerSelectInv));
        var newLambda = manyLambda.WithExpressionBody(newBody);
        result=CreateCall(CreateCall(manySource,"SelectMany",newLambda),"Any");
        return true;
    }
    private static bool IsPlainMemberChain(ExpressionSyntax expression){
        expression=Strip(expression);
        return expression switch {
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax { Expression: var inner } access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => IsPlainMemberChain(inner),
            _ => false,
        };
    }
    // src.Select(f).Skip(n) -> src.Skip(n).Select(f)
    // System.Linq(と ZLinq)は、源によって(配列・List・GroupBy の結果など)、飛ばす要素の選択子を呼ばない。融合したループは飛ばす要素にも選択子を呼ぶので、
    // 評価の回数が増えていた(f が 0 除算などで例外を投げる要素を飛ばす形では、System.Linq にない例外になる)。
    // FLinq は「System.Linq が評価しない要素に、ラムダを評価しない」を守る(System.Linq が評価する要素を評価しないのは構わない)ので、源を問わず先に飛ばす。
    // 選択子は 1:1 の写像なので、先に飛ばしても結果は同じ。添字つき(2引数)の選択子は、添字がずれるので対象外。.Anchor を含む選択子は評価の回数を守る。
    private bool TrySkipBeforeSelect(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }
        if(outerName!="Skip"||outerArgs.Count!=1){
            return false;
        }
        if(Strip(outerReceiver) is not InvocationExpressionSyntax innerInv){
            return false;
        }
        if(!this.TryGetEnumerableExtensionCall(innerInv,out var innerName,out var innerSource,out var innerArgs)){
            return false;
        }
        if(innerName!="Select"||innerArgs.Count!=1||!TryGetSingleParamLambda(innerArgs[0],out var lambda)){
            return false;
        }
        if(AnchorAnalysis.ContainsAnchor(lambda,this.model)){
            return false;
        }
        var skipped=CreateCall(innerSource,"Skip",outerArgs[0].Expression);
        result=CreateCall(skipped,"Select",innerArgs[0].Expression);
        return true;
    }
    // Skip / SkipLast の負の件数は 0 として扱われ、合計は int.MaxValue で頭打ちになる(System.Linq と同じ)。
    // n1 + n2 のまま足すと、Skip(5).Skip(-10) が Skip(-5) になって先頭の5件を飛ばさなくなり、MaxValue 同士は桁あふれで負になる。
    private static ExpressionSyntax NonNegativeSaturatingSum(ExpressionSyntax first,ExpressionSyntax second) =>
        SyntaxFactory.ParseExpression(
            $"(int)global::System.Math.Min((long)global::System.Math.Max({first},0)+global::System.Math.Max({second},0),int.MaxValue)");

    // src.Skip(n1).Skip(n2) -> src.Skip(Max(n1,0) + Max(n2,0))
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
            var sumVal=(int)Math.Min((long)Math.Max(innerVal,0)+Math.Max(outerVal,0),int.MaxValue);
            var sumLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(sumVal));
            result=CreateCall(innerSource,"Skip",sumLit);
            return true;
        }else{
            result=CreateCall(innerSource,"Skip",NonNegativeSaturatingSum(innerArgs[0].Expression,outerArgs[0].Expression));
            return true;
        }
    }
    // src.TakeLast(n1).TakeLast(n2) -> src.TakeLast(Math.Min(n1, n2))
    private bool TryFuseConsecutiveTakeLast(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="TakeLast"||outerArgs.Count!=1){
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
        if(innerName!="TakeLast"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int outerVal&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int innerVal){
            var minVal=Math.Min(outerVal,innerVal);
            var minLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(minVal));
            result=CreateCall(innerSource,"TakeLast",minLit);
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
            result=CreateCall(innerSource,"TakeLast",minInvocation);
            return true;
        }
    }
    // src.SkipLast(n1).SkipLast(n2) -> src.SkipLast(Max(0,n1) + Max(0,n2))(負数と桁あふれの扱いは Skip と同じ)
    private bool TryFuseConsecutiveSkipLast(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="SkipLast"||outerArgs.Count!=1){
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
        if(innerName!="SkipLast"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int outerVal&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int innerVal){
            var sumVal=(int)Math.Min((long)Math.Max(innerVal,0)+Math.Max(outerVal,0),int.MaxValue);
            var sumLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(sumVal));
            result=CreateCall(innerSource,"SkipLast",sumLit);
            return true;
        }else{
            result=CreateCall(innerSource,"SkipLast",NonNegativeSaturatingSum(innerArgs[0].Expression,outerArgs[0].Expression));
            return true;
        }
    }
    // src.Take(m).Skip(n) -> src.Skip(n).Take(Math.Max(0, m - n))
    // m <= n の場合は要素数 0 なので src.Take(0) へ簡約
    private bool TryFuseTakeSkip(InvocationExpressionSyntax node,out SyntaxNode result){
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
        if(innerName!="Take"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int n&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int m){
            if(n<=0){
                result=innerInv;
                return true;
            }else if(m<=0||n>=m){
                var zeroLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(0));
                result=CreateCall(innerSource,"Take",zeroLit);
                return true;
            }else{
                var skipLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(n));
                var takeLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(m-n));
                var skipCall=CreateCall(innerSource,"Skip",skipLit);
                result=CreateCall(skipCall,"Take",takeLit);
                return true;
            }
        }else{
            return false;
        }
    }
    // src.TakeLast(m).SkipLast(n) -> src.SkipLast(n).TakeLast(Math.Max(0, m - n))
    // m <= n の場合は要素数 0 なので src.TakeLast(0) へ簡約
    private bool TryFuseTakeLastSkipLast(InvocationExpressionSyntax node,out SyntaxNode result){
        result=null!;
        if(!this.TryGetEnumerableExtensionCall(node,out var outerName,out var outerReceiver,out var outerArgs)){
            return false;
        }else{
        }
        if(outerName!="SkipLast"||outerArgs.Count!=1){
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
        if(innerName!="TakeLast"||innerArgs.Count!=1){
            return false;
        }else{
        }
        if(outerArgs[0].Expression is LiteralExpressionSyntax outerLit&&outerLit.Token.Value is int n&&
           innerArgs[0].Expression is LiteralExpressionSyntax innerLit&&innerLit.Token.Value is int m){
            if(n<=0){
                result=innerInv;
                return true;
            }else if(m<=0||n>=m){
                var zeroLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(0));
                result=CreateCall(innerSource,"TakeLast",zeroLit);
                return true;
            }else{
                var skipLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(n));
                var takeLit=SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(m-n));
                var skipCall=CreateCall(innerSource,"SkipLast",skipLit);
                result=CreateCall(skipCall,"TakeLast",takeLit);
                return true;
            }
        }else{
            return false;
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
        if((name=="Skip"||name=="SkipLast")&&args.Count==1&&args[0].Expression is LiteralExpressionSyntax lit&&lit.Token.Value is 0){
            result=this.Visit(receiver) as SyntaxNode??receiver;
            return true;
        }else{
        }
        if(name=="Select"&&args.Count==1&&TryGetSingleParamLambda(args[0],out var lambda)&&this.IsIdentityLambda(lambda)) {
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
        // 短絡化は後続の例外やAnchorの評価を省く。既知の有限sourceと全域な述語だけを許す。
        if(AnchorAnalysis.ContainsAnchor(countInv,this.model)
            ||!this.IsSafeCountComparisonSource(countSource)
            ||countArgs.Any(argument=>!this.IsSafeCountComparisonLambda(argument.Expression))){return false;}
        ExpressionSyntax any = countArgs.Count==1
            ? CreateCall(countSource,"Any",countArgs[0].Expression)
            : CreateCall(countSource,"Any");
        if(negate) {
            any=SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,Parenthesize(any));
        }
        result=any;
        return true;
    }
    private bool IsSafeCountComparisonSource(ExpressionSyntax source){
        var type=this.model.GetTypeInfo(source).Type;
        if(type is IArrayTypeSymbol{Rank:1}){return true;}
        if(type is INamedTypeSymbol named&&named.OriginalDefinition.MetadataName=="List`1"
            &&named.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"){return true;}
        if(Strip(source) is not InvocationExpressionSyntax invocation
            ||this.model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method){return false;}
        var definition=method.ReducedFrom??method;
        if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"){return false;}
        if(definition.Name is "Range" or "Repeat" or "Empty"){return true;}
        if(!this.TryGetEnumerableExtensionCall(invocation,out var name,out var inner,out var arguments)
            ||!this.IsSafeCountComparisonSource(inner)){return false;}
        if(name is "AsEnumerable" or "Reverse"){return arguments.Count==0;}
        if(name is "Where" or "Select"){
            return arguments.Count==1&&this.IsSafeCountComparisonLambda(arguments[0].Expression);
        }
        return false;
    }
    private bool IsSafeCountComparisonLambda(ExpressionSyntax expression){
        return Strip(expression) is LambdaExpressionSyntax{Body:ExpressionSyntax body}
            &&!AnchorAnalysis.ContainsAnchor(body,this.model)
            &&IsSafeCountComparisonOperation(this.model.GetOperation(body));
    }
    private static bool IsSafeCountComparisonOperation(IOperation? operation){
        if(operation is null||operation.Type?.SpecialType is not (
            SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_IntPtr or SpecialType.System_UIntPtr
            or SpecialType.System_Single or SpecialType.System_Double)){return false;}
        if(operation.ConstantValue.HasValue){return true;}
        return operation switch{
            IParameterReferenceOperation parameter=>parameter.Parameter.RefKind==RefKind.None,
            IDefaultValueOperation=>true,
            IParenthesizedOperation parenthesized=>IsSafeCountComparisonOperation(parenthesized.Operand),
            IUnaryOperation unary=>!unary.IsChecked&&unary.OperatorMethod is null&&IsSafeCountComparisonOperation(unary.Operand),
            IBinaryOperation binary=>!binary.IsChecked&&binary.OperatorMethod is null
                &&binary.OperatorKind is not (BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder)
                &&IsSafeCountComparisonOperation(binary.LeftOperand)&&IsSafeCountComparisonOperation(binary.RightOperand),
            IConversionOperation conversion=>!conversion.IsChecked&&!conversion.IsTryCast&&conversion.OperatorMethod is null
                &&!conversion.Conversion.IsUserDefined&&IsSafeCountComparisonOperation(conversion.Operand),
            IConditionalOperation conditional=>!conditional.IsRef&&IsSafeCountComparisonOperation(conditional.Condition)
                &&IsSafeCountComparisonOperation(conditional.WhenTrue)&&IsSafeCountComparisonOperation(conditional.WhenFalse),
            _=>false,
        };
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
    // 下流が Where 出力を全件消費するときだけソート前へ移動する。
    // First/Any/Take などの前では、ソート後に必要な分だけ述語を評価する契約を維持する。
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
        if(!OrderFilterReorderSafety.CanMoveFilter(orderInv,whereLambda,this.model)
            ||!DemandAnalysis.IsEntireSequenceConsumed(node,this.model,this.enumerableType)){
            return false;
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
    // 恒等の包み(TransparentCalls。評価回数の検査が本体を包む EvaluationProbe.Hit など)を剥がして判定する。
    private bool IsIdentityLambda(LambdaExpressionSyntax lambda) {
        string p;
        if(lambda is SimpleLambdaExpressionSyntax sl) {
            p=sl.Parameter.Identifier.ValueText;
        } else if(lambda is ParenthesizedLambdaExpressionSyntax pl&&pl.ParameterList.Parameters.Count==1) {
            p=pl.ParameterList.Parameters[0].Identifier.ValueText;
        } else {
            return false;
        }
        if(lambda.Body is not ExpressionSyntax body) return false;
        return Strip(TransparentCalls.Peel(body,this.model)) is IdentifierNameSyntax id&&id.Identifier.ValueText==p;
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
    // Max(0,left) と Max(0,right) の和を、int.MaxValue で頭打ちにして返す式。left を先に評価する。
    private static ExpressionSyntax CreateSaturatingCountSum(ExpressionSyntax left,ExpressionSyntax right) =>
        SyntaxFactory.ParseExpression($"(int)global::System.Math.Min((long)global::System.Math.Max(0,{left.ToString()})+global::System.Math.Max(0,{right.ToString()}),int.MaxValue)");

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
