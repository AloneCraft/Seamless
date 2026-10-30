using Benchmark.Dynamic.Core;
using System.Runtime.CompilerServices;

namespace Benchmark.Dynamic.Worker;

public sealed record DynamicQueryInvocationFailure(string Type,string Message,string? StackTrace);

public sealed record DynamicQueryValidationResult(
    WorkerExecutionStatus Status,
    IReadOnlyDictionary<DynamicImplementationKind,DynamicQueryValue> Results,
    IReadOnlyDictionary<DynamicImplementationKind,DynamicQueryInvocationFailure> Exceptions,
    IReadOnlyDictionary<DynamicImplementationKind,DynamicQueryComparison> Comparisons,
    IReadOnlyDictionary<DynamicImplementationKind,long> Fallbacks){
    public bool ShouldMeasure=>this.Status==WorkerExecutionStatus.Succeeded&&this.Exceptions.Count==0;
}

/// <summary>動的クエリの意味を System.Linq と比較し、測定できる正常実行かを判定する。</summary>
public static class DynamicQueryValidator{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DynamicQueryValidationResult Validate(
        LoadedDynamicQuery loaded,
        object? primary,
        object? secondary,
        IReadOnlyList<DynamicImplementationKind> implementations,
        bool expectExceptions,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(implementations);
        cancellationToken.ThrowIfCancellationRequested();
        if(!implementations.Contains(DynamicImplementationKind.System)||!loaded.Has(DynamicImplementationKind.System)){
            throw new ArgumentException("Validation requires the System implementation.",nameof(implementations));
        }
        var results=new Dictionary<DynamicImplementationKind,DynamicQueryValue>();
        var exceptions=new Dictionary<DynamicImplementationKind,DynamicQueryInvocationFailure>();
        var fallbacks=new Dictionary<DynamicImplementationKind,long>();
        var comparisons=new Dictionary<DynamicImplementationKind,DynamicQueryComparison>();
        foreach(var implementation in implementations.Where(loaded.Has)){
            cancellationToken.ThrowIfCancellationRequested();
            try{
                using var scope=global::Seamless.Linq.LinqCounterScope.Start();
                var value=loaded.Run(implementation,primary,secondary);
                value.Validate();
                results[implementation]=value;
                // 例外経路より後で検査する既存 Worker 契約を維持する。
                // Run/Validate が投げたときは、この実装の fallback 数を記録しない。
                if(implementation is DynamicImplementationKind.FLinqUnoptimized or DynamicImplementationKind.FLinq
                    &&scope.Counter.EnumerableFallback>0){
                    fallbacks[implementation]=scope.Counter.EnumerableFallback;
                }
            }catch(Exception exception) when(exception is not OperationCanceledException){
                exceptions[implementation]=new(exception.GetType().FullName??exception.GetType().Name,exception.Message,exception.StackTrace);
            }
        }
        if(exceptions.Count!=0&&!expectExceptions){
            return new(WorkerExecutionStatus.ExecutionFailed,results,exceptions,comparisons,fallbacks);
        }
        exceptions.TryGetValue(DynamicImplementationKind.System,out var expectedException);
        foreach(var implementation in implementations.Where(implementation=>implementation!=DynamicImplementationKind.System&&loaded.Has(implementation))){
            exceptions.TryGetValue(implementation,out var actualException);
            comparisons[implementation]=(expectedException,actualException) switch{
                (null,null)=>DynamicQueryComparer.Compare(results[DynamicImplementationKind.System],results[implementation]),
                ({ }expected,{ }actual) when expected.Type==actual.Type=>new(true),
                ({ }expected,{ }actual)=>new(false,$"System threw {expected.Type} but {implementation} threw {actual.Type}: {actual.Message}"),
                ({ }expected,null)=>new(false,$"System threw {expected.Type} but {implementation} returned a value."),
                (null,{ }actual)=>new(false,$"System returned a value but {implementation} threw {actual.Type}: {actual.Message}"),
            };
        }
        var status=WorkerExecutionStatus.Succeeded;
        if(comparisons.Values.Any(static comparison=>!comparison.IsMatch)){
            status=WorkerExecutionStatus.SemanticMismatch;
        }else if(exceptions.Count==0&&fallbacks.Count!=0){
            status=WorkerExecutionStatus.ExpansionIncomplete;
        }
        return new(status,results,exceptions,comparisons,fallbacks);
    }
}
