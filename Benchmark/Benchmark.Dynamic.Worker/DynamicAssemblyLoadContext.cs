using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Benchmark.Dynamic.Worker;

public sealed record AssemblyUnloadResult(bool Unloaded,int CollectionAttempts);

internal sealed class DynamicAssemblyLoadContext:AssemblyLoadContext {
    public DynamicAssemblyLoadContext(string 名前):base(名前,isCollectible:true) {
    }

    protected override Assembly? Load(AssemblyName アセンブリ名) {
        return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
            アセンブリ=>AssemblyName.ReferenceMatchesDefinition(アセンブリ.GetName(),アセンブリ名));
    }
}

internal static class DynamicAssemblyUnloader {
    public static AssemblyUnloadResult Verify(WeakReference 参照,int 最大試行数=3) {
        for(var 試行=1;試行<=最大試行数;試行++) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if(!参照.IsAlive) {
                return new(true,試行);
            }
        }
        return new(false,最大試行数);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WeakReference Release(DynamicAssemblyLoadContext コンテキスト) {
        var 参照=new WeakReference(コンテキスト,trackResurrection:false);
        コンテキスト.Unload();
        return 参照;
    }
}
