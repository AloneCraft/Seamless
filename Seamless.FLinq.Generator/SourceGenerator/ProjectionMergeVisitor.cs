using Microsoft.CodeAnalysis.CSharp;
using static Seamless.FLinq.CarrierAnalysis;
namespace Seamless.FLinq;
// 射影(Select)を直下の selector / result selector へ吸収して段を 1 つ減らす syntax rewriter。
//
//   src.Select(f).Select(g)                           -> src.Select(g∘f)
//   a.Join(b,ko,ki,(x,y)=>carrier).Select(g)          -> a.Join(b,ko,ki,(x,y)=>g[carrier])
//   a.SelectMany(cs,(x,y)=>carrier).Select(g)         -> a.SelectMany(cs,(x,y)=>g[carrier])
//
// クエリ式 lowering は join 句ごとに透過 ValueTuple の carrier を作り、末尾に必ず Select を足すので
// (OptimizeVisitor.LowerJoinClause / LowerSelectOrGroup)、述語プッシュダウンが Where を葉へ退けた後に
// このパスを走らせると透過 carrier の生成が丸ごと消える。これが本パスの最大の利得。
//
// Select は 1 行 -> 1 行なので行数も順序も変えない。LeftJoin / RightJoin / GroupJoin でも
// 述語プッシュダウンで必要だった「inner 側へ押すと行数が変わる」種類のガードは要らない。
// 危険なのは carrier メンバの評価回数・評価順だけで、そこは CarrierAnalysis の
// LiftPolicy.ProjectionMerge(Tier 規則 + Anchor 契約)が受け持つ。
//
// OptimizeVisitor と同じく 1 パスにつき経路ごと 1 書換だけ行う。合成ノードには意味論ゲートを掛けられないため、
// 収束は呼び出し側の再束縛ループ(FLinqIncrementalGenerator.TryMergeProjections)へ委ねる。
// 1 回の書換で構文木中の Select 呼び出しがちょうど 1 個減るので、再訪は有限回で止まる。
internal sealed class ProjectionMergeVisitor:CSharpSyntaxRewriter {
    // result selector が carrier を作る二項演算子。いずれも result selector は 4 番目の引数。
    private static readonly HashSet<string> BinaryCarrierMethods = new(StringComparer.Ordinal) {
        "Join","LeftJoin","RightJoin","GroupJoin",
    };
    private readonly SemanticModel model;
    private readonly INamedTypeSymbol? enumerableType;
    private ProjectionMergeVisitor(SemanticModel model) {
        this.model=model;
        this.enumerableType=model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
    }
    public static TSyntax Merge<TSyntax>(TSyntax node,SemanticModel model) where TSyntax : SyntaxNode {
        return new ProjectionMergeVisitor(model).Visit(node) as TSyntax??node;
    }
    // 元ノードの上で先に書換を試し、成立しなければ子へ降りる(OptimizeVisitor と同じ形)。
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var merged = this.TryMergeInvocation(node);
        if(merged is not null) {
            return merged;
        }
        return base.VisitInvocationExpression(node);
    }
    // Select 直下の sequence call の種類で吸収先の selector を決める。
    private InvocationExpressionSyntax? TryMergeInvocation(InvocationExpressionSyntax invocation) {
        if(!this.TryGetLinqSequenceCall(invocation,out var select)
            ||select.MethodName!="Select"
            ||select.Arguments.Count!=1
            ||select.Arguments[0].Expression is not LambdaExpressionSyntax consumer
            ||GetLambdaParameterNames(consumer).Count!=1
            ||select.Source is not InvocationExpressionSyntax producerInvocation
            ||!this.TryGetLinqSequenceCall(producerInvocation,out var producer)) {
            return null;
        }
        // Select(selector) の selector へ吸収。index 付き Select((x,i)=>..) はパラメータ数で弾かれる。
        if(producer.MethodName=="Select") {
            return this.TryMergeIntoSelector(producer,consumer,0,1);
        }
        // Join/LeftJoin/RightJoin/GroupJoin: (inner,outerKey,innerKey,resultSelector[,comparer])
        if(BinaryCarrierMethods.Contains(producer.MethodName)) {
            return this.TryMergeIntoSelector(producer,consumer,3,2);
        }
        // SelectMany(collectionSelector,resultSelector)。1 引数形は carrier を持たないので対象外。
        if(producer.MethodName=="SelectMany") {
            return this.TryMergeIntoSelector(producer,consumer,1,2);
        }
        return null;
    }
    /// <summary>
    /// producer の <paramref name="selectorIndex"/> 番目のラムダが作る carrier を逆解析して
    /// <paramref name="consumer"/> の本体をその入力空間へ引き戻し、ラムダ本体を差し替えて Select 段を消す。
    /// </summary>
    private InvocationExpressionSyntax? TryMergeIntoSelector(
        SequenceCallInfo producer,
        LambdaExpressionSyntax consumer,
        int selectorIndex,
        int expectedParameterCount) {
        if(producer.Arguments.Count<=selectorIndex
            ||producer.Arguments[selectorIndex].Expression is not LambdaExpressionSyntax selector) {
            return null;
        }
        if(GetLambdaParameterNames(selector).Count!=expectedParameterCount) {
            return null;
        }
        if(!TryLift(consumer,selector,this.model,LiftPolicy.ProjectionMerge,out var mergedBody,out _)) {
            return null;
        }
        var arguments = producer.Arguments.Select(static a => a.Expression).ToArray();
        arguments[selectorIndex]=ReplaceLambdaBody(selector,mergedBody);
        return CreateSequenceCall(producer.Source,producer.MethodName,arguments);
    }
    // reduced extension 形の System.Linq.Enumerable 呼び出しに限って対象にする。
    // 静的形 SystemEnumerable.X(...) は intercept されない参照なので書き換えない。
    private bool TryGetLinqSequenceCall(InvocationExpressionSyntax invocation,out SequenceCallInfo call) =>
        CarrierAnalysis.TryGetLinqSequenceCall(invocation,this.model,this.enumerableType,out call);
}
