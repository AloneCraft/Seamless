using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using MemoryPack;

namespace Seamless.Database.Client;

/// <summary>接続単位で要求を直列化する型付きクエリクライアント。</summary>
public sealed class NativeQueryClient(string host="localhost",int port=14335):IAsyncDisposable{
    private readonly SemaphoreSlim gate=new(1,1);
    private TcpClient? socket;
    private NetworkStream? stream;
    private NativeTransaction? transaction;
    private bool disposed;
    public async Task ConnectAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.transaction is not null){throw new InvalidOperationException("A transaction already owns this connection.");}await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);try{await this.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);}catch{this.CloseConnection();throw;}finally{this.gate.Release();}
    }
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken){if(this.socket is not null){return;}var connected=new TcpClient{NoDelay=true};try{await connected.ConnectAsync(host,port,cancellationToken).ConfigureAwait(false);this.socket=connected;this.stream=connected.GetStream();}catch{connected.Dispose();throw;}}
    public IAsyncEnumerable<TResult> QueryAsync<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>this.QueryCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken);
    internal async IAsyncEnumerable<TResult> QueryCore<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,NativeTransaction? transaction,[EnumeratorCancellation]CancellationToken cancellationToken){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(transaction is null&&this.transaction is not null){throw new InvalidOperationException("Use the active transaction to execute queries.");}
        if(transaction is not null){this.Validate(transaction);transaction.Enter();}else{await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);}
        var complete=false;
        try{
            var request=new NativeQueryRequest(moduleId,queryId,database,schemaFingerprint,MemoryPackSerializer.Serialize(arguments),transaction?.Id??Guid.Empty);
            await this.SendAsync(request,cancellationToken).ConfigureAwait(false);
            while(true){
                var frame=await NativeFrame.ReadAsync(this.stream!,cancellationToken).ConfigureAwait(false)??throw new EndOfStreamException("The server disconnected before completing the query.");
                if(frame.Kind==NativeFrame.Completed){complete=true;yield break;}
                if(frame.Kind==NativeFrame.Error){throw new NativeQueryException(Encoding.UTF8.GetString(frame.Payload));}
                if(frame.Kind!=NativeFrame.Row){throw new InvalidDataException("Unexpected native response kind.");}
                yield return MemoryPackSerializer.Deserialize<TResult>(frame.Payload)!;
            }
        }finally{
            if(!complete){this.CloseConnection();}
            if(transaction is not null){transaction.Exit();}else{this.gate.Release();}
        }
    }
    public async Task<TResult> ExecuteScalarAsync<TArguments,TResult>(string moduleId,string queryId,string database,string fingerprint,TArguments arguments,CancellationToken cancellationToken=default){
        var found=false;TResult result=default!;
        await foreach(var value in this.QueryAsync<TArguments,TResult>(moduleId,queryId,database,fingerprint,arguments,cancellationToken).ConfigureAwait(false)){
            if(found){throw new InvalidDataException("A scalar query returned multiple rows.");}result=value;found=true;
        }
        if(!found){throw new InvalidDataException("A scalar query returned no rows.");}return result;
    }
    public async Task<NativeTransaction> BeginTransactionAsync(string database,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.transaction is not null){throw new InvalidOperationException("A transaction is already active.");}
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{var id=Guid.NewGuid();await this.ControlAsync("begin",database,id,cancellationToken).ConfigureAwait(false);var result=new NativeTransaction(this,database,id);this.transaction=result;return result;}
        catch{this.CloseConnection();this.gate.Release();throw;}
    }
    internal async Task FinishTransactionAsync(NativeTransaction transaction,bool commit,CancellationToken cancellationToken){
        this.Validate(transaction);transaction.Enter();
        try{var operation="rollback";if(commit){operation="commit";}await this.ControlAsync(operation,transaction.Database,transaction.Id,cancellationToken).ConfigureAwait(false);}
        catch{this.CloseConnection();throw;}
        finally{transaction.Exit();transaction.Finish();this.transaction=null;this.gate.Release();}
    }
    private void Validate(NativeTransaction transaction){if(!ReferenceEquals(this.transaction,transaction)||transaction.IsCompleted){throw new InvalidOperationException("The transaction does not belong to this connection or has completed.");}}
    private async Task ControlAsync(string operation,string database,Guid transactionId,CancellationToken cancellationToken){
        await this.SendAsync(new("$transaction",operation,database,"",[],transactionId),cancellationToken).ConfigureAwait(false);
        var frame=await NativeFrame.ReadAsync(this.stream!,cancellationToken).ConfigureAwait(false)??throw new EndOfStreamException();
        if(frame.Kind==NativeFrame.Error){throw new NativeQueryException(Encoding.UTF8.GetString(frame.Payload));}if(frame.Kind!=NativeFrame.Completed){throw new InvalidDataException("Unexpected transaction response.");}
    }
    private async ValueTask SendAsync(NativeQueryRequest request,CancellationToken cancellationToken){
        await this.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await NativeFrame.WriteAsync(this.stream!,NativeFrame.Request,MemoryPackSerializer.Serialize(request),cancellationToken).ConfigureAwait(false);
    }
    private void CloseConnection(){this.stream?.Dispose();this.socket?.Dispose();this.stream=null;this.socket=null;}
    public async ValueTask DisposeAsync(){
        if(this.disposed){return;}
        try{if(this.transaction is not null){await this.transaction.DisposeAsync().ConfigureAwait(false);}}
        finally{this.disposed=true;this.CloseConnection();this.gate.Dispose();}
    }
}
public sealed class NativeTransaction:IAsyncDisposable{
    private readonly NativeQueryClient client;
    private int active;
    internal NativeTransaction(NativeQueryClient client,string database,Guid id){this.client=client;this.Database=database;this.Id=id;}
    public string Database{get;}
    public Guid Id{get;}
    public bool IsCompleted{get;private set;}
    internal void Enter(){if(Interlocked.CompareExchange(ref this.active,1,0)!=0){throw new InvalidOperationException("Concurrent operations in one transaction are not supported.");}}
    internal void Exit()=>Volatile.Write(ref this.active,0);
    internal void Finish()=>this.IsCompleted=true;
    public IAsyncEnumerable<TResult> QueryAsync<TArguments,TResult>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>this.client.QueryCore<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken);
    public Task CommitAsync(CancellationToken cancellationToken=default)=>this.client.FinishTransactionAsync(this,true,cancellationToken);
    public Task RollbackAsync(CancellationToken cancellationToken=default)=>this.client.FinishTransactionAsync(this,false,cancellationToken);
    public async ValueTask DisposeAsync(){if(!this.IsCompleted){await this.RollbackAsync().ConfigureAwait(false);}}
}
