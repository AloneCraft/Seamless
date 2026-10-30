using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

// default 初期化の直後に必ず代入する生成ローカルを、最初から値を持つ宣言にする。
// 文をまたいで計算を移動せず、利用者のローカルや副作用・例外の順序には触れない。
internal static class GeneratedLocalInitializers{
    internal static string Simplify(string text,bool methodBody=false){
        if(methodBody){
            var block=(BlockSyntax)SyntaxFactory.ParseStatement("{"+text+"}");
            var rewritten=(BlockSyntax)new Rewriter().Visit(block)!;
            var result=rewritten.ToFullString();
            return result.Substring(1,result.Length-2);
        }
        var root=CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
        return new Rewriter().Visit(root)!.ToFullString();
    }

    private sealed class Rewriter:CSharpSyntaxRewriter{
        public override SyntaxNode? VisitBlock(BlockSyntax node){
            var visited=(BlockSyntax)base.VisitBlock(node)!;
            var statements=visited.Statements.ToList();
            for(var index=0;index+1<statements.Count;index++){
                if(statements[index] is not LocalDeclarationStatementSyntax declaration
                    ||declaration.Declaration.Variables.Count!=1
                    ||declaration.UsingKeyword!=default||declaration.AwaitKeyword!=default){
                    continue;
                }
                var variable=declaration.Declaration.Variables[0];
                if(!variable.Identifier.ValueText.StartsWith("__",StringComparison.Ordinal)
                    ||!IsDefault(variable.Initializer?.Value)
                    ||statements[index+1] is not ExpressionStatementSyntax{Expression:AssignmentExpressionSyntax assignment} next
                    ||!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    ||assignment.Left is not IdentifierNameSyntax identifier
                    ||identifier.Identifier.ValueText!=variable.Identifier.ValueText
                    ||assignment.Right.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(name=>name.Identifier.ValueText==variable.Identifier.ValueText)){
                    continue;
                }
                var initialized=variable.WithInitializer(variable.Initializer!.WithValue(assignment.Right));
                statements[index]=declaration.WithDeclaration(declaration.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(initialized)))
                    .WithTrailingTrivia(next.GetTrailingTrivia());
                statements.RemoveAt(index+1);
            }
            return visited.WithStatements(SyntaxFactory.List(statements));
        }

        private static bool IsDefault(ExpressionSyntax? expression)=>expression switch{
            DefaultExpressionSyntax=>true,
            LiteralExpressionSyntax literal=>literal.IsKind(SyntaxKind.DefaultLiteralExpression),
            PostfixUnaryExpressionSyntax suppress when suppress.IsKind(SyntaxKind.SuppressNullableWarningExpression)=>IsDefault(suppress.Operand),
            _=>false
        };
    }
}