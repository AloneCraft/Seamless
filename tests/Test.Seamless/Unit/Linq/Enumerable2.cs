using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using SampleContainer = Test.Seamless.Database.Database0.Container;

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable2 {
    [Fact, Trait("Category","Unit")]
    public void QueryInterceptorsMatchOriginalMethods() {
        var container = new SampleContainer();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { "a","b" };

        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,string i)>>(container,"SelectMany_Query",[typeof(int),typeof(string)],outer,inner),
            container.SelectMany_Query<int,string>(outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectQuery",[typeof(int)],outer,2),
            container.SelectQuery<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectQuery",[typeof(int)],outer,2),
            container.WhereSelectQuery<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereQuery",[typeof(int)],outer,2),
            container.SelectWhereQuery<int>(outer,2));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"LetQuery",outer),
            container.LetQuery(outer));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinQuery",20),
            container.JoinQuery(20));
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerKey,int innerValue)>>(container,"JoinIntoQuery",20),
            container.JoinIntoQuery(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"OrderByQuery"),
            container.OrderByQuery());
        Assert.Equal(
            this.Invoke<IEnumerable<(int key,int value)>>(container,"GroupQuery"),
            container.GroupQuery());
    }

    [Fact, Trait("Category","Unit")]
    public void QueryTreeInterceptorsUseSeamlessLinqAndMatchOriginalResults() {
        this.AssertSelectManyTreeInterceptor("SelectMany_QueryTree","Select","SelectMany");
        this.AssertSelectManyTreeInterceptor("SelectMany_MethodTree","SelectMany",nestedMethodName: null);
    }

    [Fact, Trait("Category","Unit")]
    public void MethodChainInterceptorsMatchOriginalMethods() {
        var container = new SampleContainer();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { 10,20 };

        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int i)>>(container,"SelectMany_Method",[typeof(int),typeof(int)],outer,inner),
            container.SelectMany_Method<int,int>(outer,inner));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"WhereSelectMethod",[typeof(int)],outer,2),
            container.WhereSelectMethod<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<IEnumerable<(int o,int p)>>(container,"SelectWhereMethod",[typeof(int)],outer,2),
            container.SelectWhereMethod<int>(outer,2));
        Assert.Equal(
            this.InvokeGeneric<int>(container,"AggregateMethod",[typeof(int)],outer),
            container.AggregateMethod<int>(outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedWhereSelectSum",outer),
            container.FusedWhereSelectSum(outer));
        Assert.Equal(
            this.Invoke<int>(container,"NonVirtualFusedWhereSelectSum",outer),
            container.NonVirtualFusedWhereSelectSum(outer));
        Assert.Equal(
            this.Invoke<double>(container,"FusedSkipTakeAverage",outer),
            container.FusedSkipTakeAverage(outer));
        Assert.Equal(
            this.Invoke<int>(container,"FusedSelectManyWhereSum",outer,inner),
            container.FusedSelectManyWhereSum(outer,inner));
        Assert.Equal(
            this.Invoke<int>(container,"FusedDistinctSkipWhileTakeWhileSum",new[] { -2,-1,1,2,2,3,5,4 }),
            container.FusedDistinctSkipWhileTakeWhileSum(new[] { -2,-1,1,2,2,3,5,4 }));
        Assert.Equal(
            this.Invoke<int>(container,"FusedCastSum",(object)new object[] { 1,2,3 }),
            container.FusedCastSum(new object[] { 1,2,3 }));
        Assert.Equal(
            this.Invoke<int>(container,"FusedOfTypeSum",(object)new object[] { 1,"skip",2,3.0,4 }),
            container.FusedOfTypeSum(new object[] { 1,"skip",2,3.0,4 }));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"FusedJoinSelectSequence"),
            container.FusedJoinSelectSequence());
        Assert.Equal(
            this.Invoke<IEnumerable<(int outerValue,int innerValue)>>(container,"FusedSelectManyHoistedSequence",outer,inner),
            container.FusedSelectManyHoistedSequence(outer,inner));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"FusedSelectManySplitWhereSequence",outer,inner,2),
            container.FusedSelectManySplitWhereSequence(outer,inner,2));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterAnonymousSelect",20),
            container.WhereAfterAnonymousSelect(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterTupleSelect",20),
            container.WhereAfterTupleSelect(20));
        Assert.Equal(new[] { 20,30 },container.WhereAfterJoin(20));
        Assert.Equal(
            this.Invoke<IEnumerable<int>>(container,"WhereAfterExcept",20),
            container.WhereAfterExcept(20));
    }

    private void AssertSelectManyTreeInterceptor(string methodName,string outerMethodName,string? nestedMethodName) {
        var originalMethod = typeof(SampleContainer).GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public);
        Assert.NotNull(originalMethod);

        var container = new SampleContainer();
        var outer = new[] { 1,2,3 };
        var inner = new[] { 10,20 };

        var originalExpression = this.Invoke<Expression<Func<IEnumerable<int>>>>(container,methodName,outer,inner);
        var optimizedExpression = methodName switch {
            "SelectMany_QueryTree" => container.SelectMany_QueryTree(outer,inner),
            "SelectMany_MethodTree" => container.SelectMany_MethodTree(outer,inner),
            _ => throw new InvalidOperationException($"Unexpected method {methodName}.")
        };

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
