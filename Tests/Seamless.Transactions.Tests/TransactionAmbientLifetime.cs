using System.Runtime.CompilerServices;
using Seamless.Transactions;

namespace Seamless.Transactions.Tests;

public sealed class TransactionAmbientLifetime{
    [Theory]
    [InlineData(true,false)]
    [InlineData(false,false)]
    [InlineData(true,true)]
    [InlineData(false,true)]
    public async Task ClosedRootIsCollectibleWhileItsOwnerAndCapturedFlowRemainAlive(bool commit,bool closeInCopiedFlow){
        await using var owner=new TransactionCoordinator();
        var result=FinishRoot(owner,commit,closeInCopiedFlow);
        for(var attempt=0;attempt<3&&result.Root.IsAlive;attempt++){
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        Assert.False(result.Root.IsAlive,"The completed root is retained by its ambient transaction marker.");
        Assert.False(owner.HasCurrentTransaction);
        ExecutionContext.Run(result.Captured,static state=>Assert.False(((TransactionCoordinator)state!).HasCurrentTransaction),owner);
        await using var next=await owner.BeginAsync();
        await next.RollbackAsync();
        GC.KeepAlive(result.Captured);
        GC.KeepAlive(owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Root,ExecutionContext Captured) FinishRoot(TransactionCoordinator owner,bool commit,bool closeInCopiedFlow){
        var root=owner.BeginAsync().GetAwaiter().GetResult();
        var child=root.BeginNested();
        Assert.Equal(TransactionCommitKind.Voted,child.CommitAsync().GetAwaiter().GetResult().Kind);
        child.DisposeAsync().GetAwaiter().GetResult();
        child.DisposeAsync().GetAwaiter().GetResult();
        Assert.True(owner.HasCurrentTransaction);
        var captured=ExecutionContext.Capture()??throw new InvalidOperationException("The diagnostic flow was not captured.");
        if(closeInCopiedFlow){
            ExecutionContext.Run(captured.CreateCopy(),static state=>{
                var input=((TransactionScope Scope,bool Commit))state!;
                Close(input.Scope,input.Commit);
            },(root,commit));
        }else{
            Close(root,commit);
        }
        Assert.Throws<ObjectDisposedException>(()=>root.CreateSavepoint("closed"));
        return(new(root),captured);
    }

    private static void Close(TransactionScope scope,bool commit){
        if(commit){scope.CommitAsync().GetAwaiter().GetResult();}
        else{scope.RollbackAsync().GetAwaiter().GetResult();}
        scope.DisposeAsync().GetAwaiter().GetResult();
        scope.DisposeAsync().GetAwaiter().GetResult();
    }
}
