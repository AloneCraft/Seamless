using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Seamless.Collections.Benchmarks;

/// <summary>Unmanaged (int) キーによる HashSet ベンチマーク。</summary>
public sealed class BenchmarkHashSet_Unmanaged(ITestOutputHelper output)
    : BenchmarkHashSet<int>(output) {
    protected override int CreateKey(int seed) => seed;

    protected override HashSetHandle<int> CreateStoreAndSet(double mutableFraction,int elementCount,bool needsOther) {
        var factory = CreateFactory(mutableFraction,elementCount);
        if(mutableFraction>=1.0) {
            var storeA = factory.Create<int>(new UnmanagedTag());
            var h = new HashSetHandle<int> {
                Store=storeA,
                Set=storeA.Create(),
            };
            if(!needsOther) return h;
            var storeB = factory.Create<int>(new UnmanagedTag());
            return new HashSetHandle<int> {
                Store=storeA,Set=h.Set,
                OtherStore=storeB,Other=storeB.Create(),
            };
        } else {
            var (dir,log,obj) = CreateDevices();
            var storeA = factory.Create<int>(log,obj,new UnmanagedTag());
            if(!needsOther) {
                return new HashSetHandle<int> {
                    Store=storeA,Set=storeA.Create(),
                    Dir=dir,Log=log,Obj=obj,
                };
            }
            var dir2 = Path.Combine(dir,"other");
            Directory.CreateDirectory(dir2);
            var logB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir2,"log.data"),deleteOnClose: true);
            var objB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir2,"obj.data"),deleteOnClose: true);
            var storeB = factory.Create<int>(logB,objB,new UnmanagedTag());
            return new HashSetHandle<int> {
                Store=storeA,Set=storeA.Create(),
                OtherStore=storeB,Other=storeB.Create(),
                Dir=dir,Log=log,Obj=obj,LogOther=logB,ObjOther=objB,
            };
        }
    }
}

/// <summary>Managed (string) キーによる HashSet ベンチマーク。</summary>
public sealed class BenchmarkHashSet_Managed(ITestOutputHelper output)
    : BenchmarkHashSet<string>(output) {
    protected override string CreateKey(int seed) => "k"+seed;

    protected override HashSetHandle<string> CreateStoreAndSet(double mutableFraction,int elementCount,bool needsOther) {
        var factory = CreateFactory(mutableFraction,elementCount);
        if(mutableFraction>=1.0) {
            var storeA = factory.Create<string>(new ManagedTag());
            if(!needsOther) {
                return new HashSetHandle<string> {
                    Store=storeA,Set=storeA.Create(),
                };
            }
            var storeB = factory.Create<string>(new ManagedTag());
            return new HashSetHandle<string> {
                Store=storeA,Set=storeA.Create(),
                OtherStore=storeB,Other=storeB.Create(),
            };
        } else {
            var (dir,log,obj) = CreateDevices();
            var storeA = factory.Create<string>(log,obj,new ManagedTag());
            if(!needsOther) {
                return new HashSetHandle<string> {
                    Store=storeA,Set=storeA.Create(),
                    Dir=dir,Log=log,Obj=obj,
                };
            }
            var dir2 = Path.Combine(dir,"other");
            Directory.CreateDirectory(dir2);
            var logB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir2,"log.data"),deleteOnClose: true);
            var objB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir2,"obj.data"),deleteOnClose: true);
            var storeB = factory.Create<string>(logB,objB,new ManagedTag());
            return new HashSetHandle<string> {
                Store=storeA,Set=storeA.Create(),
                OtherStore=storeB,Other=storeB.Create(),
                Dir=dir,Log=log,Obj=obj,LogOther=logB,ObjOther=objB,
            };
        }
    }
}
