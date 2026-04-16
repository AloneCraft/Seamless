using System.Diagnostics;
using Seamless.Collections;
using Seamless.Stores;

namespace Benchmarks;

[Collection("Benchmark")]
public class FixedVariableConstructorBenchmark(ITestOutputHelper output){
    private const int Count = 100_000;
    private static readonly int IndexSize = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Count);
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    [Fact]
    [Trait("Category","")]
    public void ManagedManagedDictionary_固定長と可変長の速度を計測する(){
        using var fixedStore = Factory.Create(IndexSize: IndexSize,MemorySize: MemorySize)
            .Create<int,int>(new ManagedTag(),new ManagedTag());
        using var fixedDict = new ManagedManagedDictionary<int,int,ManagedSerializer<int>,ManagedSerializer<int>>(fixedStore);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            fixedDict.Add(i,i);
        for(var i = 0; i<Count; i++)
            _=fixedDict.TryGetValue(i,out _);
        sw.Stop();
        var fixedUs = sw.Elapsed.TotalMicroseconds/(Count*2);

        using var variableStore = Factory.Create(IndexSize: IndexSize,MemorySize: MemorySize)
            .Create<string,string>(new ManagedTag(),new ManagedTag());
        using var variableDict = new ManagedManagedDictionary<string,string,ManagedSerializer<string>,ManagedSerializer<string>>(variableStore);

        sw.Restart();
        for(var i = 0; i<Count; i++){
            var key = i.ToString();
            variableDict.Add(key,key);
        }
        for(var i = 0; i<Count; i++)
            _=variableDict.TryGetValue(i.ToString(),out _);
        sw.Stop();
        var variableUs = sw.Elapsed.TotalMicroseconds/(Count*2);

        output.WriteLine($"[ManagedManagedDictionary ctor benchmark] fixed(int,int)={fixedUs:F2}us/op, variable(string,string)={variableUs:F2}us/op");

        Assert.Equal(Count,fixedDict.GetLongCount());
        Assert.Equal(Count,variableDict.GetLongCount());
    }

    [Fact]
    [Trait("Category","")]
    public void ManagedHashSet_固定長と可変長の速度を計測する(){
        using var fixedStore = Factory.Create(IndexSize: IndexSize,MemorySize: MemorySize)
            .Create<int>(new ManagedTag());
        using var fixedSet = new ManagedHashSet<int>(fixedStore);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            fixedSet.Add(i);
        for(var i = 0; i<Count; i++)
            _=fixedSet.Contains(i);
        sw.Stop();
        var fixedUs = sw.Elapsed.TotalMicroseconds/(Count*2);

        using var variableStore = Factory.Create(IndexSize: IndexSize,MemorySize: MemorySize)
            .Create<string>(new ManagedTag());
        using var variableSet = new ManagedHashSet<string>(variableStore);

        sw.Restart();
        for(var i = 0; i<Count; i++)
            variableSet.Add(i.ToString());
        for(var i = 0; i<Count; i++)
            _=variableSet.Contains(i.ToString());
        sw.Stop();
        var variableUs = sw.Elapsed.TotalMicroseconds/(Count*2);

        output.WriteLine($"[ManagedHashSet ctor benchmark] fixed(int)={fixedUs:F2}us/op, variable(string)={variableUs:F2}us/op");

        Assert.Equal(Count,fixedSet.GetLongCount());
        Assert.Equal(Count,variableSet.GetLongCount());
    }
}