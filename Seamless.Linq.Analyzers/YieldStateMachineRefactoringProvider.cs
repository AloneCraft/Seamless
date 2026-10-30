using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Linq.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp,Name = nameof(YieldStateMachineRefactoringProvider)),Shared]
public sealed class YieldStateMachineRefactoringProvider:CodeRefactoringProvider {
    public const string Title = "Seamless: ゼロアロケーション構造体列挙子へ展開";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context){
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if(root is null){
            return;
        }

        var node = root.FindNode(context.Span);
        var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if(methodDeclaration is null||methodDeclaration.Body is null){
            return;
        }

        var yieldStatements = methodDeclaration.Body.DescendantNodes().OfType<YieldStatementSyntax>().ToList();
        if(yieldStatements.Count==0){
            return;
        }

        var action = CodeAction.Create(
            title: Title,
            createChangedDocument: c => this.ExpandYieldStateMachineAsync(context.Document,root,methodDeclaration,c),
            equivalenceKey: Title);

        context.RegisterRefactoring(action);
    }

    private Task<Document> ExpandYieldStateMachineAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken){
        var methodName = methodDeclaration.Identifier.ValueText;
        var enumerableTypeName = methodName+"Enumerable";
        var enumeratorTypeName = methodName+"Enumerator";

        var elementType = "object";
        if(methodDeclaration.ReturnType is GenericNameSyntax genericName&&genericName.TypeArgumentList.Arguments.Count==1){
            elementType=genericName.TypeArgumentList.Arguments[0].ToString();
        }

        var parameters = methodDeclaration.ParameterList.Parameters;
        var paramNames = global::System.Linq.Enumerable.Select(parameters,static p => p.Identifier.ValueText).ToArray();
        var paramTypes = global::System.Linq.Enumerable.Select(parameters,static p => p.Type?.ToString()??"object").ToArray();

        var structFields = new StringBuilder();
        var ctorParams = new StringBuilder();
        var ctorAssigns = new StringBuilder();
        var forwardArgs = new StringBuilder();

        for(var i = 0;i<paramNames.Length;i++){
            if(i>0){
                ctorParams.Append(",");
                forwardArgs.Append(",");
            }
            structFields.AppendLine($"        private readonly {paramTypes[i]} _{paramNames[i]};");
            ctorParams.Append($"{paramTypes[i]} {paramNames[i]}");
            ctorAssigns.AppendLine($"            this._{paramNames[i]}={paramNames[i]};");
            forwardArgs.Append($"this._{paramNames[i]}");
        }

        // ローカル変数（forループカウンタ等）のフィールド昇格
        var hoistedFields = new StringBuilder();
        var hoistedInit = new StringBuilder();
        var hoistedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach(var forStmt in methodDeclaration.Body!.DescendantNodes().OfType<ForStatementSyntax>()){
            if(forStmt.Declaration is not null){
                var varType = forStmt.Declaration.Type.ToString();
                if(varType=="var"){
                    varType="int";
                }
                foreach(var v in forStmt.Declaration.Variables){
                    var vName = v.Identifier.ValueText;
                    if(hoistedNames.Add(vName)){
                        hoistedFields.AppendLine($"        private {varType} _{vName};");
                        var initExpr = v.Initializer?.Value.ToString()??"0";
                        hoistedInit.AppendLine($"            this._{vName}={initExpr};");
                    }
                }
            }
        }

        // AST書換え: yield return -> { this._current = expr; return true; }
        // 識別子 i -> this._i, count -> this._count
        var rewriter = new YieldToMoveNextRewriter(paramNames,hoistedNames);
        var transformedBody = (BlockSyntax)rewriter.Visit(methodDeclaration.Body!)!;

        var moveNextStatements = new List<StatementSyntax>();
        moveNextStatements.AddRange(transformedBody.Statements);
        moveNextStatements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)));

        var moveNextBody = SyntaxFactory.Block(moveNextStatements).NormalizeWhitespace().ToFullString();

        var generatedStructsText = $$"""

        public readonly struct {{enumerableTypeName}}:global::System.Collections.Generic.IEnumerable<{{elementType}}>{
    {{structFields.ToString().TrimEnd()}}

            public {{enumerableTypeName}}({{ctorParams}}){
    {{ctorAssigns.ToString().TrimEnd()}}
            }

            public {{enumeratorTypeName}} GetEnumerator(){
                return new {{enumeratorTypeName}}({{forwardArgs}});
            }

            global::System.Collections.Generic.IEnumerator<{{elementType}}> global::System.Collections.Generic.IEnumerable<{{elementType}}>.GetEnumerator(){
                return this.GetEnumerator();
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator(){
                return this.GetEnumerator();
            }
        }

        public struct {{enumeratorTypeName}}:global::System.Collections.Generic.IEnumerator<{{elementType}}>{
            private int _state;
            private {{elementType}} _current;
    {{structFields.ToString().TrimEnd()}}
    {{hoistedFields.ToString().TrimEnd()}}

            public {{enumeratorTypeName}}({{ctorParams}}){
                this._state=0;
                this._current=default!;
    {{ctorAssigns.ToString().TrimEnd()}}
    {{hoistedInit.ToString().TrimEnd()}}
            }

            public {{elementType}} Current{
                get{
                    return this._current;
                }
            }

            object global::System.Collections.IEnumerator.Current{
                get{
                    return this._current!;
                }
            }

            public bool MoveNext()
    {{moveNextBody}}

            public void Dispose(){
            }

            public void Reset(){
                throw new global::System.NotSupportedException();
            }
        }
    """;

        var newMethodReturnType = SyntaxFactory.ParseTypeName(enumerableTypeName)
            .WithTrailingTrivia(SyntaxFactory.Space);

        var callArgs = string.Join(",",paramNames);
        var newMethodBody = SyntaxFactory.Block(
            SyntaxFactory.ReturnStatement(
                SyntaxFactory.ParseExpression($"new {enumerableTypeName}({callArgs})")
            )
        );

        var newMethod = methodDeclaration
            .WithReturnType(newMethodReturnType)
            .WithBody(newMethodBody);

        var parsedGeneratedMembers = SyntaxFactory.ParseCompilationUnit(generatedStructsText).Members;

        var containingType = methodDeclaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if(containingType is null){
            return Task.FromResult(document);
        }

        var newContainingType = containingType
            .ReplaceNode(methodDeclaration,newMethod);

        foreach(var member in parsedGeneratedMembers){
            newContainingType=newContainingType.AddMembers(member);
        }

        var newRoot = root.ReplaceNode(containingType,newContainingType);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private sealed class YieldToMoveNextRewriter:CSharpSyntaxRewriter {
        private readonly HashSet<string> _paramNames;
        private readonly HashSet<string> _hoistedNames;

        public YieldToMoveNextRewriter(string[] paramNames,HashSet<string> hoistedNames){
            this._paramNames=new HashSet<string>(paramNames,StringComparer.Ordinal);
            this._hoistedNames=hoistedNames;
        }

        public override SyntaxNode? VisitYieldStatement(YieldStatementSyntax node){
            if(node.IsKind(SyntaxKind.YieldReturnStatement)){
                var expr = (ExpressionSyntax)this.Visit(node.Expression!)!;
                var assign = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.ParseExpression("this._current"),
                        expr
                    )
                );
                var ret = SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
                return SyntaxFactory.Block(assign,ret);
            }else{
                return SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression));
            }
        }

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node){
            // for(var i = 0; i < count; i++) -> for(; this._i < this._count; this._i++)
            var newNode = (ForStatementSyntax)base.VisitForStatement(node)!;
            return newNode.WithDeclaration(null);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node){
            var name = node.Identifier.ValueText;
            if(this._paramNames.Contains(name)){
                return SyntaxFactory.IdentifierName($"this._{name}");
            }
            if(this._hoistedNames.Contains(name)){
                return SyntaxFactory.IdentifierName($"this._{name}");
            }
            return base.VisitIdentifierName(node);
        }
    }
}
