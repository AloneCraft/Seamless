using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.Linq.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnorderedEnumerableAnalyzer:DiagnosticAnalyzer {
    private const string AttributeName="Seamless.Linq.UnorderedEnumerableAttribute";

    private static readonly DiagnosticDescriptor OrderDependentRule=new(
        "SEAMLINQ001",
        "順序なし列に順序依存演算は使用できません",
        "順序なし列に順序依存演算 '{0}' は使用できません。AsEnumerable() または IEnumerable<T> への明示的castで順序を受け入れてください",
        "Correctness",
        DiagnosticSeverity.Error,
        true,
        customTags:[WellKnownDiagnosticTags.NotConfigurable]);

    private static readonly DiagnosticDescriptor LostOriginRule=new(
        "SEAMLINQ002",
        "順序なし列の由来が失われます",
        "順序なし列を未注釈の境界 '{0}' へ渡すと由来が失われます。[UnorderedEnumerable] を付けるか明示的に AsEnumerable() を使用してください",
        "Correctness",
        DiagnosticSeverity.Error,
        true,
        customTags:[WellKnownDiagnosticTags.NotConfigurable]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics=>[OrderDependentRule,LostOriginRule];

    public override void Initialize(AnalysisContext context){
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation,OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeReturn,OperationKind.Return);
        context.RegisterOperationAction(AnalyzeAssignment,OperationKind.SimpleAssignment);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context){
        var invocation=(IInvocationOperation)context.Operation;
        var source=GetSource(invocation);
        if(source is not null&&IsUnordered(source,new HashSet<ISymbol>(SymbolEqualityComparer.Default))&&IsOrderDependent(invocation.TargetMethod)){
            context.ReportDiagnostic(Diagnostic.Create(OrderDependentRule,invocation.Syntax.GetLocation(),invocation.TargetMethod.Name));
        }

        if(IsTrustedSequenceOperator(invocation.TargetMethod)){
            return;
        }
        foreach(var argument in invocation.Arguments){
            if(argument.Parameter is null||HasAttribute(argument.Parameter)||IsExplicitOptIn(argument.Value)||!IsUnordered(argument.Value,new HashSet<ISymbol>(SymbolEqualityComparer.Default))){
                continue;
            }
            context.ReportDiagnostic(Diagnostic.Create(LostOriginRule,argument.Syntax.GetLocation(),argument.Parameter.Name));
        }
    }

    private static void AnalyzeReturn(OperationAnalysisContext context){
        var operation=(IReturnOperation)context.Operation;
        if(operation.ReturnedValue is null||IsExplicitOptIn(operation.ReturnedValue)||!IsUnordered(operation.ReturnedValue,new HashSet<ISymbol>(SymbolEqualityComparer.Default))){
            return;
        }
        if(context.ContainingSymbol is IMethodSymbol method&&!HasReturnAttribute(method)){
            context.ReportDiagnostic(Diagnostic.Create(LostOriginRule,operation.Syntax.GetLocation(),method.Name));
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context){
        var assignment=(ISimpleAssignmentOperation)context.Operation;
        if(IsExplicitOptIn(assignment.Value)||!IsUnordered(assignment.Value,new HashSet<ISymbol>(SymbolEqualityComparer.Default))){
            return;
        }
        var target=GetReferencedSymbol(assignment.Target);
        if(target is IFieldSymbol or IPropertySymbol&& !HasAttribute(target)){
            context.ReportDiagnostic(Diagnostic.Create(LostOriginRule,assignment.Syntax.GetLocation(),target.Name));
        }
    }

    private static bool IsUnordered(IOperation operation,HashSet<ISymbol> visiting){
        operation=UnwrapImplicit(operation);
        if(IsExplicitOptIn(operation)){
            return false;
        }
        if(IsKnownUnorderedType(operation.Type)){
            return true;
        }
        switch(operation){
            case IParameterReferenceOperation parameter:
                return HasAttribute(parameter.Parameter);
            case IFieldReferenceOperation field:
                return HasAttribute(field.Field);
            case IPropertyReferenceOperation property:
                return HasAttribute(property.Property);
            case ILocalReferenceOperation local:
                return IsUnorderedLocal(local,visiting);
            case IInvocationOperation invocation:
                if(HasReturnAttribute(invocation.TargetMethod)){
                    return true;
                }
                if(IsSequenceType(invocation.Type)&&IsTrustedSequenceOperator(invocation.TargetMethod)&&GetSource(invocation) is { } source){
                    return IsUnordered(source,visiting);
                }
                return false;
            case IConditionalOperation conditional:
                return IsUnordered(conditional.WhenTrue,visiting)||conditional.WhenFalse is { } whenFalse&&IsUnordered(whenFalse,visiting);
            case ICoalesceOperation coalesce:
                return IsUnordered(coalesce.Value,visiting)||IsUnordered(coalesce.WhenNull,visiting);
            default:
                return false;
        }
    }

    private static bool IsUnorderedLocal(ILocalReferenceOperation reference,HashSet<ISymbol> visiting){
        if(!visiting.Add(reference.Local)){
            return false;
        }
        try{
            var model=reference.SemanticModel;
            if(model is null){
                return false;
            }
            foreach(var syntaxReference in reference.Local.DeclaringSyntaxReferences){
                var syntax=syntaxReference.GetSyntax();
                var operation=model.GetOperation(syntax);
                if(operation is IVariableDeclaratorOperation declarator&&declarator.Initializer is { } initializer&&IsUnordered(initializer.Value,visiting)){
                    return true;
                }
            }
            foreach(var syntax in reference.Syntax.SyntaxTree.GetRoot().DescendantNodes()){
                if(syntax.SpanStart>=reference.Syntax.SpanStart){
                    continue;
                }
                if(model.GetOperation(syntax) is ISimpleAssignmentOperation assignment&&GetReferencedSymbol(assignment.Target) is { } symbol&&SymbolEqualityComparer.Default.Equals(symbol,reference.Local)&&IsUnordered(assignment.Value,visiting)){
                    return true;
                }
            }
            return false;
        }finally{
            visiting.Remove(reference.Local);
        }
    }

    private static IEnumerable<IOperation> Descendants(IOperation root){
        var stack=new Stack<IOperation>();
        stack.Push(root);
        while(stack.Count!=0){
            var current=stack.Pop();
            yield return current;
            foreach(var child in current.ChildOperations){
                stack.Push(child);
            }
        }
    }

    private static IOperation? GetSource(IInvocationOperation invocation){
        if(invocation.Instance is not null){
            return invocation.Instance;
        }
        return invocation.Arguments.FirstOrDefault()?.Value;
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation){
        operation=UnwrapImplicit(operation);
        return operation switch{
            ILocalReferenceOperation local=>local.Local,
            IFieldReferenceOperation field=>field.Field,
            IPropertyReferenceOperation property=>property.Property,
            IParameterReferenceOperation parameter=>parameter.Parameter,
            _=>null,
        };
    }

    private static IOperation UnwrapImplicit(IOperation operation){
        while(operation is IConversionOperation { IsImplicit:true } conversion){
            operation=conversion.Operand;
        }
        return operation;
    }

    private static bool IsExplicitOptIn(IOperation operation){
        if(operation is IConversionOperation { IsImplicit:false } conversion&&IsIEnumerable(conversion.Type)){
            return true;
        }
        if(operation is IInvocationOperation invocation&&invocation.TargetMethod.Name=="AsEnumerable"){
            return true;
        }
        return false;
    }

    private static bool IsOrderDependent(IMethodSymbol method){
        var name=method.Name;
        if(name.StartsWith("Order",StringComparison.Ordinal)||name.StartsWith("ThenBy",StringComparison.Ordinal)||name.StartsWith("First",StringComparison.Ordinal)||name.StartsWith("Last",StringComparison.Ordinal)||name.StartsWith("ElementAt",StringComparison.Ordinal)||name.StartsWith("Skip",StringComparison.Ordinal)||name.StartsWith("Take",StringComparison.Ordinal)){
            return true;
        }
        if(name is "Reverse" or "Shuffle" or "Index" or "Zip" or "SequenceEqual" or "Chunk" or "Append" or "Prepend" or "Concat"){
            return true;
        }
        return name is "Where" or "Select" or "SelectMany"&&HasIndexedDelegate(method);
    }

    private static bool HasIndexedDelegate(IMethodSymbol method){
        foreach(var parameter in method.Parameters){
            if(parameter.Type is INamedTypeSymbol { DelegateInvokeMethod:{ } invoke }&&invoke.Parameters.Length>=2&&invoke.Parameters[1].Type.SpecialType==SpecialType.System_Int32){
                return true;
            }
        }
        return false;
    }

    private static bool IsTrustedSequenceOperator(IMethodSymbol method){
        var type=method.ContainingType?.ToDisplayString();
        return type is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable"||type?.StartsWith("ZLinq.",StringComparison.Ordinal)==true;
    }

    private static bool IsKnownUnorderedType(ITypeSymbol? type){
        if(type is null){
            return false;
        }
        var original=(type as INamedTypeSymbol)?.OriginalDefinition.ToDisplayString()??type.ToDisplayString();
        if(original is "Seamless.Persisted.Collections.Set<T>" or "Seamless.Tsavorite.Collections.HashSet<T>" or "Seamless.Tsavorite.Collections.Set<TKey, TRecord>" or "Seamless.Collections.ISet<T>" or "Seamless.Collections.IUnorderedEnumerable<T>"){
            return true;
        }
        foreach(var interfaceType in type.AllInterfaces){
            var name=interfaceType.OriginalDefinition.ToDisplayString();
            if(name is "Seamless.Collections.ISet<T>" or "Seamless.Collections.IUnorderedEnumerable<T>"){
                return true;
            }
        }
        return false;
    }

    private static bool IsSequenceType(ITypeSymbol? type){
        if(type is null){
            return false;
        }
        if(IsIEnumerable(type)||type.AllInterfaces.Any(IsIEnumerable)){
            return true;
        }
        var name=(type as INamedTypeSymbol)?.OriginalDefinition.ToDisplayString()??type.ToDisplayString();
        return name.StartsWith("Seamless.Linq.ValueEnumerable<",StringComparison.Ordinal)||name.StartsWith("ZLinq.ValueEnumerable<",StringComparison.Ordinal);
    }

    private static bool IsIEnumerable(ITypeSymbol? type)=>
        type is INamedTypeSymbol named&&named.OriginalDefinition.SpecialType==SpecialType.System_Collections_Generic_IEnumerable_T;

    private static bool HasReturnAttribute(IMethodSymbol method)=>
        method.GetReturnTypeAttributes().Any(IsAttribute);

    private static bool HasAttribute(ISymbol symbol)=>symbol.GetAttributes().Any(IsAttribute);

    private static bool IsAttribute(AttributeData attribute)=>attribute.AttributeClass?.ToDisplayString()==AttributeName;
}
