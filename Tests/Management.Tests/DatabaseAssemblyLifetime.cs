using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class DatabaseAssemblyLifetime{
    [Fact]
    public void PublishedDatabaseContextsAreCollectedAfterReplacementAndHostDisposal(){
        var contexts=PublishUseAndClose();
        Assert.NotEmpty(contexts);
        for(var attempt=0;attempt<10&&contexts.Any(static context=>context.IsAlive);attempt++){
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        Assert.All(contexts,static context=>Assert.False(context.IsAlive,"停止したDBの読み込みコンテキストが保持されています。"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] PublishUseAndClose(){
        var contexts=new List<WeakReference>();
        void Loaded(object? sender,AssemblyLoadEventArgs args){
            if(AssemblyLoadContext.GetLoadContext(args.LoadedAssembly) is ManagedLoadContext context
                &&args.LoadedAssembly.GetType("Model.Db",false) is not null){
                contexts.Add(new(context));
            }
        }
        AppDomain.CurrentDomain.AssemblyLoad+=Loaded;
        try{
            var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"assembly-lifetime-"+Guid.NewGuid().ToString("N"));
            using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
            using var service=new Service(store,null,startAuditWorker:false);
            var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
            var definition=NativeWorkflow.Database();
            NativeWorkflow.Publish(service,user,definition);
            service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))])).GetAwaiter().GetResult();
            NativeWorkflow.Publish(service,user,definition with{Source=definition.Source with{Revision="2"}},1,1);
            var row=Assert.Single(service.ReadRowsAsync(user,new("Native","Rows")).GetAwaiter().GetResult().Rows);
            Assert.Equal(7,row.Values.GetProperty("value").GetInt32());
        }finally{AppDomain.CurrentDomain.AssemblyLoad-=Loaded;}
        return contexts.ToArray();
    }
}
