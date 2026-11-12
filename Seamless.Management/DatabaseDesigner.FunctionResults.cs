using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    private sealed record FunctionResultInference(MethodDeclarationSyntax? Method,InvocationExpressionSyntax? Factory,GenericNameSyntax? Helper,ITypeSymbol? Type,string? Reason);

    private static FunctionResultInference InferFunctionResult(CSharpCompilation compilation,IMethodSymbol method){
        var syntax=method.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
        if(method.ReturnType is not INamedTypeSymbol{Name:"ScalarFunction" or "TableFunction",TypeArguments.Length:1} wrapper||wrapper.ContainingNamespace.ToDisplayString()!="Seamless.Database"
            ||syntax?.ExpressionBody?.Expression is not InvocationExpressionSyntax factory||factory.Expression is not MemberAccessExpressionSyntax{Name:GenericNameSyntax helper}
            ||helper.Identifier.ValueText is not("ScalarFunction" or "TableFunction")){
            return new(syntax,null,null,null,"CustomFunctionFactory");
        }
        var model=compilation.GetSemanticModel(factory.SyntaxTree);
        var binding=model.GetSymbolInfo(factory);
        var methods=binding.CandidateSymbols.OfType<IMethodSymbol>().ToArray();
        var original=binding.Symbol as IMethodSymbol;
        if(original is null&&methods.Length==1){original=methods[0];}
        if(original?.ContainingAssembly.Name!="Seamless.Database"){
            return new(syntax,factory,helper,null,"CustomFunctionFactory");
        }
        if(QueryDefinitionArgument(factory)?.Expression is AnonymousFunctionExpressionSyntax lambda&&model.GetOperation(lambda) is IAnonymousFunctionOperation operation&&HasImplicitUserReturnConversion(operation.Body)){
            return new(syntax,factory,helper,null,"DeclaredReturnConversionRequired");
        }
        var validScalar=wrapper.Name=="ScalarFunction"&&binding.Symbol is not null&&!model.GetDiagnostics(factory.Span).Any(item=>item.Severity==DiagnosticSeverity.Error);
        if(validScalar){
            try{if(FunctionElement(wrapper.TypeArguments[0]) is not null){return new(syntax,factory,helper,null,"DeclaredScalarSequence");}}
            catch(ManagementException){return new(syntax,factory,helper,null,"DeclaredResultTypeRequired");}
        }
        // 型引数を外した解析用の呼出しで、古い公開戻り型より先に本体の型を推論する。
        var annotation=new SyntaxAnnotation();
        var probe=factory.WithExpression(((MemberAccessExpressionSyntax)factory.Expression).WithName(SyntaxFactory.IdentifierName("ScalarFunction").WithTriviaFrom(helper))).WithAdditionalAnnotations(annotation);
        var tree=factory.SyntaxTree;
        var root=tree.GetRoot().ReplaceNode(factory,probe);
        var probeTree=tree.WithRootAndOptions(root,tree.Options);
        var inferredCompilation=compilation.ReplaceSyntaxTree(tree,probeTree);
        var call=probeTree.GetRoot().GetAnnotatedNodes(annotation).OfType<InvocationExpressionSyntax>().Single();
        var inferred=inferredCompilation.GetSemanticModel(probeTree).GetSymbolInfo(call).Symbol as IMethodSymbol;
        if(inferred is not{TypeArguments.Length:1}||inferred.ContainingAssembly.Name!="Seamless.Database"||inferred.TypeArguments[0].TypeKind==TypeKind.Error){
            return new(syntax,factory,helper,null,"DeclaredResultTypeRequired");
        }
        var type=inferred.TypeArguments[0];
        // 既存の単一値がobject等に包まれた列挙なら、遅延列挙へ変えて評価時点を動かさない。
        if(validScalar){
            try{if(FunctionElement(type) is not null){return new(syntax,factory,helper,null,"DeclaredScalarSequence");}}
            catch(ManagementException){return new(syntax,factory,helper,null,"DeclaredResultTypeRequired");}
        }
        if(ContainsAnonymousResult(type)){return new(syntax,factory,helper,null,"AnonymousResultRequiresNamedType");}
        return new(syntax,factory,helper,type,null);
    }

    private static bool ContainsAnonymousResult(ITypeSymbol type){
        if(type is IArrayTypeSymbol array){return ContainsAnonymousResult(array.ElementType);}
        if(type is INamedTypeSymbol named){return named.IsAnonymousType||named.TypeArguments.Any(ContainsAnonymousResult);}
        return false;
    }

    private static bool IsAsyncFunctionResult(ITypeSymbol type){
        if(type is IArrayTypeSymbol array){return IsAsyncFunctionResult(array.ElementType);}
        if(type is not INamedTypeSymbol named){return false;}
        if(named.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T){return IsAsyncFunctionResult(named.TypeArguments[0]);}
        for(var current=named;current is not null;current=current.BaseType){if(IsAsyncTypeDefinition(current)){return true;}}
        return named.AllInterfaces.Any(IsAsyncTypeDefinition);
    }

    private static bool IsAsyncTypeDefinition(INamedTypeSymbol type){
        var original=type.OriginalDefinition;
        if(original.ContainingAssembly?.Name is not("System.Private.CoreLib" or "System.Runtime" or "System.Threading.Tasks")){return false;}
        var ns=original.ContainingNamespace.ToDisplayString();
        return ns=="System.Threading.Tasks"&&(original.Name is "Task" or "ValueTask")&&original.Arity<=1
            ||ns=="System.Collections.Generic"&&(original.Name is "IAsyncEnumerable" or "IAsyncEnumerator")&&original.Arity==1;
    }

    private static bool HasImplicitUserReturnConversion(IOperation operation){
        if(operation is IAnonymousFunctionOperation or ILocalFunctionOperation){return false;}
        if(operation is IReturnOperation result){
            for(var value=result.ReturnedValue;value is IConversionOperation{IsImplicit:true} conversion;value=conversion.Operand){
                if(conversion.OperatorMethod is not null){return true;}
            }
        }
        return operation.ChildOperations.Any(HasImplicitUserReturnConversion);
    }

    private static ITypeSymbol? FunctionElement(ITypeSymbol type){
        if(type is IArrayTypeSymbol{Rank:1} array){return array.ElementType;}
        if(type is not INamedTypeSymbol named||named.SpecialType==SpecialType.System_String){return null;}
        var candidates=named.AllInterfaces.Prepend(named).Where(item=>item.OriginalDefinition.SpecialType==SpecialType.System_Collections_Generic_IEnumerable_T)
            .Select(item=>item.TypeArguments[0]).Distinct(SymbolEqualityComparer.Default).ToArray();
        if(candidates.Length>1){throw new ManagementException("Validation","関数結果に複数の列挙要素型があります。返す列挙型をC#で明示してください。");}
        if(candidates.Length==1){return(ITypeSymbol)candidates[0]!;}
        return null;
    }

    private static Definition SynchronizeFunctionResult(Definition definition,string schema,string name){
        if(definition.Kind!=DefinitionKind.Database){return definition;}
        var compilation=Compilation(definition);
        var methods=Owner(Container(compilation,definition),schema).GetMembers(name).OfType<IMethodSymbol>().ToArray();
        if(methods.Length!=1){return definition;}
        var result=InferFunctionResult(compilation,methods[0]);
        if(result.Reason=="AnonymousResultRequiresNamedType"){
            throw new ManagementException("Validation","匿名型の関数結果には、名前のある行型を定義してください。入力は保持しています。");
        }
        if(result.Type is null){return definition;}
        var element=FunctionElement(result.Type);
        var helperName="ScalarFunction";var resultType=result.Type;
        if(element is not null){helperName="TableFunction";resultType=element;}
        if(IsAsyncFunctionResult(resultType)){throw new ManagementException("Unsupported","非同期の戻り値を待機する実行は未対応です。同期的な値を返す処理を指定してください。入力は保持しています。");}
        var current=(INamedTypeSymbol)methods[0].ReturnType;
        if(current.Name==helperName&&TypeName(current.TypeArguments[0])==TypeName(resultType)&&Equals(current.TypeArguments[0].ContainingAssembly?.Identity,resultType.ContainingAssembly?.Identity)){return definition;}
        var generated=SyntaxFactory.GenericName(SyntaxFactory.Identifier(helperName),SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.ParseTypeName(TypeName(resultType)))));
        var syntax=result.Method!;
        var outerType=syntax.ReturnType;
        if(outerType is QualifiedNameSyntax qualified){outerType=qualified.Right;}
        if(outerType is AliasQualifiedNameSyntax alias){outerType=alias.Name;}
        var originalWrapper=outerType as GenericNameSyntax;
        TypeSyntax returnType;
        if(originalWrapper is not null){returnType=syntax.ReturnType.ReplaceNode(originalWrapper,RetypeQueryGeneric(originalWrapper,generated));}
        else{returnType=SyntaxFactory.ParseTypeName("global::Seamless.Database."+generated).WithTriviaFrom(syntax.ReturnType);}
        var access=(MemberAccessExpressionSyntax)result.Factory!.Expression;
        var factory=result.Factory.WithExpression(access.WithName(RetypeQueryGeneric(result.Helper!,generated)));
        var rewritten=syntax.ReplaceNode(result.Factory,factory).WithReturnType(returnType);
        var text=rewritten.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString();
        if(text==syntax.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()){return definition;}
        var source=new SourceEditor().Apply(definition.Source,new(definition.Source.Hash(),syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,text));
        var changed=definition with{Source=source};
        var reboundCompilation=Compilation(changed);
        var rebound=Owner(Container(reboundCompilation,changed),schema).GetMembers(name).OfType<IMethodSymbol>().Single();
        var reboundSyntax=(MethodDeclarationSyntax)rebound.DeclaringSyntaxReferences.Single().GetSyntax();
        var reboundCall=(InvocationExpressionSyntax)reboundSyntax.ExpressionBody!.Expression;
        var reboundFactory=reboundCompilation.GetSemanticModel(reboundCall.SyntaxTree).GetSymbolInfo(reboundCall).Symbol as IMethodSymbol;
        if(reboundFactory?.ContainingAssembly.Name!="Seamless.Database"||reboundFactory.Name!=helperName){throw new ManagementException("Validation","戻り型の変更で別の関数factoryが選ばれます。C#宣言と処理本体を確認してください。入力は保持しています。");}
        return changed;
    }
}
