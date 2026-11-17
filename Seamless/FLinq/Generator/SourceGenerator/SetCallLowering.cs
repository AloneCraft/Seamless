using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.FLinq.Generator;

/// <summary>
/// Seamless.Linq.Set の呼び出し(ISet を受けて ISet を返す Where・Select など)を、System.Linq の同じ演算へ下げる。
/// ビュー・関数の本体はこの形で書く(Document/refactoring-designs/20261112-DBアクセスモデル.md の手順3)。
/// <list type="bullet">
/// <item><description>集合の連鎖の先頭に IEnumerable<T> への変換を挟み、以降を System.Linq.Enumerable の演算へ結び付ける。</description></item>
/// <item><description>Set の Select・SelectMany・Join・LeftJoin・RightJoin・GroupJoin と、結果を作る関数を渡す GroupBy は結果の重複を除くので、下げた後に Distinct() を足す。Where・Union・Intersect・Except・Distinct は System.Linq も同じ結果になる。</description></item>
/// <item><description>下げるのは、結果を LINQ の演算が受け取る連鎖だけ。ISet のまま使う箇所(Count プロパティ・ISet の変数へ代入など)は元のまま残す。</description></item>
/// </list>
/// 下げた結果に型の誤りが出たら、何も下げない(呼び出し先の Set のまま動く)。
/// </summary>
internal static class SetCallLowering{
    private const string SetTypeName="Seamless.Linq.Set";
    private const string SystemEnumerableTypeName="System.Linq.Enumerable";
    private const string SeamlessEnumerableTypeName="Seamless.Linq.Enumerable";

    internal static bool TryLower(MethodDeclarationSyntax method,SemanticModel model,out MethodDeclarationSyntax lowered,out SemanticModel loweredModel){
        lowered=method;loweredModel=model;
        if(!method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation=>IsLowerableSetCall(invocation,model))){return false;}
        var rewritten=(MethodDeclarationSyntax)new Rewriter(model).Visit(method)!;
        if(SyntaxFactory.AreEquivalent(method,rewritten)){return false;}
        var annotation=new SyntaxAnnotation("SeamlessSetLowering");
        var root=method.SyntaxTree.GetRoot();
        var newRoot=root.ReplaceNode(method,rewritten.WithAdditionalAnnotations(annotation));
        var newTree=method.SyntaxTree.WithRootAndOptions(newRoot,method.SyntaxTree.Options);
        var compilation=model.Compilation.ReplaceSyntaxTree(method.SyntaxTree,newTree);
        var newModel=compilation.GetSemanticModel(newTree);
        if(newTree.GetRoot().GetAnnotatedNodes(annotation).OfType<MethodDeclarationSyntax>().SingleOrDefault() is not {} annotated){return false;}
        // 下げた結果が型の誤りになる形(System.Linq が見えない・ISet が要る箇所へ渡る)は下げない。
        if(newModel.GetDiagnostics(annotated.Span).Any(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error)){return false;}
        lowered=annotated;loweredModel=newModel;
        return true;
    }

    private static bool IsSetCall(InvocationExpressionSyntax invocation,SemanticModel model,out IMethodSymbol method){
        method=null!;
        if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol||invocation.Expression is not MemberAccessExpressionSyntax){return false;}
        if(symbol.ReducedFrom?.ContainingType.ToDisplayString()!=SetTypeName&&!IsTableCall(symbol)){return false;}
        method=symbol;
        return true;
    }

    // [関係代入] 表の型(Seamless.Database.Table)が自分で持つ Where・Union・Except・Intersect。表へ代入するための演算で、
    // 結果を LINQ が受け取る読み取りの問い合わせでは System.Linq の同じ演算と同じ結果になるので、Set と同じく下げる。
    private static bool IsTableCall(IMethodSymbol method)=>method is{IsStatic:false,ReducedFrom:null,Name:"Where" or "Union" or "Except" or "Intersect"}
        &&method.ContainingType.OriginalDefinition.ContainingNamespace.ToDisplayString()=="Seamless.Database"
        &&method.ContainingType.OriginalDefinition.MetadataName is "Table`2" or "Table`1";

    private static ITypeSymbol ElementType(IMethodSymbol method)=>IsTableCall(method)?method.ContainingType.TypeArguments[method.ContainingType.TypeArguments.Length-1]:method.TypeArguments[0];

    // 結果を LINQ の演算(System.Linq・Seamless.Linq.Enumerable・Set)が受け取るか。受け取らない Set の呼び出しは ISet のまま残す。
    private static bool IsLowerableSetCall(InvocationExpressionSyntax invocation,SemanticModel model){
        if(!IsSetCall(invocation,model,out _)){return false;}
        SyntaxNode current=invocation;
        while(current.Parent is ParenthesizedExpressionSyntax parenthesized){current=parenthesized;}
        if(current.Parent is MemberAccessExpressionSyntax access&&access.Expression==current
            &&access.Parent is InvocationExpressionSyntax consumer
            &&model.GetSymbolInfo(consumer).Symbol is IMethodSymbol consumerMethod
            &&(consumerMethod.ReducedFrom??consumerMethod).ContainingType.ToDisplayString() is SystemEnumerableTypeName or SeamlessEnumerableTypeName or SetTypeName){
            return true;
        }
        // 表の型の演算を重ねた連鎖は、外側が下げられるときだけ下げる(表へ代入する連鎖は表の型のまま残す)。
        if(current.Parent is MemberAccessExpressionSyntax tableAccess&&tableAccess.Expression==current
            &&tableAccess.Parent is InvocationExpressionSyntax tableConsumer
            &&model.GetSymbolInfo(tableConsumer).Symbol is IMethodSymbol tableMethod&&IsTableCall(tableMethod)){
            return IsLowerableSetCall(tableConsumer,model);
        }
        // IEnumerable<T> として受け取られる(SelectMany の集合を返すラムダの戻り値など)。ISet として使われないので下げてよい。
        return model.GetTypeInfo((ExpressionSyntax)current).ConvertedType is INamedTypeSymbol{TypeArguments.Length:1} converted
            &&converted.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.IEnumerable<T>";
    }

    private sealed class Rewriter(SemanticModel model):CSharpSyntaxRewriter{
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node){
            var visited=(InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if(!IsLowerableSetCall(node,model)||!IsSetCall(node,model,out var method)
                ||visited.Expression is not MemberAccessExpressionSyntax access){
                return visited;
            }
            var originalReceiver=((MemberAccessExpressionSyntax)node.Expression).Expression;
            while(originalReceiver is ParenthesizedExpressionSyntax parenthesized){originalReceiver=parenthesized.Expression;}
            ExpressionSyntax receiver=access.Expression;
            // 受け手が下げた Set の呼び出しなら既に IEnumerable。そうでなければ(表・ビューの集合など)IEnumerable<T> への変換を挟む。
            // AsEnumerable() は式の最適化(AlgebraicRewriter)が冗長として消し、Set の演算へ結び付き直すので使わない。
            if(!(originalReceiver is InvocationExpressionSyntax receiverInvocation&&IsLowerableSetCall(receiverInvocation,model))){
                var element=ElementType(method).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                receiver=SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName("global::System.Collections.Generic.IEnumerable<"+element+">"),Parenthesize(receiver)));
            }
            ExpressionSyntax lowered=visited.WithExpression(access.WithExpression(receiver));
            // Set.SelectMany の集合を返すラムダの戻り値なら、外側が結果全体の重複を除くので、内側で除かなくても結果は同じ。
            // 結果を写す演算は重複が出うるので、下げた後に重複を除く。GroupBy は結果を作る関数を渡す形だけ(組そのものはキーで重複しない)。
            var projects=method.Name is "Select" or "SelectMany" or "Join" or "LeftJoin" or "RightJoin" or "GroupJoin"
                ||method.Name=="GroupBy"&&method.Parameters.Any(static parameter=>parameter.Name=="resultSelector");
            if(projects&&!IsCollectionOfSetSelectMany(node,model)){
                lowered=SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    lowered,SyntaxFactory.IdentifierName("Distinct")));
            }
            return lowered.WithTriviaFrom(node);
        }

        private static bool IsCollectionOfSetSelectMany(InvocationExpressionSyntax node,SemanticModel model){
            SyntaxNode current=node;
            while(current.Parent is ParenthesizedExpressionSyntax parenthesized){current=parenthesized;}
            return current.Parent is LambdaExpressionSyntax{Body:var body} lambda&&body==current
                &&lambda.Parent is ArgumentSyntax{Parent:ArgumentListSyntax{Parent:InvocationExpressionSyntax outer}}
                &&IsSetCall(outer,model,out var outerMethod)&&outerMethod.Name=="SelectMany";
        }

        private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)=>
            expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax
                ? expression
                : SyntaxFactory.ParenthesizedExpression(expression);
    }
}
