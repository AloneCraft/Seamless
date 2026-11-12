using System.Reflection;

namespace Management.Tests;

public sealed class NullableSumSemantics{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void NullableSumDoesNotApplyCheckedContextToTheSelector(bool directArray){
        var inputType=directArray?"int?[]":"System.Collections.Generic.IEnumerable<int?>";
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static int? Compute("+inputType+" values)=>values.Select(static value=>value+1).Sum();public static int? Run("+inputType+" values)=>Compute(values);}";
        var generate=typeof(QueryProfiler).GetMethod("Generate",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var program=(IDisposable)generate.Invoke(null,new object?[]{"value=>value>1",false,false,source})!;
        var invoke=program.GetType().GetMethod("InvokeArgument",BindingFlags.NonPublic|BindingFlags.Instance)!;
        int?[] input=[int.MaxValue];
        Assert.Equal(input.Select(static value=>value+1).Sum(),(int?)invoke.Invoke(program,new object[]{input}));
    }
    [Theory]
    [InlineData(false,false)][InlineData(false,true)]
    [InlineData(true,false)][InlineData(true,true)]
    public void NullableIntegerWhereSumThrowsOnOverflow(bool useLong,bool directArray){
        var elementType=useLong?"long?":"int?";
        var inputType=directArray?elementType+"[]":"System.Collections.Generic.IEnumerable<"+elementType+">";
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static "+elementType+" Compute("+inputType+" values)=>values.Where(static value=>true).Sum();public static "+elementType+" Run("+inputType+" values)=>Compute(values);}";
        var generate=typeof(QueryProfiler).GetMethod("Generate",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var program=(IDisposable)generate.Invoke(null,new object?[]{"value=>value>1",false,false,source})!;
        var invoke=program.GetType().GetMethod("InvokeArgument",BindingFlags.NonPublic|BindingFlags.Instance)!;
        object input=useLong?new long?[]{long.MaxValue,1}:new int?[]{int.MaxValue,1};
        var error=Record.Exception(()=>invoke.Invoke(program,new[]{input}));
        Assert.NotNull(error);
        Assert.IsType<OverflowException>(error.GetBaseException());
    }
    [Theory]
    [InlineData("Concat",false)][InlineData("Concat",true)]
    [InlineData("Distinct",false)][InlineData("Distinct",true)]
    [InlineData("Union",false)][InlineData("Union",true)]
    [InlineData("Except",false)][InlineData("Except",true)]
    [InlineData("Intersect",false)][InlineData("Intersect",true)]
    [InlineData("Prepend",false)][InlineData("Prepend",true)]
    [InlineData("Append",false)][InlineData("Append",true)]
    [InlineData("DefaultIfEmpty",false)][InlineData("DefaultIfEmpty",true)]
    public void NullableSetAndConcatenationSumMatchesStandardLinq(string operation,bool directArray){
        var query=operation switch{
            "Concat"=>"values.Concat(values)",
            "Distinct"=>"values.Distinct()",
            "Union"=>"values.Union(values)",
            "Except"=>"values.Except(new decimal?[]{4m})",
            "Prepend"=>"values.Prepend(null)",
            "Append"=>"values.Append(null)",
            "DefaultIfEmpty"=>"values.DefaultIfEmpty()",
            _=>"values.Intersect(new decimal?[]{null,4m})"
        };
        var inputType=directArray?"decimal?[]":"System.Collections.Generic.IEnumerable<decimal?>";
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static decimal? Compute("+inputType+" values)=>"+query+".Sum();public static decimal? Run("+inputType+" values)=>Compute(values);}";
        var generate=typeof(QueryProfiler).GetMethod("Generate",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var program=(IDisposable)generate.Invoke(null,new object?[]{"value=>value>1",false,false,source})!;
        var invoke=program.GetType().GetMethod("InvokeArgument",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(var input in new decimal?[][]{[],[null,null],[1m,null,4m,4m]}){
            IEnumerable<decimal?> expected=operation switch{
                "Concat"=>input.Concat(input),
                "Distinct"=>input.Distinct(),
                "Union"=>input.Union(input),
                "Except"=>input.Except(new decimal?[]{4m}),
                "Prepend"=>input.Prepend(null),
                "Append"=>input.Append(null),
                "DefaultIfEmpty"=>input.DefaultIfEmpty(),
                _=>input.Intersect(new decimal?[]{null,4m})
            };
            Assert.Equal(expected.Sum(),(decimal?)invoke.Invoke(program,new object[]{input}));
        }
    }
    [Theory]
    [InlineData("values.Where(static value=>true).Sum()")]
    [InlineData("values.Where(static value=>value.HasValue).Sum()")]
    public void NullableWhereSumMatchesStandardLinq(string expression){
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static decimal? Compute(System.Collections.Generic.IEnumerable<decimal?> values)=>"+expression+";public static decimal? Run(System.Collections.Generic.IEnumerable<decimal?> values)=>Compute(values);}";
        var generate=typeof(QueryProfiler).GetMethod("Generate",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var program=(IDisposable)generate.Invoke(null,new object?[]{"value=>value>1",false,false,source})!;
        var invoke=program.GetType().GetMethod("InvokeArgument",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(var input in new decimal?[][]{[],[null,null],[1m,null,4m]}){
            Assert.Equal(input.Sum(),(decimal?)invoke.Invoke(program,new object[]{input}));
        }
    }
    [Theory][InlineData(false,"Array")][InlineData(false,"List")][InlineData(false,"Opaque")][InlineData(false,"DirectArray")][InlineData(false,"DirectList")][InlineData(true,"Array")][InlineData(true,"List")][InlineData(true,"Opaque")][InlineData(true,"DirectArray")][InlineData(true,"DirectList")]
    public void OrdinaryNullableSumPreservesTheStandardLinqResultWithoutStartingAProfiler(bool selector,string storage){
        var expression="values.Sum()";if(selector){expression="values.Sum(static value=>ProfileQuery.Visit(value))";}
        var inputType="System.Collections.Generic.IEnumerable<decimal?>";if(storage=="DirectArray"){inputType="decimal?[]";}else if(storage=="DirectList"){inputType="System.Collections.Generic.List<decimal?>";}
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{public static int Calls;public static decimal? Visit(decimal? value){Calls++;return value;}[LinqIntercept(true)] private static decimal? Compute("+inputType+" values)=>"+expression+";public static decimal? Run("+inputType+" values)=>Compute(values);}";
        var generate=typeof(QueryProfiler).GetMethod("Generate",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var program=(IDisposable)generate.Invoke(null,new object?[]{"value=>value>1",false,false,source})!;
        var invoke=program.GetType().GetMethod("InvokeArgument",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var assembly=(Assembly)program.GetType().GetField("assembly",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(program)!;
        var calls=assembly.GetType("ProfileQuery",true)!.GetField("Calls")!;
        Assert.Null(Seamless.Execution.QueryProfiler.Current);
        foreach(var sample in new decimal?[][]{[],[null,null],[1m,null,4m],[1m,4m],[decimal.MaxValue,1m]}){
            IEnumerable<decimal?> input=sample;if(storage is "List" or "DirectList"){input=sample.ToList();}else if(storage=="Opaque"){input=Opaque(sample);}
            decimal? expected=null;var expectedCalls=0;
            var expectedError=Record.Exception(()=>{if(selector){expected=System.Linq.Enumerable.Sum(input,value=>{expectedCalls++;return value;});}else{expected=System.Linq.Enumerable.Sum(input);}});
            calls.SetValue(null,0);decimal? actual=null;
            var actualError=Record.Exception(()=>{actual=(decimal?)invoke.Invoke(program,new object[]{input});});
            if(expectedError is null){Assert.Null(actualError);Assert.Equal(expected,actual);}
            else{Assert.IsType<OverflowException>(expectedError);Assert.NotNull(actualError);Assert.IsType<OverflowException>(actualError.GetBaseException());}
            Assert.Equal(expectedCalls,(int)calls.GetValue(null)!);
        }
    }
    private static IEnumerable<decimal?> Opaque(IEnumerable<decimal?> values){foreach(var value in values){yield return value;}}
}
