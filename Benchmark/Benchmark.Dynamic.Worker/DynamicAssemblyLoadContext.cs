using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Benchmark.Dynamic.Worker;

public sealed record AssemblyUnloadResult(bool Unloaded,int CollectionAttempts);

internal sealed class DynamicAssemblyLoadContext:AssemblyLoadContext {
    private readonly bool isolateAssertions;
    public DynamicAssemblyLoadContext(string 名前,bool isolateAssertions=false):base(名前,isCollectible:true) {
        this.isolateAssertions=isolateAssertions;
    }

    protected override Assembly? Load(AssemblyName アセンブリ名) {
        // assertion側の型別キャッシュも、独自型を持つ回帰ソースと同じ寿命にする。
        if(this.isolateAssertions&&アセンブリ名.Name=="xunit.assert"){
            return this.LoadFromAssemblyPath(typeof(Xunit.Assert).Assembly.Location);
        }
        return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
            アセンブリ=>AssemblyName.ReferenceMatchesDefinition(アセンブリ.GetName(),アセンブリ名));
    }
}

internal static class DynamicAssemblyUnloader {
    public static AssemblyUnloadResult Verify(WeakReference 参照,int 最大試行数=30) {
        for(var 試行=1;試行<=最大試行数;試行++) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if(!参照.IsAlive) {
                return new(true,試行);
            }
            Thread.Sleep(30);
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
