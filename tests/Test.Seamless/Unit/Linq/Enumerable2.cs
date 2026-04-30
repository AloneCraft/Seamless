using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable2 {
    private static readonly Type SampleContainerType = ResolveType(
        "Test.Seamless.Database.Database0.Container",
        "Test.Seamless.Benchmarks.Simple2.Container");

    private static readonly Type SampleOptimizedContainerType = ResolveType(
        "Test.Seamless.Database.Database0.OptimizedContainer",
        "Test.Seamless.Benchmarks.Simple2.OptimizedContainer");

    [Fact, Trait("Category","Unit")]
    public void QueryOverridesMatchOriginalMethods() {
        var container = CreateInstance(SampleContainerType);
        var optimized = CreateInstance(SampleOptimizedContainerType);
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { "a","b" };

        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,string i)>>(container,"SelectMany_Query",[typeof(int),typeof(string)],outer,inner),
            this.InvokeGeneric<IEnumerable<(int o,string i)>>(optimized,"SelectMany_Query",[typeof(int),typeof(string)],outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectQuery",[typeof(int)],outer,2),
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(optimized,"SelectQuery",[typeof(int)],outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectQuery",[typeof(int)],outer,2),
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(optimized,"WhereSelectQuery",[typeof(int)],outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereQuery",[typeof(int)],outer,2),
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(optimized,"SelectWhereQuery",[typeof(int)],outer,2));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"LetQuery",outer),
            this.Invoke<IEnumerable<int>>(optimized,"LetQuery",outer));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinQuery",20),
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(optimized,"JoinQuery",20));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinIntoQuery",20),
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(optimized,"JoinIntoQuery",20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"OrderByQuery"),
            this.Invoke<IEnumerable<int>>(optimized,"OrderByQuery"));
        Assert.Equal(
            this.Invoke<IEnumerable<(int key,int value)>>(container,"GroupQuery"),
            this.Invoke<IEnumerable<(int key,int value)>>(optimized,"GroupQuery"));
    }

    [Fact, Trait("Category","Unit")]
    public void QueryTreeOverridesUseSeamlessLinqAndMatchOriginalResults() {
        this.AssertSelectManyTreeOverride("SelectMany_QueryTree","Select","SelectMany");
        this.AssertSelectManyTreeOverride("SelectMany_MethodTree","SelectMany",nestedMethodName: null);
    }

    [Fact, Trait("Category","Unit")]
    public void MethodChainOverridesMatchOriginalMethods() {
        var container = CreateInstance(SampleContainerType);
        var optimized = CreateInstance(SampleOptimizedContainerType);
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { 10,20 };

        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int i)>>(container,"SelectMany_Method",[typeof(int),typeof(int)],outer,inner),
            this.InvokeGeneric<IEnumerable<(int o,int i)>>(optimized,"SelectMany_Method",[typeof(int),typeof(int)],outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectMethod",[typeof(int)],outer,2),
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(optimized,"WhereSelectMethod",[typeof(int)],outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereMethod",[typeof(int)],outer,2),
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(optimized,"SelectWhereMethod",[typeof(int)],outer,2));
        Assert.Equal(
            this.InvokeGeneric<int>(container,"AggregateMethod",[typeof(int)],outer),
            this.InvokeGeneric<int>(optimized,"AggregateMethod",[typeof(int)],outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedWhereSelectSum",outer),
            this.Invoke<int>(optimized,"FusedWhereSelectSum",outer));
        Assert.Equal(
            this.Invoke<double>(container,"FusedSkipTakeAverage",outer),
            this.Invoke<double>(optimized,"FusedSkipTakeAverage",outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedSelectManyWhereSum",outer,inner),
            this.Invoke<int>(optimized,"FusedSelectManyWhereSum",outer,inner));
        Assert.Equal(
            this.Invoke<int>(container,"FusedDistinctSkipWhileTakeWhileSum",new[] { -2,-1,1,2,2,3,5,4 }),
            this.Invoke<int>(optimized,"FusedDistinctSkipWhileTakeWhileSum",new[] { -2,-1,1,2,2,3,5,4 }));
        Assert.Equal(
            this.Invoke<int>(container,"FusedCastSum",(object)new object[] { 1,2,3 }),
            this.Invoke<int>(optimized,"FusedCastSum",(object)new object[] { 1,2,3 }));
        Assert.Equal(
            this.Invoke<int>(container,"FusedOfTypeSum",(object)new object[] { 1,"skip",2,3.0,4 }),
            this.Invoke<int>(optimized,"FusedOfTypeSum",(object)new object[] { 1,"skip",2,3.0,4 }));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterAnonymousSelect",20),
            this.Invoke<IEnumerable<int>>(optimized,"WhereAfterAnonymousSelect",20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterTupleSelect",20),
            this.Invoke<IEnumerable<int>>(optimized,"WhereAfterTupleSelect",20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterJoin",20),
            this.Invoke<IEnumerable<int>>(optimized,"WhereAfterJoin",20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterExcept",20),
            this.Invoke<IEnumerable<int>>(optimized,"WhereAfterExcept",20));
    }

    private void AssertSelectManyTreeOverride(string methodName,string outerMethodName,string? nestedMethodName) {
        var originalMethod = SampleContainerType.GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public);
        Assert.NotNull(originalMethod);

        var optimizedMethod = SampleOptimizedContainerType.GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public);
        Assert.NotNull(optimizedMethod);

        var container = CreateInstance(SampleContainerType);
        var optimized = CreateInstance(SampleOptimizedContainerType);
        var outer = new[] { 1,2,3 };
        var inner = new[] { 10,20 };

        var originalExpression = this.Invoke<Expression<Func<IEnumerable<int>>>>(container,methodName,outer,inner);
        var optimizedExpression = this.Invoke<Expression<Func<IEnumerable<int>>>>(optimized,methodName,outer,inner);

        Assert.Equal(originalExpression.Compile()(),optimizedExpression.Compile()());

        var outerCall = Assert.IsAssignableFrom<MethodCallExpression>(optimizedExpression.Body);
        Assert.Equal(outerMethodName,outerCall.Method.Name);
        Assert.Equal("Seamless.Linq.Enumerable",outerCall.Method.DeclaringType?.FullName);

        if(nestedMethodName is null) {
            return;
        }

        var nestedCall = Assert.IsAssignableFrom<MethodCallExpression>(outerCall.Arguments[0]);
        Assert.Equal(nestedMethodName,nestedCall.Method.Name);
        Assert.Equal("Seamless.Linq.Enumerable",nestedCall.Method.DeclaringType?.FullName);
    }

    private static object CreateInstance(Type type) {
        return Activator.CreateInstance(type)
            ??throw new InvalidOperationException($"Could not create {type.FullName}.");
    }

    private static Type ResolveType(params string[] typeNames) {
        foreach(var typeName in typeNames) {
            var type = typeof(Enumerable2).Assembly.GetType(typeName,throwOnError: false);
            if(type is not null) {
                return type;
            }
        }

        throw new InvalidOperationException($"Could not resolve any of: {string.Join(", ",typeNames)}");
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
