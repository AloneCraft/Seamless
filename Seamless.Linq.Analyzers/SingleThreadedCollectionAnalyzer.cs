using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.Linq.Analyzers;

/// <summary>
/// シングル用のコレクション(Tsavorite の <c>Create()</c> で作る HashSet / Set / Dictionary、Database の Table)は、1 つのスレッドが順に使う前提で、
/// 複数のスレッドが同時に使うと結果が誤ったり例外になったりする(BUG-0016)。<c>Task.Run</c> や <c>Parallel.For</c> などに渡したラムダが、
/// ラムダの外のシングル用のコレクションを捕捉している形を警告する。スレッドごとに <c>BeginParallel().Create()</c> で作ること。
/// 見逃す形: インターフェイス型の変数、メソッド呼び出しをまたぐ共有、フィールドを経由した間接的な共有。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleThreadedCollectionAnalyzer:DiagnosticAnalyzer {
    private static readonly DiagnosticDescriptor CapturedRule=new(
        "SEAMTHREAD001",
        "シングル用のコレクションを別のスレッドで実行されるラムダが捕捉しています",
        "シングル用のコレクション '{0}' を、別のスレッドで実行される可能性のあるラムダが捕捉しています。同時に使うと結果が誤ります。スレッドごとに BeginParallel().Create() で作ってください",
        "Correctness",
        DiagnosticSeverity.Warning,
        true);

    private static readonly HashSet<string> ThreadLaunchingTypes=new(StringComparer.Ordinal){
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.TaskFactory",
        "System.Threading.Tasks.TaskFactory<TResult>",
        "System.Threading.Tasks.Parallel",
        "System.Threading.ThreadPool",
    };

    private static readonly HashSet<string> ThreadLaunchingMethods=new(StringComparer.Ordinal){
        "Run","StartNew","For","ForEach","ForAsync","ForEachAsync","Invoke","QueueUserWorkItem","UnsafeQueueUserWorkItem",
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics=>[CapturedRule];

    public override void Initialize(AnalysisContext context){
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation,OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeObjectCreation,OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context){
        var invocation=(IInvocationOperation)context.Operation;
        var method=invocation.TargetMethod;
        var containing=method.ContainingType?.OriginalDefinition.ToDisplayString();
        if(containing is null||!ThreadLaunchingTypes.Contains(containing)||!ThreadLaunchingMethods.Contains(method.Name)){
            return;
        }
        foreach(var argument in invocation.Arguments){
            AnalyzeArgument(context,argument.Value);
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context){
        var creation=(IObjectCreationOperation)context.Operation;
        if(creation.Constructor?.ContainingType?.ToDisplayString()!="System.Threading.Thread"){
            return;
        }
        foreach(var argument in creation.Arguments){
            AnalyzeArgument(context,argument.Value);
        }
    }

    private static void AnalyzeArgument(OperationAnalysisContext context,IOperation value){
        var lambda=Unwrap(value) as IAnonymousFunctionOperation;
        if(lambda is null){
            return;
        }
        var reported=new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach(var operation in Descendants(lambda.Body)){
            var symbol=GetCapturedSymbol(operation,lambda.Symbol);
            if(symbol is null||!reported.Add(symbol)){
                continue;
            }
            context.ReportDiagnostic(Diagnostic.Create(CapturedRule,operation.Syntax.GetLocation(),symbol.Name));
        }
    }

    // ラムダの外で宣言され、シングル用のコレクションの型を持つシンボルを参照していれば、そのシンボルを返す。
    private static ISymbol? GetCapturedSymbol(IOperation operation,IMethodSymbol lambda){
        switch(operation){
            case ILocalReferenceOperation local when IsSingleThreaded(local.Local.Type)&&!IsDeclaredInside(local.Local,lambda):
                return local.Local;
            case IParameterReferenceOperation parameter when IsSingleThreaded(parameter.Parameter.Type)&&!IsDeclaredInside(parameter.Parameter,lambda):
                return parameter.Parameter;
            case IFieldReferenceOperation field when IsSingleThreaded(field.Field.Type)&&!IsMemberOfOtherObject(field):
                return field.Field;
            case IPropertyReferenceOperation property when IsSingleThreaded(property.Property.Type)&&!IsMemberOfOtherObject(property):
                return property.Property;
            default:
                return null;
        }
    }

    // x.Items のように別のオブジェクトのメンバーなら対象にしない(this または静的なメンバーだけが、ラムダの外から共有されている)。
    private static bool IsMemberOfOtherObject(IMemberReferenceOperation member){
        return member.Instance is not null&&member.Instance is not IInstanceReferenceOperation;
    }

    private static bool IsDeclaredInside(ISymbol symbol,IMethodSymbol lambda){
        for(var current=symbol.ContainingSymbol;current is not null;current=current.ContainingSymbol){
            if(SymbolEqualityComparer.Default.Equals(current,lambda)){
                return true;
            }
        }
        return false;
    }

    private static bool IsSingleThreaded(ITypeSymbol? type){
        for(var current=type as INamedTypeSymbol;current is not null;current=current.BaseType){
            var definition=current.OriginalDefinition;
            var ns=definition.ContainingNamespace?.ToDisplayString();
            if(ns=="Seamless.Database"&&definition.Name=="Table"){
                return true;
            }
            if(ns=="Seamless.Collections.Tsavorite"&&definition.Name=="Collection"
                &&current.TypeArguments.Length>0&&current.TypeArguments[current.TypeArguments.Length-1].Name=="ExclusiveAccess"){
                return true;
            }
        }
        return false;
    }

    private static IOperation Unwrap(IOperation operation){
        // ラムダはデリゲートへの変換(IDelegateCreationOperation)で包まれて渡される。
        while(true){
            if(operation is IConversionOperation conversion){
                operation=conversion.Operand;
            }else if(operation is IDelegateCreationOperation delegateCreation){
                operation=delegateCreation.Target;
            }else{
                return operation;
            }
        }
    }

    private static IEnumerable<IOperation> Descendants(IOperation root){
        var stack=new Stack<IOperation>();
        stack.Push(root);
        while(stack.Count>0){
            var current=stack.Pop();
            yield return current;
            foreach(var child in current.ChildOperations){
                stack.Push(child);
            }
        }
    }
}
