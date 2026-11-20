using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

/// <summary>
/// 関係代入(表のプロパティへの代入 <c>T = …</c>)を、形の分かった差分の処理の呼び出しへ書き換える
/// (Document/refactoring-designs/20261117-関係代入.md)。
/// <list type="bullet">
/// <item><description><c>T = T.Union(R1)…</c> → <c>T.AssignUnion(R1,…)</c>(表に無い行だけを足す。表の全件は読まない)。</description></item>
/// <item><description><c>T = T.Except(R1)…</c> → <c>T.AssignExcept(R1,…)</c>(全列が等しい行だけを消す。表の全件は読まない)。</description></item>
/// <item><description><c>T = T.Where(p)</c> → <c>T.AssignWhere(p)</c>。</description></item>
/// <item><description><c>T = new(q)</c>・<c>T = [..q]</c> → <c>T.AssignRows(q)</c>(差分だけを書く置き換え)。</description></item>
/// </list>
/// 代入先は集合なので、右の行の集まりの末尾の並べ替え(OrderBy・ThenBy など)・Distinct・AsEnumerable は結果に影響しない。取り除く。
/// これ以外の形は書き換えず、表の set が実行時に式を読んで同じ差分を求める。
/// </summary>
internal static class RelationalAssignmentLowering{
    private const string SystemEnumerableTypeName="System.Linq.Enumerable";

    /// <summary>メソッドの本体に関係代入があるか(FLinq の対象を探す段で使う)。</summary>
    internal static bool ContainsAssignment(SyntaxNode body,SemanticModel model)=>
        body.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment=>IsTableAssignment(assignment,model)&&
            (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)||assignment.Parent is ExpressionStatementSyntax&&LowerCompound(assignment,model) is not null));

    internal static bool TryLower(MethodDeclarationSyntax method,SemanticModel model,out MethodDeclarationSyntax lowered,out SemanticModel loweredModel,out int count){
        lowered=method;loweredModel=model;count=0;
        var targets=method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment=>assignment.Parent is ExpressionStatementSyntax&&IsTableAssignment(assignment,model)).ToArray();
        if(targets.Length==0){return false;}
        var replacements=new Dictionary<SyntaxNode,SyntaxNode>();
        foreach(var assignment in targets){
            if(Lower(assignment,model) is{} call){replacements[assignment]=call.WithTriviaFrom(assignment);}
        }
        if(replacements.Count==0){return false;}
        var rewritten=method.ReplaceNodes(replacements.Keys,(original,_)=>replacements[original]);
        var annotation=new SyntaxAnnotation("SeamlessRelationalAssignment");
        var root=method.SyntaxTree.GetRoot();
        var newRoot=root.ReplaceNode(method,rewritten.WithAdditionalAnnotations(annotation));
        var newTree=method.SyntaxTree.WithRootAndOptions(newRoot,method.SyntaxTree.Options);
        var compilation=model.Compilation.ReplaceSyntaxTree(method.SyntaxTree,newTree);
        var newModel=compilation.GetSemanticModel(newTree);
        if(newTree.GetRoot().GetAnnotatedNodes(annotation).OfType<MethodDeclarationSyntax>().SingleOrDefault() is not{} annotated){return false;}
        // 書き換えた結果が型の誤りになるなら書き換えない(表の set が実行時に同じ差分を求める)。
        if(newModel.GetDiagnostics(annotated.Span).Any(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error)){return false;}
        lowered=annotated;loweredModel=newModel;count=replacements.Count;
        return true;
    }

    // 左辺が書き込み可能な表のプロパティ。
    private static bool IsTableAssignment(AssignmentExpressionSyntax assignment,SemanticModel model)=>
        assignment.Kind() is SyntaxKind.SimpleAssignmentExpression or SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression or SyntaxKind.AndAssignmentExpression
        &&model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol{SetMethod:not null} property
        &&IsTableType(property.Type);

    private static bool IsTableType(ITypeSymbol? type)=>type is INamedTypeSymbol named
        &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()=="Seamless.Database"
        &&named.OriginalDefinition.MetadataName is "Table`2" or "Table`1";

    private static bool IsTableCall(InvocationExpressionSyntax invocation,SemanticModel model,out string name){
        name=string.Empty;
        if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol{IsStatic:false,ReducedFrom:null} method
            ||invocation.Expression is not MemberAccessExpressionSyntax
            ||method.Name is not("Where" or "Union" or "Except" or "Intersect")
            ||!IsTableType(method.ContainingType)){return false;}
        name=method.Name;
        return true;
    }

    private static InvocationExpressionSyntax? Lower(AssignmentExpressionSyntax assignment,SemanticModel model){
        if(!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)){return LowerCompound(assignment,model);}
        var target=assignment.Left;
        var value=Unparenthesize(assignment.Right);
        // 置き換え: new(q)・new Table<…>(q)・[..q]
        if(value is BaseObjectCreationExpressionSyntax{ArgumentList.Arguments.Count:1} creation&&creation.Initializer is null
            &&IsTableType(model.GetTypeInfo(value).Type)){
            return Call(target,"AssignRows",[StripOrderAndDuplicates(creation.ArgumentList!.Arguments[0].Expression,model)]);
        }
        if(value is CollectionExpressionSyntax{Elements.Count:1} collection&&collection.Elements[0] is SpreadElementSyntax spread){
            return Call(target,"AssignRows",[StripOrderAndDuplicates(spread.Expression,model)]);
        }
        // 左辺と同じ表を根に持つ演算の連鎖。
        var operations=new List<(string Name,ExpressionSyntax Argument)>();
        var current=value;
        while(current is InvocationExpressionSyntax invocation&&IsTableCall(invocation,model,out var name)&&invocation.ArgumentList.Arguments.Count==1){
            operations.Add((name,invocation.ArgumentList.Arguments[0].Expression));
            current=Unparenthesize(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        }
        if(operations.Count==0||!SameTable(target,current,model)){return null;}
        operations.Reverse();
        if(operations.TrueForAll(static operation=>operation.Name=="Union")){
            return Call(target,"AssignUnion",[..operations.Select(operation=>StripOrderAndDuplicates(operation.Argument,model))]);
        }
        if(operations.TrueForAll(static operation=>operation.Name=="Except")){
            return Call(target,"AssignExcept",[..operations.Select(operation=>StripOrderAndDuplicates(operation.Argument,model))]);
        }
        if(operations.Count==1&&operations[0].Name=="Where"){
            return Call(target,"AssignWhere",[operations[0].Argument]);
        }
        // それ以外の組み合わせは、表の set が実行時に同じ規則で差分を求める。
        return null;
    }

    private static InvocationExpressionSyntax? LowerCompound(AssignmentExpressionSyntax assignment,SemanticModel model){
        if(model.GetOperation(assignment) is not ICompoundAssignmentOperation{
                Target:IPropertyReferenceOperation{Property:var property},OperatorMethod:{ } operation}||
            !IsTableType(operation.ContainingType)||operation.Parameters.Length!=2||
            !UsesStandardSetter(property,model.Compilation)){return null;}

        // 受け手と右辺はそれぞれ一度だけ評価する。集合式の型付けと null の検査も演算子と同じ順で行う。
        var rowsType=SyntaxFactory.ParseTypeName(operation.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var right=SyntaxFactory.CastExpression(rowsType,SyntaxFactory.ParenthesizedExpression(assignment.Right.WithoutTrivia()));
        var missingRows=SyntaxFactory.ThrowExpression(SyntaxFactory.ParseExpression("new global::System.ArgumentNullException(\"rows\")"))
            .WithThrowKeyword(SyntaxFactory.Token(SyntaxKind.ThrowKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        var checkedRows=SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression,right,missingRows);
        var method=assignment.Kind() switch{
            SyntaxKind.AddAssignmentExpression=>"AssignUnion",
            SyntaxKind.SubtractAssignmentExpression=>"AssignExcept",
            _=>"AssignIntersect"
        };
        return Call(assignment.Left,method,[checkedRows]);
    }

    private static bool UsesStandardSetter(IPropertySymbol property,Compilation compilation){
        if(property.IsStatic||property.IsVirtual||property.IsOverride||property.IsAbstract){return false;}
        if(IsStandardStoredTable(property,compilation)){return true;}
        // 型付き取引のルート表は、生成された get/set の両方が同じDBの表を開く。
        if(property.ContainingType.Name!="TypedTransaction"||
            !property.DeclaringSyntaxReferences.Any(reference=>reference.SyntaxTree.FilePath.EndsWith(".Container.g.cs",StringComparison.Ordinal))||
            property.ContainingType.ContainingType is not{} database){return false;}
        return database.GetMembers(property.Name).OfType<IPropertySymbol>().Any(table=>IsStandardStoredTable(table,compilation));
    }

    private static bool IsStandardStoredTable(IPropertySymbol property,Compilation compilation){
        if(property.IsVirtual||property.IsOverride||property.IsAbstract){return false;}
        var implementation=property.PartialImplementationPart??property;
        foreach(var reference in implementation.DeclaringSyntaxReferences){
            if(reference.GetSyntax() is not PropertyDeclarationSyntax{AccessorList:{ } accessors}){continue;}
            var getter=accessors.Accessors.FirstOrDefault(accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
            var setter=accessors.Accessors.FirstOrDefault(accessor=>accessor.IsKind(SyntaxKind.SetAccessorDeclaration))?.ExpressionBody?.Expression;
            if(getter is not InvocationExpressionSyntax{
                Expression:MemberAccessExpressionSyntax{Expression:ThisExpressionSyntax,Name:GenericNameSyntax{Identifier.ValueText:"Table"}}} call||
                call.ArgumentList.Arguments.Count>1||
                call.ArgumentList.Arguments.Count==1&&call.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax||
                setter is not InvocationExpressionSyntax{
                    Expression:MemberAccessExpressionSyntax{Name.Identifier.ValueText:"Assign",Expression:MemberAccessExpressionSyntax{Expression:ThisExpressionSyntax} table},
                    ArgumentList.Arguments.Count:1} assign||
                table.Name.Identifier.ValueText!=property.Name||
                assign.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax{Identifier.ValueText:"value"}){continue;}
            var model=compilation.GetSemanticModel(reference.SyntaxTree);
            if(model.GetSymbolInfo(call).Symbol is IMethodSymbol method&&method.ContainingNamespace.ToDisplayString()=="Seamless.Database"&&
                method.ContainingType.Name is "DatabaseContainer" or "DatabaseSchema"){return true;}
        }
        return false;
    }

    // 左辺と同じ表か。同じ式で書いた同じプロパティを指すときだけ(取引の表は読むたびに包みが変わるが、同じ取引の同じ表)。
    private static bool SameTable(ExpressionSyntax target,ExpressionSyntax candidate,SemanticModel model)=>
        SyntaxFactory.AreEquivalent(Unparenthesize(target),candidate)
        &&SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(target).Symbol,model.GetSymbolInfo(candidate).Symbol);

    // 代入先は集合なので、行の集まりの末尾の並べ替え・重複除去・AsEnumerable は結果に影響しない。
    private static ExpressionSyntax StripOrderAndDuplicates(ExpressionSyntax expression,SemanticModel model){
        var current=Unparenthesize(expression);
        while(current is InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax access} invocation
            &&model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
            &&(method.ReducedFrom??method).ContainingType.ToDisplayString()==SystemEnumerableTypeName
            &&method.Name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or "Order" or "OrderDescending" or "Distinct" or "AsEnumerable"
            &&(method.Name!="Distinct"||invocation.ArgumentList.Arguments.Count==0)){
            current=Unparenthesize(access.Expression);
        }
        return current;
    }

    private static InvocationExpressionSyntax Call(ExpressionSyntax target,string name,ExpressionSyntax[] arguments)=>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Parenthesize(target.WithoutTrivia()),SyntaxFactory.IdentifierName(name)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(static argument=>SyntaxFactory.Argument(argument.WithoutTrivia())))));

    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression){
        while(expression is ParenthesizedExpressionSyntax parenthesized){expression=parenthesized.Expression;}
        return expression;
    }

    private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)=>
        expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);
}
