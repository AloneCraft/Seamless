namespace Seamless.Database.Client;

public sealed partial class NativeQueryClient{
    public NativeLambdaClient<TDatabase> Lambda<TDatabase>(string database)=>this.Lambda<TDatabase>(database,null);
    public NativeLambdaClient<TDatabase> Lambda<TDatabase>(string database,NativeWireLimits? limits)=>new(new LambdaTransport(this,database,null),limits);
    internal NativeLambdaClient<TDatabase> Lambda<TDatabase>(NativeTransaction transaction,NativeWireLimits? limits)=>new(new LambdaTransport(this,transaction.Database,transaction),limits);

    private async Task<byte[]> LambdaRequestAsync(byte kind,byte[] payload,NativeTransaction? transaction,CancellationToken cancellation){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(transaction is null&&this.transaction is not null){throw new InvalidOperationException("Use the active transaction to execute lambdas.");}
        if(transaction is null){await this.gate.WaitAsync(cancellation).ConfigureAwait(false);}else{this.Validate(transaction);transaction.Enter();}
        var complete=false;
        try{
            await this.EnsureConnectedAsync(cancellation).ConfigureAwait(false);
            await NativeFrame.WriteAsync(this.stream!,kind,payload,cancellation).ConfigureAwait(false);
            byte[]? result=null;
            while(true){
                var frame=await NativeFrame.ReadAsync(this.stream!,cancellation).ConfigureAwait(false)??throw new EndOfStreamException("The lambda result is unknown because the server disconnected.");
                if(frame.Kind==NativeLambdaWire.Error){complete=true;throw NativeLambdaWire.ReadFailure(frame.Payload);}
                if(frame.Kind==NativeFrame.Completed){if(result is null){throw new InvalidDataException("The lambda result is missing.");}complete=true;return result;}
                var expected=NativeFrame.Row;if(kind==NativeLambdaWire.Prepare){expected=NativeLambdaWire.Prepared;}
                if(result is not null||frame.Kind!=expected){throw new InvalidDataException("Unexpected lambda response.");}
                result=frame.Payload;
            }
        }finally{
            if(!complete){this.CloseConnection();if(transaction is not null&&ReferenceEquals(this.transaction,transaction)){transaction.Finish();this.transaction=null;this.gate.Release();}}
            if(transaction is null){this.gate.Release();}else{transaction.Exit();}
        }
    }
    private sealed class LambdaTransport(NativeQueryClient owner,string database,NativeTransaction? transaction):INativeLambdaTransport{
        public async Task<NativePreparedLambda> PrepareAsync(NativeLambdaProgram program,CancellationToken cancellationToken)=>NativeLambdaWire.ReadHandle(await owner.LambdaRequestAsync(NativeLambdaWire.Prepare,NativeLambdaWire.PrepareRequest(database,program),transaction,cancellationToken).ConfigureAwait(false));
        public Task<byte[]> ExecuteAsync(NativePreparedLambda query,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken)=>owner.LambdaRequestAsync(NativeLambdaWire.Execute,NativeLambdaWire.ExecuteRequest(database,query,transaction?.Id??Guid.Empty,arguments),transaction,cancellationToken);
    }
}

public sealed partial class NativeTransaction{
    public NativeLambdaClient<TDatabase> Lambda<TDatabase>()=>this.client.Lambda<TDatabase>(this,null);
    public NativeLambdaClient<TDatabase> Lambda<TDatabase>(NativeWireLimits? limits)=>this.client.Lambda<TDatabase>(this,limits);
}
