using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Seamless.Collections.Benchmarks;

/// <summary>
/// Unmanaged Key × Unmanaged Value (int,int) による Dictionary ベンチマーク。
/// </summary>
public sealed class BenchmarkDictionary_Unmanaged(ITestOutputHelper output)
    : BenchmarkDictionary<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed*2+1;

    protected override DictHandle<int,int> CreateStoreAndDict(double mutableFraction,int elementCount) {
        var factory = CreateFactory(mutableFraction,elementCount);
        if(mutableFraction>=1.0) {
            var store = factory.Create<int,int>();
            return new DictHandle<int,int> {
                Store=store,
                Dict=store.Create(),
            };
        } else {
            var (dir,log,obj) = CreateDevices();
            var store = factory.Create<int,int>(log,obj);
            return new DictHandle<int,int> {
                Store=store,
                Dict=store.Create(),
                Dir=dir,Log=log,Obj=obj,
            };
        }
    }
}

/// <summary>
/// Managed Key × Managed Value (string,string) による Dictionary ベンチマーク。
/// </summary>
public sealed class BenchmarkDictionary_Managed(ITestOutputHelper output)
    : BenchmarkDictionary<string,string>(output) {
    protected override string CreateKey(int seed) => "k"+seed;
    protected override string CreateValue(int seed) => "v"+seed;

    protected override DictHandle<string,string> CreateStoreAndDict(double mutableFraction,int elementCount) {
        var factory = CreateFactory(mutableFraction,elementCount);
        if(mutableFraction>=1.0) {
            var store = factory.Create<string,string>(new ManagedTag(),new ManagedTag());
            return new DictHandle<string,string> {
                Store=store,
                Dict=store.Create(),
            };
        } else {
            var (dir,log,obj) = CreateDevices();
            var store = factory.Create<string,string>(log,obj,new ManagedTag(),new ManagedTag());
            return new DictHandle<string,string> {
                Store=store,
                Dict=store.Create(),
                Dir=dir,Log=log,Obj=obj,
            };
        }
    }
}
