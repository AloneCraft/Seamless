using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator{
    internal sealed partial class GenerationPipeline{
        internal static bool CanLiftConditionQueries(ExpressionSyntax expression,SemanticModel model,IMethodSymbol method){
            if(method.Parameters.Any(static parameter=>parameter.RefKind!=RefKind.None)||!method.IsStatic&&method.ContainingType.IsValueType){return false;}
            foreach(var node in expression.DescendantNodesAndSelf()){
                if(node is AwaitExpressionSyntax or DeclarationExpressionSyntax or PatternSyntax or ConditionalAccessExpressionSyntax){return false;}
                if(node is IdentifierNameSyntax&&model.GetSymbolInfo(node).Symbol is ILocalSymbol local&&(local.RefKind!=RefKind.None||local.Type.IsRefLikeType)){return false;}
            }
            return true;
        }
        private bool RenderConditionQueries(ExpressionSyntax expression,SemanticModel semanticModel,INamedTypeSymbol containingType,ref int uniqueNumber,bool methodIsStatic,
            ref string staticMethods,List<string> unsafeAccessorCollector,List<Fragment> generatedFragments,StringBuilder builder,ref int linqInvocationCount,HashSet<string> usedNames,out string rendered){
            rendered=string.Empty;
            if(!CanLiftConditionQueries(expression,semanticModel,this.state.Target.Method)){
                rendered=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);return true;
            }
            var replacements=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var invocation in this.外側LINQ呼び出し取得_式(expression,semanticModel)){
                var type=semanticModel.GetTypeInfo(invocation).Type;
                if(type is null||type.IsAnonymousType||type.IsRefLikeType){continue;}
                var body=new StringBuilder();
                if(!this.LINQ呼び出し差し替え式本体生成(invocation,semanticModel,containingType,ref uniqueNumber,methodIsStatic,false,
                    ref staticMethods,unsafeAccessorCollector,generatedFragments,body,ref linqInvocationCount,usedNames)){return false;}
                var name=AllocateName(usedNames,"conditionQuery");
                builder.AppendLine("// [条件/評価位置] 宣言ではqueryを実行しない。元の式の呼出し位置で実行し、短絡・反復・例外の順序を保つ。");
                builder.AppendLine(type.ToDisplayString(完全修飾形式)+" "+name+"(){");builder.Append(body);builder.AppendLine("}");
                replacements[NormalizeExpressionText(invocation.ToString())]=name+"()";
            }
            rendered=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector,replacements);return true;
        }
    }
}
