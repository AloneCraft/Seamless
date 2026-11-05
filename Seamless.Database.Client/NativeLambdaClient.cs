using MemoryPack;

namespace Seamless.Database.Client;

/// <summary>ラムダを生成器で構造化し、自動準備と明示的な準備を同じ経路へ接続する。</summary>
public sealed class NativeLambdaClient<TDatabase>(INativeLambdaTransport transport,NativeWireLimits? limits){
    private readonly INativeLambdaTransport transport=transport??throw new ArgumentNullException(nameof(transport));
    public NativeLambdaClient(INativeLambdaTransport transport):this(transport,null){}

    // これらの呼出位置はRoslyn生成器がinterceptする。Delegateを呼んで解析しない。
    public ClientQuery<TArguments,TResult> DefineQuery<TArguments,TResult>(Func<TDatabase,TArguments,TResult> query)=>throw MissingGenerator();
    public Task<TResult> ExecuteAsync<TArguments,TResult>(TArguments arguments,Func<TDatabase,TArguments,TResult> query,CancellationToken cancellationToken=default)=>throw MissingGenerator();

    public ClientQuery<TArguments,TResult> CreateGeneratedQuery<TArguments,TResult>(NativeLambdaNode body){
        var program=new NativeLambdaProgram(NativeLambdaProgram.CurrentVersion,typeof(TDatabase).FullName!,NativeLambdaContracts.Surface<TDatabase>(),typeof(TArguments).FullName!,typeof(TResult).FullName!,NativeWireContract.For<TArguments>(),NativeWireContract.For<TResult>(),body);
        program.ValidateBounds();return new(this.transport,program,limits??NativeWireLimits.Default);
    }

    private static InvalidOperationException MissingGenerator()=>new("This lambda must be analyzed by Seamless.Database.Generator. Use a supported lambda at the call site.");
}

public sealed class ClientQuery<TArguments,TResult>{
    private readonly INativeLambdaTransport transport;
    private readonly NativeLambdaProgram program;
    private readonly SemaphoreSlim preparation=new(1,1);
    private NativePreparedLambda? prepared;
    private readonly NativeWireValidator resultValidator=NativeWireValidation.For<TResult>();
    private readonly NativeWireLimits limits;
    internal ClientQuery(INativeLambdaTransport transport,NativeLambdaProgram program,NativeWireLimits limits){limits.CheckProfile();this.transport=transport;this.program=program;this.limits=limits;}

    public async Task PrepareAsync(CancellationToken cancellationToken=default){
        if(Volatile.Read(ref this.prepared) is not null){return;}
        await this.preparation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            if(this.prepared is null){Volatile.Write(ref this.prepared,await this.transport.PrepareAsync(this.program,cancellationToken).ConfigureAwait(false));}
        }finally{this.preparation.Release();}
    }

    public async Task<TResult> ExecuteAsync(TArguments arguments,CancellationToken cancellationToken=default){
        await this.PrepareAsync(cancellationToken).ConfigureAwait(false);
        var bytes=MemoryPackSerializer.Serialize(arguments);
        var query=Volatile.Read(ref this.prepared)!;
        byte[] result;
        try{result=await this.transport.ExecuteAsync(query,bytes,cancellationToken).ConfigureAwait(false);}
        catch(NativeLambdaException error)when(error.Code=="Stale"&&error.ExecutionNotStarted){
            // 実行前の明示的な失効だけを再準備する。通信断・実行後の例外は再実行しない。
            Interlocked.CompareExchange(ref this.prepared,null,query);
            await this.PrepareAsync(cancellationToken).ConfigureAwait(false);
            result=await this.transport.ExecuteAsync(Volatile.Read(ref this.prepared)!,bytes,cancellationToken).ConfigureAwait(false);
        }
        this.resultValidator.Validate(result,this.limits);return MemoryPackSerializer.Deserialize<TResult>(result)!;
    }
}
