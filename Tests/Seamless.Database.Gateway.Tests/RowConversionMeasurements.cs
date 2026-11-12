using System.Diagnostics;
using Seamless.Database.Query;
using Xunit.Abstractions;

namespace Seamless.Database.Gateway.Tests;

public sealed class RowConversionMeasurements(ITestOutputHelper output){
    public readonly record struct Source(int Id,int Value);
    public readonly record struct Target(long Id,int Value);

    [Fact]
    public void CompareTypedDynamicAndCachedReflectionWithoutAssumingWinner(){
        const int count=200_000;var plan=new MigrationPlan(1,[new(1,"Id",SqlType.Int,false),new(2,"Value",SqlType.Int,false)],[new(1,"Id",SqlType.BigInt,false),new(2,"Value",SqlType.Int,false)]);
        var converter=Seamless.Database.Query.RowConverterCompiler.Compile<Source,Target>(plan);var context=new MigrationContext();
        var id=typeof(Source).GetProperty(nameof(Source.Id))!;var value=typeof(Source).GetProperty(nameof(Source.Value))!;var constructor=typeof(Target).GetConstructor([typeof(long),typeof(int)])!;
        long Typed(){long sum=0;for(var index=0;index<count;index++){var source=new Source(index,index+1);var target=new Target(source.Id,source.Value);sum+=target.Id+target.Value;}return sum;}
        long Dynamic(){long sum=0;for(var index=0;index<count;index++){var source=new Source(index,index+1);var target=converter(in source,context);sum+=target.Id+target.Value;}return sum;}
        long Reflection(){long sum=0;var arguments=new object?[2];for(var index=0;index<count;index++){object source=new Source(index,index+1);arguments[0]=(long)(int)id.GetValue(source)!;arguments[1]=value.GetValue(source);var target=(Target)constructor.Invoke(arguments);sum+=target.Id+target.Value;}return sum;}
        void Measure(string method,Func<long> action){
            _=action();var allocated=GC.GetAllocatedBytesForCurrentThread();var started=Stopwatch.GetTimestamp();var sum=action();var elapsed=Stopwatch.GetElapsedTime(started);allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.Equal((long)count*count,sum);output.WriteLine($"{method}: rows={count}, milliseconds={elapsed.TotalMilliseconds:F3}, allocatedBytes={allocated}, checksum={sum}");
        }
        Measure("typed C#",Typed);Measure("DynamicMethod",Dynamic);Measure("cached reflection",Reflection);
    }
}
