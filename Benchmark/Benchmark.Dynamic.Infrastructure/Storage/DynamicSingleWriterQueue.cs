using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Infrastructure;

public sealed class DynamicSingleWriterQueue:IAsyncDisposable{
    private readonly DynamicDatabase database;
    private readonly Channel<IWriteRequest> channel;
    private readonly Task consumerTask;
    private int disposed;

    public DynamicSingleWriterQueue(DynamicDatabase database){
        this.database=database??throw new ArgumentNullException(nameof(database));
        this.channel=Channel.CreateUnbounded<IWriteRequest>(new UnboundedChannelOptions{
            SingleReader=true,
            SingleWriter=false,
            AllowSynchronousContinuations=false,
        });
        this.consumerTask=Task.Run(this.ConsumeAsync);
    }

    public Task<T> ExecuteAsync<T>(
        Func<SqliteConnection,SqliteTransaction,T> action,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(action);
        if(Volatile.Read(ref this.disposed)!=0){
            return Task.FromException<T>(new ObjectDisposedException(nameof(DynamicSingleWriterQueue)));
        }

        var request=new WriteRequest<T>(action,cancellationToken);
        if(!this.channel.Writer.TryWrite(request)){
            request.Fail(new ObjectDisposedException(nameof(DynamicSingleWriterQueue)));
        }
        return request.Task;
    }

    public async Task FlushAsync(CancellationToken cancellationToken=default){
        await this.ExecuteAsync(static (_,_)=>true,cancellationToken);
    }

    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.disposed,1)!=0){
            return;
        }
        this.channel.Writer.TryComplete();
        await this.consumerTask;
    }

    private async Task ConsumeAsync(){
        SqliteConnection? connection=null;
        try{
            await foreach(var request in this.channel.Reader.ReadAllAsync()){
                if(request.IsCancellationRequested){
                    request.Cancel();
                    continue;
                }

                try{
                    connection??=this.database.OpenWritableConnection();
                    this.database.VerifyWritableVersion(connection);
                    using var transaction=connection.BeginTransaction();
                    request.Execute(connection,transaction);
                    transaction.Commit();
                    request.Complete();
                }catch(Exception exception){
                    request.Fail(exception);
                    if(exception is SqliteException or DynamicDatabaseVersionMismatchException){
                        connection?.Dispose();
                        connection=null;
                    }
                }
            }
        }finally{
            connection?.Dispose();
            while(this.channel.Reader.TryRead(out var request)){
                request.Fail(new ObjectDisposedException(nameof(DynamicSingleWriterQueue)));
            }
        }
    }

    private interface IWriteRequest{
        bool IsCancellationRequested{get;}
        void Execute(SqliteConnection connection,SqliteTransaction transaction);
        void Complete();
        void Cancel();
        void Fail(Exception exception);
    }

    private sealed class WriteRequest<T>(
        Func<SqliteConnection,SqliteTransaction,T> action,
        CancellationToken cancellationToken):IWriteRequest{
        private readonly TaskCompletionSource<T> completionSource=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private T? result;

        public Task<T> Task=>this.completionSource.Task;
        public bool IsCancellationRequested=>cancellationToken.IsCancellationRequested;

        public void Execute(SqliteConnection connection,SqliteTransaction transaction){
            this.result=action(connection,transaction);
        }

        public void Complete(){
            this.completionSource.TrySetResult(this.result!);
        }

        public void Cancel(){
            this.completionSource.TrySetCanceled(cancellationToken);
        }

        public void Fail(Exception exception){
            this.completionSource.TrySetException(exception);
        }
    }
}
