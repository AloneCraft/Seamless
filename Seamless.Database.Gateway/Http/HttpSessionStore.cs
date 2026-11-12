using System.Security.Cryptography;
using Seamless.Database.Gateway.Hosting;
using Seamless.Execution;
using Seamless.Security;

namespace Seamless.Database.Gateway.Http;

public sealed record HttpServerOptions{
    public global::System.Net.IPAddress ListenAddress{get;init;}=DefaultListenAddress();
    public global::System.Security.Cryptography.X509Certificates.X509Certificate2? Certificate{get;init;}
    public long? MaximumRequestBodyBytes{get;init;}
    public long? MaximumBufferedRequestBodyBytes{get;init;}
    public long? MaximumPendingWriteBytes{get;init;}
    public TimeSpan? ResponseWriteTimeout{get;init;}
    public long? MaximumConnections{
        get;
        init{
            if(value is{} limit){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit,nameof(MaximumConnections));}
            field=value;
        }
    }
    public int? MaximumConcurrentRequests{get;init;}
    public int MaximumSessions{get;init;}=128;
    public TimeSpan SessionIdleTimeout{get;init;}=TimeSpan.FromMinutes(5);
    public ExecutionOptions Execution{get;init;}=new();
    private static global::System.Net.IPAddress DefaultListenAddress(){if(global::System.Net.Sockets.Socket.OSSupportsIPv6){return global::System.Net.IPAddress.IPv6Any;}return global::System.Net.IPAddress.Any;}
}

/// <summary>HTTP 接続プールとは独立した所有者。要求全体を直列化し、期限切れの取引を取消する。</summary>
internal sealed class HttpSessionStore:IDisposable{
    private readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
    private readonly object gate=new();
    private readonly HttpServerOptions options;
    private readonly ResourceBudget budget;
    private readonly Timer timer;
    private bool disposed;
    private long nextId;
    private Exception? lastCleanupError;
    internal HttpSessionStore(HttpServerOptions options,bool protectedTransactions=false){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumSessions);if(options.SessionIdleTimeout<TimeSpan.FromSeconds(1)){throw new ArgumentOutOfRangeException(nameof(options.SessionIdleTimeout));}
        this.options=options;this.budget=options.Execution.ParentBudget?.CreateChild(options.Execution.MemoryLimitBytes,options.Execution.TemporaryStorageLimitBytes)??new(options.Execution.MemoryLimitBytes,options.Execution.TemporaryStorageLimitBytes);
        var interval=TimeSpan.FromTicks(Math.Min(options.SessionIdleTimeout.Ticks,(protectedTransactions?TimeSpan.FromSeconds(1):TimeSpan.FromMinutes(1)).Ticks));this.timer=new(_=>this.Expire(),null,interval,interval);
    }
    internal Exception? LastCleanupError=>Volatile.Read(ref this.lastCleanupError);
    internal ExecutionScope Execution(CancellationToken token)=>new(this.options.Execution with{ParentBudget=this.budget,CancellationToken=token});
    internal string Create(string database,SecuritySubjectId? subject=null){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.entries.Count>=this.options.MaximumSessions){throw new HttpSessionException(429,"The HTTP session limit was reached.");}
            var memory=this.budget.ReserveMemory(1024);try{var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));this.entries.Add(token,new(new(-Interlocked.Increment(ref this.nextId),database),memory,subject));return token;}catch{memory.Dispose();throw;}
        }
    }
    internal async ValueTask<Lease> AcquireAsync(string token,CancellationToken cancellationToken,SecuritySubjectId? subject=null){
        Entry entry;lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);if(!this.entries.TryGetValue(token,out entry!)||entry.Subject!=subject){throw new HttpSessionException(404,"The HTTP session was not found or has expired.");}}
        await entry.Serial.WaitAsync(cancellationToken);
        lock(this.gate){if(this.disposed||!this.entries.TryGetValue(token,out var current)||!ReferenceEquals(entry,current)){entry.Serial.Release();throw new HttpSessionException(404,"The HTTP session has ended.");}}
        return new(entry);
    }
    internal async ValueTask<Lease?> TryAcquireTransactionAsync(string token,Guid id,byte[] proof,CancellationToken cancellation){
        Entry entry;ProtectedTransaction transaction;
        lock(this.gate){
            if(this.disposed||!this.entries.TryGetValue(token,out entry!)||entry.TransactionAuthorization is not{} candidate
                ||candidate.Id!=id||!CryptographicOperations.FixedTimeEquals(candidate.BearerProof,proof)){return null;}
            transaction=candidate;
        }
        await entry.Serial.WaitAsync(cancellation);
        lock(this.gate){
            if(!this.disposed&&this.entries.TryGetValue(token,out var current)&&ReferenceEquals(entry,current)
                &&ReferenceEquals(transaction,entry.TransactionAuthorization)){return new(entry);}
        }
        entry.Serial.Release();return null;
    }
    internal void Delete(string token,Lease lease){
        lock(this.gate){if(!this.entries.Remove(token,out var entry)||!ReferenceEquals(entry,lease.Entry)){throw new HttpSessionException(404,"The HTTP session has ended.");}}
        lease.Entry.Close();
    }
    private void Expire(){
        lock(this.gate){
            if(this.disposed){return;}
            foreach(var pair in this.entries.ToArray()){
                var entry=pair.Value;if(!this.ShouldExpire(entry)||!entry.Serial.Wait(0)){continue;}
                try{if(this.ShouldExpire(entry)){this.entries.Remove(pair.Key);entry.Close();}}catch(Exception error){Volatile.Write(ref this.lastCleanupError,error);}finally{entry.Serial.Release();}
            }
        }
    }
    private bool ShouldExpire(Entry entry)=>entry.TransactionAuthorization?.Authorization.CancellationToken.IsCancellationRequested==true
        ||global::System.Diagnostics.Stopwatch.GetElapsedTime(Volatile.Read(ref entry.LastUsed))>=this.options.SessionIdleTimeout;
    public void Dispose(){
        Entry[] remaining;lock(this.gate){if(this.disposed){return;}this.disposed=true;remaining=this.entries.Values.ToArray();this.entries.Clear();}this.timer.Dispose();
        foreach(var entry in remaining){entry.Serial.Wait();try{entry.Close();}catch(Exception error){Volatile.Write(ref this.lastCleanupError,error);}finally{entry.Serial.Release();}}
    }
    internal sealed class Entry(SessionContext session,ResourceReservation memory,SecuritySubjectId? subject){
        private ProtectedTransaction? transactionAuthorization;
        internal SecuritySubjectId? Subject{get;}=subject;
        internal SessionContext Session{get;}=session;
        internal SemaphoreSlim Serial{get;}=new(1,1);
        internal long LastUsed=global::System.Diagnostics.Stopwatch.GetTimestamp();
        internal ProtectedTransaction? TransactionAuthorization=>Volatile.Read(ref this.transactionAuthorization);
        internal void AttachAuthorization(AuthorizationLease authorization,byte[] proof){
            var transaction=this.Session.Transaction?.Id??throw new InvalidOperationException("An active transaction is required.");
            Volatile.Write(ref this.transactionAuthorization,new(transaction,authorization,proof));
        }
        internal void ReleaseAuthorization()=>Interlocked.Exchange(ref this.transactionAuthorization,null)?.Authorization.Dispose();
        internal void Close(){try{this.Session.Dispose();}finally{this.ReleaseAuthorization();memory.Dispose();}}
    }
    internal sealed record ProtectedTransaction(Guid Id,AuthorizationLease Authorization,byte[] BearerProof);
    internal sealed class Lease(Entry entry):IDisposable{
        private bool disposed;
        internal Entry Entry{get;}=entry;
        internal SessionContext Session=>this.Entry.Session;
        public void Dispose(){if(this.disposed){return;}this.Session.CancellationToken=CancellationToken.None;Volatile.Write(ref this.Entry.LastUsed,global::System.Diagnostics.Stopwatch.GetTimestamp());this.Entry.Serial.Release();this.disposed=true;}
    }
}
internal sealed class HttpSessionException(int status,string message):Exception(message){internal int Status{get;}=status;}
