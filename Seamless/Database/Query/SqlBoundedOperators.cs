using Seamless.Execution;
using Seamless.Linq;
namespace Seamless.Database.Query;
internal static class SqlBoundedOperators{
    internal static IEnumerable<T>Sort<T>(QueryExecution execution,IEnumerable<T>source,Func<T,object?[]>selector,IComparer<object?[]>comparer){
        var options=execution.Options??new ExecutionOptions();
        using var scope=new ExecutionScope(options with{ParentBudget=execution.Scope.Budget,MemoryLimitBytes=execution.Scope.Budget.MemoryLimitBytes,TemporaryStorageLimitBytes=execution.Scope.Budget.TemporaryStorageLimitBytes,CancellationToken=execution.Scope.CancellationToken});
        scope.Codecs.Register<object?[]>(new SqlRowCodec());
        foreach(var value in scope.Query(source).OrderBy(selector,comparer)){yield return value;}
    }
}
