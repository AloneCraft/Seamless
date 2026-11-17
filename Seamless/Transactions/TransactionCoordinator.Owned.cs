using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Transactions;

public sealed partial class TransactionCoordinator{
    /// <summary>接続所有のroot。AsyncLocalの通常rootへ合流せず、外側の接続が寿命を所有する。</summary>
    internal async ValueTask<TransactionScope> BeginOwnedAsync(Guid id,bool readOnly,CancellationToken cancellationToken=default){
        if(id==Guid.Empty){throw new ArgumentException("An owned transaction identity is required.",nameof(id));}if(this.versioned is null){throw new NotSupportedException("Owned scopes require a versioned domain.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();if(!this.started){this.Recover();}
        cancellationToken.ThrowIfCancellationRequested();if((await this.GetOutcomeAsync(id).ConfigureAwait(false)).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The transaction identity has already completed.");}
        return await this.Acquire(new Marker(),readOnly,cancellationToken,id).ConfigureAwait(false);
    }
}
public sealed partial class TransactionScope{
    internal long ReadVersion=>this.state.Context.ReadVersion;
    internal T ExecuteOwnedOperation<T>(Func<T> action){using var access=this.Enter(allowQuery:true);var previous=this.state.QueryThread;this.state.QueryThread=Environment.CurrentManagedThreadId;try{var result=action();this.state.Context.ThrowIfUnavailable();return result;}finally{this.state.QueryThread=previous;}}
    /// <summary>全行設置後・共通prefix公開前のGateway参照更新。利用者の業務callbackには公開しない。</summary>
    internal ValueTask<TransactionCommitResult> CommitOwnedAsync(ReadOnlyMemory<byte> metadata,Action<TransactionInstallationContext> installed,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(installed);if(this.parent is not null||this.state.Context.Domain is null){throw new InvalidOperationException("Install publication belongs to the owned versioned root.");}return this.CommitCore(metadata,cancellationToken,installed);
    }
}
