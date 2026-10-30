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
    public const string SyncTitle = "Seamless: ゼロアロケーション構造体列挙子へ展開";
    public const string AsyncTitle = "Seamless: ゼロアロケーション非同期構造体列挙子へ展開";
    public const string Title = SyncTitle;

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

        var returnTypeText = methodDeclaration.ReturnType.ToString();
        var isAsync = returnTypeText.Contains("IAsyncEnumerable",StringComparison.Ordinal)
            ||methodDeclaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.AsyncKeyword))
            ||methodDeclaration.Body.DescendantNodes().Any(static n => n.IsKind(SyntaxKind.AwaitExpression));

        var title = isAsync ? AsyncTitle : SyncTitle;

        var action = CodeAction.Create(
            title: title,
            createChangedDocument: c => this.ExpandStateMachineAsync(context.Document,root,methodDeclaration,isAsync,c),
            equivalenceKey: title);

        context.RegisterRefactoring(action);
    }

    private Task<Document> ExpandStateMachineAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        bool isAsync,
        CancellationToken cancellationToken){
        return isAsync
            ? this.ExpandAsyncStateMachineAsync(document,root,methodDeclaration,cancellationToken)
            : this.ExpandSyncStateMachineAsync(document,root,methodDeclaration,cancellationToken);
    }

    private Task<Document> ExpandSyncStateMachineAsync(
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

        var hoistedFields = new StringBuilder();
        var hoistedInit = new StringBuilder();
        var hoistedNames = new HashSet<string>(StringComparer.Ordinal);

        CollectHoistedVariables(methodDeclaration.Body!,hoistedFields,hoistedInit,hoistedNames);

        var rewriter = new YieldToMoveNextRewriter(paramNames,hoistedNames,false);
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

        return ReplaceMethodWithGeneratedStructs(document,root,methodDeclaration,enumerableTypeName,paramNames,generatedStructsText);
    }

    private Task<Document> ExpandAsyncStateMachineAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken){
        var methodName = methodDeclaration.Identifier.ValueText;
        var enumerableTypeName = methodName+"AsyncEnumerable";
        var enumeratorTypeName = methodName+"AsyncEnumerator";

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

        var hoistedFields = new StringBuilder();
        var hoistedInit = new StringBuilder();
        var hoistedNames = new HashSet<string>(StringComparer.Ordinal);

        CollectHoistedVariables(methodDeclaration.Body!,hoistedFields,hoistedInit,hoistedNames);

        var hasAwait = methodDeclaration.Body!.DescendantNodes().Any(static n => n.IsKind(SyntaxKind.AwaitExpression));
        var rewriter = new YieldToMoveNextRewriter(paramNames,hoistedNames,true,hasAwait);
        var transformedBody = (BlockSyntax)rewriter.Visit(methodDeclaration.Body!)!;

        var moveNextStatements = new List<StatementSyntax>();
        moveNextStatements.AddRange(transformedBody.Statements);
        var falseExpr = hasAwait
            ? "false"
            : "new global::System.Threading.Tasks.ValueTask<bool>(false)";
        moveNextStatements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression(falseExpr)));

        var moveNextBody = SyntaxFactory.Block(moveNextStatements).NormalizeWhitespace().ToFullString();
        var moveNextModifiers = hasAwait ? "public async global::System.Threading.Tasks.ValueTask<bool> MoveNextAsync()" : "public global::System.Threading.Tasks.ValueTask<bool> MoveNextAsync()";

        var generatedStructsText = $$"""

        public readonly struct {{enumerableTypeName}}:global::System.Collections.Generic.IAsyncEnumerable<{{elementType}}>{
    {{structFields.ToString().TrimEnd()}}

            public {{enumerableTypeName}}({{ctorParams}}){
    {{ctorAssigns.ToString().TrimEnd()}}
            }

            public {{enumeratorTypeName}} GetAsyncEnumerator(global::System.Threading.CancellationToken cancellationToken = default){
                return new {{enumeratorTypeName}}({{forwardArgs}});
            }

            global::System.Collections.Generic.IAsyncEnumerator<{{elementType}}> global::System.Collections.Generic.IAsyncEnumerable<{{elementType}}>.GetAsyncEnumerator(global::System.Threading.CancellationToken cancellationToken){
                return this.GetAsyncEnumerator(cancellationToken);
            }
        }

        public struct {{enumeratorTypeName}}:global::System.Collections.Generic.IAsyncEnumerator<{{elementType}}>{
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

            {{moveNextModifiers}}
    {{moveNextBody}}

            public global::System.Threading.Tasks.ValueTask DisposeAsync(){
                return default;
            }
        }
    """;

        return ReplaceMethodWithGeneratedStructs(document,root,methodDeclaration,enumerableTypeName,paramNames,generatedStructsText);
    }

    private static void CollectHoistedVariables(
        BlockSyntax body,
        StringBuilder hoistedFields,
        StringBuilder hoistedInit,
        HashSet<string> hoistedNames){
        foreach(var forStmt in body.DescendantNodes().OfType<ForStatementSyntax>()){
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
    }

    private static Task<Document> ReplaceMethodWithGeneratedStructs(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        string enumerableTypeName,
        string[] paramNames,
        string generatedStructsText){
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
            .WithBody(newMethodBody)
            .WithModifiers(SyntaxFactory.TokenList(methodDeclaration.Modifiers.Where(static m => !m.IsKind(SyntaxKind.AsyncKeyword))));

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
        private readonly bool _isAsync;
        private readonly bool _hasAwait;

        public YieldToMoveNextRewriter(string[] paramNames,HashSet<string> hoistedNames,bool isAsync,bool hasAwait = false){
            this._paramNames=new HashSet<string>(paramNames,StringComparer.Ordinal);
            this._hoistedNames=hoistedNames;
            this._isAsync=isAsync;
            this._hasAwait=hasAwait;
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
                var retExpr = this._isAsync
                    ? (this._hasAwait ? "true" : "new global::System.Threading.Tasks.ValueTask<bool>(true)")
                    : "true";
                var ret = SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression(retExpr));
                return SyntaxFactory.Block(assign,ret);
            }else{
                var retExpr = this._isAsync
                    ? (this._hasAwait ? "false" : "new global::System.Threading.Tasks.ValueTask<bool>(false)")
                    : "false";
                return SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression(retExpr));
            }
        }

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node){
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
