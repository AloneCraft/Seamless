using System.Text.Json;
using Seamless.Database.Query;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    internal TransactionDomainDiagnostics? VersionedDiagnostics=>this.versioned?.Coordinator.GetDomainDiagnostics();
    private LogicalTransaction NewVersionedTransaction(SessionContext session,Guid? requestedId,bool readOnly,bool statement){
        LogicalTransaction transaction;
        lock(this.gate){this.EnsureReady();transaction=new(session.SessionId,requestedId){Manager=this,Session=session,Statement=statement};if(transaction.Id==Guid.Empty||this.active.Contains(transaction.Id)||this.journal.GetOutcome(transaction.Id).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The Gateway transaction identity has already been used.");}transaction.Memory=this.budget.ReserveMemory(512);this.active.Add(transaction.Id);}
        try{transaction.Scope=this.commitCoordinator.BeginOwnedAsync(transaction.Id,readOnly,session.CancellationToken).AsTask().GetAwaiter().GetResult();this.failureInjection?.Invoke(GatewayCommitPhase.TransactionStarted,0);transaction.Completion=transaction.Scope.OnCompletion(()=>{if(!transaction.Completing){this.CloseVersioned(session,transaction);}});return transaction;}
        catch(Exception error){
            transaction.Completing=true;
            try{transaction.Scope?.DisposeAsync().AsTask().GetAwaiter().GetResult();}
            catch(Exception cleanup){throw new AggregateException(error,cleanup);}
            finally{transaction.Memory?.Dispose();transaction.Memory=null;lock(this.gate){this.active.Remove(transaction.Id);}}
            throw;
        }
    }
    private void BeginVersioned(SessionContext session,Guid? requestedId){
        if(session.IsInTransaction){var current=this.Own(session);if(requestedId is{} id&&id!=current.Id){throw new InvalidOperationException("The nested request has a different transaction ID.");}session.BeginNestedTransaction();return;}
        if(session.StatementTransaction is not null){throw new InvalidOperationException("Finish the current statement result before starting another transaction.");}var transaction=this.NewVersionedTransaction(session,requestedId,false,false);session.AttachGatewayTransaction(transaction);
    }
    private LogicalTransaction StartReadStatement(SessionContext session){
        if(session.SnapshotTransaction is not null){throw new InvalidOperationException("A statement already owns this execution context.");}
        LogicalTransaction transaction;
        if(this.versioned is not null){transaction=this.NewVersionedTransaction(session,null,true,true);}
        else{lock(this.gate){this.EnsureReady();transaction=this.NewLegacyTransaction(session,null,true);}}
        session.StatementTransaction=transaction;
        return transaction;
    }
    internal QueryResult ExecuteReadStatement(SessionContext session,Func<QueryResult> execute,bool metadataOnly=false){
        var transaction=this.StartReadStatement(session);
        try{var result=transaction.Scope!.ExecuteOwnedOperation(execute);if(metadataOnly||result.Columns.Count==0){this.EndReadStatement(session,transaction);return result;}return new(result.Columns,session.Enumerate(Rows()),result.RowsAffected);IEnumerable<object?[]> Rows(){using var iterator=result.Rows.GetEnumerator();try{while(transaction.Scope.ExecuteOwnedOperation(iterator.MoveNext)){yield return iterator.Current;}}finally{this.EndReadStatement(session,transaction);}}}
        catch{this.EndReadStatement(session,transaction);throw;}
    }
    internal void EndReadStatement(SessionContext session,LogicalTransaction transaction){
        if(transaction.Closed){return;}
        if(transaction.Scope is not{} scope){this.Close(session,transaction,false);return;}
        transaction.Completing=true;try{scope.DisposeAsync().AsTask().GetAwaiter().GetResult();}finally{this.CloseVersioned(session,transaction);}
    }
    // 逐次結果では応答終了まで取引を維持し、保存済みの結果では送信前に閉じる。
    internal IReadOnlyList<QueryResult> ExecuteReadResults(SessionContext session,Func<IReadOnlyList<QueryResult>> execute,bool materialized=false){
        var transaction=this.StartReadStatement(session);
        var scope=transaction.Scope;
        try{
            var results=scope is null?execute():scope.ExecuteOwnedOperation(execute);
            if(materialized){this.EndReadStatement(session,transaction);return results;}
            if(scope is null){return results;}
            return results.Select(result=>new QueryResult(result.Columns,session.Enumerate(Rows(result)),result.RowsAffected)).ToArray();
        }catch{
            this.EndReadStatement(session,transaction);
            throw;
        }
        IEnumerable<object?[]> Rows(QueryResult result){
            using var iterator=result.Rows.GetEnumerator();
            Func<bool> moveNext=iterator.MoveNext;
            while(scope!.ExecuteOwnedOperation(moveNext)){yield return iterator.Current;}
        }
    }
    private GatewayParticipant EnlistVersioned(SessionContext session,InProcessDatabaseAdapter adapter){
        var transaction=this.Own(session);var id=adapter.Database.Options.GatewayProfile!.DatabaseId;if(transaction.Participants.TryGetValue(id,out var previous)){if(!ReferenceEquals(previous.Adapter,adapter)){throw new SqlQueryException("The SQL binding belongs to another snapshot generation.",3961);}return previous;}
        var selected=this.versioned!.ReadDatabase(transaction.Scope!,id);GatewayPreparedGeneration prepared;lock(this.gate){prepared=this.versioned.Generations[selected.Reference.GenerationId].Prepared!;}
        if(!ReferenceEquals(prepared.Loaded.Adapter,adapter)){throw new SqlQueryException("The SQL binding belongs to another snapshot generation.",3961);}var lease=this.Acquire(session,transaction.Id,id,DatabaseLockMode.Read);IDisposable? context=null;
        try{context=adapter.Database.EnterGatewayVersionedExecution(new(transaction.Id,session.SessionId),transaction.Scope!);var participant=new GatewayParticipant(adapter,lease){Epoch=selected.Epoch,Prepared=prepared,KeepPrepared=true,VersionedContext=context};participant.Registration=this.VersionedRegistration(this.versioned.Generations[selected.Reference.GenerationId],selected.Name);transaction.Memory!.Resize(checked(transaction.Memory.Bytes+512));transaction.Participants.Add(id,participant);session.RefreshTransactionContext();return participant;}catch{context?.Dispose();lease.Dispose();throw;}
    }
    private void EnsureVersionedWrite(SessionContext session,InProcessDatabaseAdapter adapter){var transaction=this.Own(session);if(transaction.IsRollbackOnly||transaction.Scope!.IsReadOnly){throw new SqlQueryException("The transaction is not writable.",3930);}this.EnlistVersioned(session,adapter).Written=true;}
    private IDisposable AcquireVersionedRead(SessionContext? session,InProcessDatabaseAdapter adapter){
        this.EnsureReady();session??=DatabaseRegistry.CurrentSqlSession??new SessionContext(0,adapter.DatabaseName);LogicalTransaction? statement=null;
        if(session.SnapshotTransaction is null){statement=this.StartReadStatement(session);}
        try{_=this.EnlistVersioned(session,adapter);var read=adapter.Database.BeginReadSession(session.CancellationToken);if(statement is null){return read;}return new StatementReadLease(this,session,statement,read);}catch{if(statement is not null){this.EndReadStatement(session,statement);}throw;}
    }
    private sealed class StatementReadLease(GatewayTransactionManager owner,SessionContext session,LogicalTransaction transaction,IDisposable read):IDisposable{public void Dispose(){try{read.Dispose();}finally{owner.EndReadStatement(session,transaction);}}}
    private void CreateVersionedSavepoint(SessionContext session,string name){
        var transaction=this.Own(session);if(transaction.Savepoints.Count>=1024){throw new InvalidOperationException("The Gateway savepoint limit was reached.");}var physical=Guid.NewGuid().ToString("N");transaction.Scope!.CreateSavepoint(physical);try{transaction.Savepoints.Add(new(name,physical,transaction.Participants.Keys.ToArray(),transaction.Participants.Where(static pair=>pair.Value.Written).Select(static pair=>pair.Key).ToArray(),new Dictionary<int,GatewayParticipant>(transaction.Participants),new Dictionary<string,GatewayNativeQueryVersion>(transaction.NativeChanges,StringComparer.Ordinal)));this.Resize(transaction);}catch{transaction.Scope.ReleaseSavepoint(physical);throw;}
    }
    private void RollbackVersionedSavepoint(SessionContext session,string name){
        var transaction=this.Own(session);var index=transaction.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}var point=transaction.Savepoints[index];transaction.Scope!.RollbackToSavepoint(point.PhysicalName);
        foreach(var pair in transaction.Participants.ToArray()){
            if(!point.Versions.TryGetValue(pair.Key,out var restored)){pair.Value.VersionedContext?.Dispose();pair.Value.VersionedContext=null;transaction.Participants.Remove(pair.Key);transaction.RetiredParticipants.Add(pair.Value);}
            else if(!ReferenceEquals(restored,pair.Value)){pair.Value.VersionedContext?.Dispose();pair.Value.VersionedContext=null;transaction.RetiredParticipants.Add(pair.Value);transaction.RetiredParticipants.Remove(restored);transaction.Participants[pair.Key]=restored;restored.VersionedContext??=restored.Adapter.Database.EnterGatewayVersionedExecution(new(transaction.Id,session.SessionId),transaction.Scope!);}
            if(restored is not null){restored.Written=point.Writers.Contains(pair.Key);}
        }
        transaction.Savepoints.RemoveRange(index+1,transaction.Savepoints.Count-index-1);this.RestoreNativeSavepoint(transaction,point);this.Resize(transaction);session.RefreshTransactionContext();
    }
    private void ReleaseVersionedSavepoint(SessionContext session,string name){var transaction=this.Own(session);var index=transaction.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}transaction.Scope!.ReleaseSavepoint(transaction.Savepoints[index].PhysicalName);transaction.Savepoints.RemoveAt(index);this.PruneNativeVersions(transaction);this.Resize(transaction);}
    private void CommitVersioned(SessionContext session){
        var transaction=this.Own(session);if(transaction.IsRollbackOnly){throw new SqlQueryException("The transaction is rollback-only.",3930);}if(session.CommitNestedTransaction()){return;}transaction.Completing=true;Exception? failure=null;
        try{
            foreach(var participant in transaction.Participants.Values.Where(static item=>item.Written||item.GenerationChanged).ToArray()){this.ValidateConstraints(session,this.databases[participant.Adapter.Database.Options.GatewayProfile!.DatabaseId].Name);}
            var changes=transaction.Participants.Values.Where(static item=>item.GenerationChanged).Select(static item=>new GenerationChange(item.Registration!.Name,item.Epoch,item.Prepared!.Reference)).ToArray();foreach(var change in changes){this.versioned!.WriteDatabase(transaction.Scope!,change);}foreach(var pair in transaction.NativeChanges){this.versioned!.WriteNative(transaction.Scope!,pair.Key,pair.Value.Reference);IEnumerable<string> dependencies=pair.Value.Source.Dependencies;lock(this.gate){if(this.nativePublished.TryGetValue(pair.Key,out var previous)){dependencies=dependencies.Concat(previous.Version.Source.Dependencies).ToArray();}}foreach(var name in dependencies.Distinct(StringComparer.OrdinalIgnoreCase)){var id=((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(name).Adapter).Database.Options.GatewayProfile!.DatabaseId;this.versioned.TouchNativeDependencies(transaction.Scope!,id);}}
            byte[] metadata=[];if(changes.Length!=0||transaction.NativeChanges.Count!=0){metadata=JsonSerializer.SerializeToUtf8Bytes(new GenerationCommit("seamless-gateway-generations-v2",changes,transaction.NativeChanges.Select(static pair=>new NativeChange(pair.Key,pair.Value.Reference)).ToArray(),transaction.Participants.Values.Concat(transaction.RetiredParticipants).Where(static participant=>participant.GenerationChanged).Select(static participant=>participant.Prepared!.Reference).Distinct().ToArray()));}
            this.failureInjection?.Invoke(GatewayCommitPhase.BeforeWal,0);transaction.Scope!.CommitOwnedAsync(metadata,installation=>{this.failureInjection?.Invoke(GatewayCommitPhase.WalDurable,0);this.PublishVersioned(transaction,installation.Lsn);this.failureInjection?.Invoke(GatewayCommitPhase.AllDatabasesApplied,0);},session.CancellationToken).AsTask().GetAwaiter().GetResult();
        }catch(Exception error){failure=error;}
        finally{
            try{transaction.Scope!.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){if(failure is null){failure=error;}else{failure=new AggregateException(failure,error);}}
            try{this.CloseVersioned(session,transaction);}catch(Exception error){if(failure is null){failure=error;}else{failure=new AggregateException(failure,error);}}
        }
        if(failure is not null){
            var outcome=this.journal.GetOutcome(transaction.Id).Kind;
            var known=failure as TransactionCommitException??(failure as AggregateException)?.Flatten().InnerExceptions.OfType<TransactionCommitException>().FirstOrDefault();
            if(known is not null){outcome=known.Outcome;}
            if(outcome==TransactionOutcomeKind.Unknown&&known is null){outcome=TransactionOutcomeKind.Aborted;}
            if(outcome==TransactionOutcomeKind.Aborted){this.journal.RecordAborted(transaction.Id);}
            else{this.commitCoordinator.Failure.Fail(failure);}
            throw new TransactionCommitException(transaction.Id,outcome,failure);
        }
    }
    private void PublishVersioned(LogicalTransaction transaction,long lsn){
        this.failureInjection?.Invoke(GatewayCommitPhase.BeforePublication,0);lock(this.gate){
            var replacements=transaction.Participants.Values.Where(static item=>item.GenerationChanged).ToArray();foreach(var participant in replacements){var id=participant.Prepared!.Reference.DatabaseId;var previous=this.published[id];this.versioned!.Generations[previous.Prepared.Reference.GenerationId].Retired=true;var state=new PublishedGenerationState(new(participant.Registration!.Name,participant.Epoch,lsn,participant.Prepared.Reference),previous.State.Current);this.published[id]=new(state,participant.Prepared);this.databases[id]=new(id,participant.Registration.Name,participant.Adapter);this.versioned.Generations[participant.Prepared.Reference.GenerationId].Published=true;this.failureInjection?.Invoke(GatewayCommitPhase.DatabaseApplied,id);}if(replacements.Length!=0){this.generationRegistry!.Publish(replacements.Select(static item=>item.Registration!).ToArray());}this.PublishNativeVersions(transaction);
        }this.failureInjection?.Invoke(GatewayCommitPhase.AfterPublication,0);
    }
    private void CloseVersioned(SessionContext session,LogicalTransaction transaction){
        lock(this.gate){if(transaction.Closed){return;}transaction.Closed=true;this.active.Remove(transaction.Id);}List<Exception>? errors=null;
        foreach(var participant in transaction.Participants.Values.Concat(transaction.RetiredParticipants).Distinct()){
            void Release(Action action){try{action();}catch(Exception error){(errors??=[]).Add(error);}}
            Release(()=>{if(transaction.Completing){participant.VersionedContext?.Dispose();}});participant.VersionedContext=null;Release(()=>participant.SchemaLease?.Dispose());Release(()=>participant.WriteLease?.Dispose());Release(participant.ReadLease.Dispose);participant.Closed=true;
            lock(this.gate){if(participant.GenerationChanged&&!this.versioned!.Generations[participant.Prepared!.Reference.GenerationId].Published){this.versioned.Generations[participant.Prepared.Reference.GenerationId].Abandoned=true;}}
        }
        var nativeErrors=new List<Exception>();this.CloseNativeVersions(transaction,nativeErrors);if(nativeErrors.Count!=0){(errors??=[]).AddRange(nativeErrors);}transaction.Participants.Clear();transaction.RetiredParticipants.Clear();transaction.Savepoints.Clear();transaction.Completion?.Dispose();transaction.Completion=null;transaction.Memory?.Dispose();transaction.Memory=null;
        if(transaction.Statement){session.EndStatementTransaction(transaction);}else if(ReferenceEquals(session.Transaction,transaction)){session.DetachTransaction();}if(errors is not null){throw new AggregateException("Versioned Gateway transaction cleanup failed.",errors);}this.ScheduleVersionedCollection();
    }
}
