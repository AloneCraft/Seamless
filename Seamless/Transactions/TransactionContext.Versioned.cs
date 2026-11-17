using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
namespace Seamless.Transactions;

public sealed partial class TransactionContext{
    internal TransactionCoordinator.OptimisticDomain? Domain;
    internal readonly Dictionary<TransactionCoordinator.ConflictKey,bool> Dependencies=[];
    internal readonly HashSet<IVersionedTransactionParticipant> Scans=[];
    internal readonly HashSet<ITransactionParticipant> Participants=[];
    internal ResourceReservation? Metadata;
    internal bool Sealed;
    internal int Operations;
    internal Action? Retire;
    public void ObserveRead(IVersionedTransactionParticipant participant,ReadOnlySpan<byte> key){
        this.ThrowIfUnavailable();
        (this.Domain??throw new InvalidOperationException("A versioned domain is required.")).Observe(this,participant,key,false);
    }
    public void ObserveWrite(IVersionedTransactionParticipant participant,ReadOnlySpan<byte> key){
        this.EnsureWritable();
        (this.Domain??throw new InvalidOperationException("A versioned domain is required.")).Observe(this,participant,key,true);
    }
    public void ObserveTableRead(IVersionedTransactionParticipant participant){
        this.ThrowIfUnavailable();
        (this.Domain??throw new InvalidOperationException("A versioned domain is required.")).ObserveScan(this,participant);
    }
}
