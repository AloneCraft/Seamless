using System.Linq.Expressions;
using System.Reflection;
using SampleContainer = Test.Seamless.Database.Database0.Container;
using OptimizedSampleContainer = Test.Seamless.Database.Database0.Container;

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable2 {
    [Fact, Trait("Category","Unit")]
    public void QueryOverridesMatchOriginalMethods() {
        var container = new SampleContainer();
        var optimized = new OptimizedSampleContainer();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { "a","b" };

        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,string i)>>(container,"SelectMany_Query",[typeof(int),typeof(string)],outer,inner),
            optimized.SelectMany_Query<int,string>(outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectQuery",[typeof(int)],outer,2),
            optimized.SelectQuery<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectQuery",[typeof(int)],outer,2),
            optimized.WhereSelectQuery<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereQuery",[typeof(int)],outer,2),
            optimized.SelectWhereQuery<int>(outer,2));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"LetQuery",outer),
            optimized.LetQuery(outer));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinQuery",20),
            optimized.JoinQuery(20));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinIntoQuery",20),
            optimized.JoinIntoQuery(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"OrderByQuery"),
            optimized.OrderByQuery());
        Assert.Equal(
            this.Invoke<IEnumerable<(int key,int value)>>(container,"GroupQuery"),
            optimized.GroupQuery());
    }

    [Fact, Trait("Category","Unit")]
    public void QueryTreeOverridesUseSeamlessLinqAndMatchOriginalResults() {
        this.AssertSelectManyTreeOverride("SelectMany_QueryTree","SelectMany",nestedMethodName: null);
        this.AssertSelectManyTreeOverride("SelectMany_MethodTree","SelectMany",nestedMethodName: null);
    }

    [Fact, Trait("Category","Unit")]
    public void MethodChainOverridesMatchOriginalMethods() {
        var container = new SampleContainer();
        var optimized = new OptimizedSampleContainer();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { 10,20 };
        optimized.FusedJoinSelectSequence();

        Assert.Equal(container.Select_Method(outer),optimized.Select_Method(outer));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int i)>>(container,"SelectMany_Method",[typeof(int),typeof(int)],outer,inner),
            optimized.SelectMany_Method<int,int>(outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectMethod",[typeof(int)],outer,2),
            optimized.WhereSelectMethod<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereMethod",[typeof(int)],outer,2),
            optimized.SelectWhereMethod<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<int>(container,"AggregateMethod",[typeof(int)],outer),
            optimized.AggregateMethod<int>(outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedWhereSelectSum",outer),
            optimized.FusedWhereSelectSum(outer));
        Assert.Equal(
            this.Invoke<int>(container,"NonVirtualFusedWhereSelectSum",outer),
            container.NonVirtualFusedWhereSelectSum(outer));
        Assert.Equal(
            this.Invoke<double>(container,"FusedSkipTakeAverage",outer),
            optimized.FusedSkipTakeAverage(outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedSelectManyWhereSum",outer,inner),
            optimized.FusedSelectManyWhereSum(outer,inner));
        Assert.Equal(
            this.Invoke<int>(container,"FusedDistinctSkipWhileTakeWhileSum",new[] { -2,-1,1,2,2,3,5,4 }),
            optimized.FusedDistinctSkipWhileTakeWhileSum([-2,-1,1,2,2,3,5,4]));
        Assert.Equal(
            this.Invoke<int>(container,"FusedCastSum",(object)new object[] { 1,2,3 }),
            optimized.FusedCastSum([1,2,3]));
        Assert.Equal(
            this.Invoke<int>(container,"FusedOfTypeSum",(object)new object[] { 1,"skip",2,3.0,4 }),
            optimized.FusedOfTypeSum([1,"skip",2,3.0,4]));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"FusedJoinSelectSequence"),
            optimized.FusedJoinSelectSequence());
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerValue,int innerValue)>>(container,"FusedSelectManyHoistedSequence",outer,inner),
            optimized.FusedSelectManyHoistedSequence(outer,inner));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"FusedSelectManySplitWhereSequence",outer,inner,2),
            optimized.FusedSelectManySplitWhereSequence(outer,inner,2));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterAnonymousSelect",20),
            optimized.WhereAfterAnonymousSelect(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterTupleSelect",20),
            optimized.WhereAfterTupleSelect(20));
        Assert.Equal([20,30],optimized.WhereAfterJoin(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterExcept",20),
            optimized.WhereAfterExcept(20));
    }

    private void AssertSelectManyTreeOverride(string methodName,string outerMethodName,string? nestedMethodName) {
        var originalMethod = typeof(SampleContainer).GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public);
        Assert.NotNull(originalMethod);
        var optimizedMethod = typeof(OptimizedSampleContainer).GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public);
        Assert.NotNull(optimizedMethod);

        var container = new SampleContainer();
        var optimized = new OptimizedSampleContainer();
        var outer = new[] { 1,2,3 };
        var inner = new[] { 10,20 };

        var originalExpression = this.Invoke<Expression<Func<IEnumerable<int>>>>(container,methodName,outer,inner);
        var optimizedExpression = methodName switch {
            "SelectMany_QueryTree" => optimized.SelectMany_QueryTree(outer,inner),
            "SelectMany_MethodTree" => optimized.SelectMany_MethodTree(outer,inner),
            _ => throw new InvalidOperationException($"Unexpected method {methodName}.")
        };

        Assert.Equal(originalExpression.Compile()(),optimizedExpression.Compile()());

        var outerCall = Assert.IsAssignableFrom<MethodCallExpression>(optimizedExpression.Body);
        Assert.True(
            outerCall.Method.Name==outerMethodName||
            outerCall.Method.Name.StartsWith("OptimizedLinqInvocation_",StringComparison.Ordinal),
            $"Unexpected outer call: {outerCall.Method.DeclaringType?.FullName}.{outerCall.Method.Name}");
        Assert.True(
            outerCall.Method.DeclaringType?.FullName is "Seamless.Linq.Enumerable" or "System.Linq.Enumerable"||
            outerCall.Method.Name.StartsWith("OptimizedLinqInvocation_",StringComparison.Ordinal),
            $"Unexpected outer call declaring type: {outerCall.Method.DeclaringType?.FullName}");

        if(nestedMethodName is null) {
            return;
        }

        var nestedCall = Assert.IsAssignableFrom<MethodCallExpression>(outerCall.Arguments[0]);
        Assert.Equal(nestedMethodName,nestedCall.Method.Name);
        Assert.Equal("Seamless.Linq.Enumerable",nestedCall.Method.DeclaringType?.FullName);
    }

    private TResult Invoke<TResult>(object target,string methodName,params object[] arguments) {
        var method = target.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public)
            ??throw new InvalidOperationException($"Could not find method {methodName} on {target.GetType().FullName}.");

        return (TResult)method.Invoke(target,arguments)!;
    }

    private TResult InvokeGeneric<TResult>(object target,string methodName,Type[] typeArguments,params object[] arguments) {
        var method = target.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public)
            ??throw new InvalidOperationException($"Could not find method {methodName} on {target.GetType().FullName}.");

        return (TResult)method.MakeGenericMethod(typeArguments).Invoke(target,arguments)!;
    }
}
