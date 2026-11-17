using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Seamless.Execution;

namespace Seamless.Database.Client.Results;

/// <summary>結果ごとの容量と、メモリから一時ファイルへ切り替えるしきい値。</summary>
public sealed record NativeResultOptions(string TemporaryDirectory,long MemoryLimitBytes,long TemporaryStorageLimitBytes,long MemoryThresholdBytes){
    public ResourceBudget? ParentBudget{get;init;}
}

/// <summary>切断後も受信順に読み直せる、完了済みのクエリ結果。</summary>
public sealed class NativeResult<T>:IDisposable{
    private readonly ExecutionScope scope;
    internal ClientResultSpool<T> Storage{get;}
    internal ResourceBudget Budget=>this.scope.Budget;

    internal NativeResult(NativeResultOptions options,Func<ReadOnlySpan<byte>,T> decode){
        this.scope=new(new(){
            ParentBudget=options.ParentBudget,MemoryLimitBytes=options.MemoryLimitBytes,
            TemporaryStorageLimitBytes=options.TemporaryStorageLimitBytes,TemporaryDirectory=options.TemporaryDirectory
        });
        this.Storage=new(this.scope,options.MemoryThresholdBytes,decode);
    }

    public bool IsFileBacked=>this.Storage.Spilled;

    /// <summary>先頭から読み直す。列挙子は一度に一つ進められる。</summary>
    public IEnumerable<T> ReadRows()=>this.Storage.ReadRows();

    public void Dispose(){
        try{this.Storage.Dispose();}
        finally{this.scope.Dispose();}
    }
}

public static class NativeResultExtensions{
    public static async Task<NativeResult<T>> ToResultAsync<T>(this NativeResultStream<T> rows,NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(rows);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(rows.CancellationToken,cancellationToken);
        return await Collect<T>(options,null,project=>rows.Project(project,cancellation.Token),cancellation.Token,rows.RecordLimits).ConfigureAwait(false);
    }
    public static Task<NativeResult<TResult>> QueryRecordsBufferedAsync<TArguments,TResult,TContract>(
        this NativeQueryClient client,string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(client);
        return Collect<TResult>(options,null,project=>client.QueryRecordsProjectedAsync<TArguments,TResult,bool>(
            moduleId,queryId,database,schemaFingerprint,arguments,null,project,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>()),cancellationToken,client.RecordLimits);
    }
    public static Task<NativeResult<TResult>> QueryRecordsBufferedAsync<TArguments,TResult,TContract>(
        this NativeTransaction transaction,string moduleId,string queryId,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(transaction);
        return Collect<TResult>(options,null,project=>transaction.ProjectRecordResult<TArguments,TResult,bool>(
            moduleId,queryId,schemaFingerprint,arguments,project,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>()),cancellationToken,transaction.RecordLimits);
    }
    public static Task<NativeResult<TResult>> QueryRecordsBufferedAsync<TArguments,TResult>(
        this NativeQueryClient client,string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(client);
        return Collect<TResult>(options,null,project=>client.QueryRecordsProjectedAsync<TArguments,TResult,bool>(
            moduleId,queryId,database,schemaFingerprint,arguments,null,project,cancellationToken),cancellationToken,client.RecordLimits);
    }
    public static Task<NativeResult<TResult>> QueryRecordsBufferedAsync<TArguments,TResult>(
        this NativeTransaction transaction,string moduleId,string queryId,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(transaction);
        return Collect<TResult>(options,null,project=>transaction.ProjectRecordResult<TArguments,TResult,bool>(
            moduleId,queryId,schemaFingerprint,arguments,project,cancellationToken),cancellationToken,transaction.RecordLimits);
    }
    public static Task<NativeResult<TResult>> QueryBufferedAsync<TArguments,TResult>(
        this NativeQueryClient client,string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(client);
        return Collect<TResult>(options,null,project=>client.QueryProjectedAsync<TArguments,TResult,bool>(
            moduleId,queryId,database,schemaFingerprint,arguments,null,project,cancellationToken),cancellationToken);
    }

    public static Task<NativeResult<TResult>> QueryBufferedAsync<TArguments,TResult>(
        this NativeTransaction transaction,string moduleId,string queryId,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(transaction);
        return Collect<TResult>(options,null,project=>transaction.ProjectResult<TArguments,TResult,bool>(
            moduleId,queryId,schemaFingerprint,arguments,project,cancellationToken),cancellationToken);
    }

    public static Task<NativeResult<TResult>> QueryGeneratedBufferedAsync<TArguments,TResult>(
        this NativeQueryClient client,string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default) where TResult:INativeReplayDecoder<TResult>{
        ArgumentNullException.ThrowIfNull(client);
        return Collect<TResult>(options,TResult.Decode,project=>client.QueryProjectedAsync<TArguments,TResult,bool>(
            moduleId,queryId,database,schemaFingerprint,arguments,null,project,cancellationToken),cancellationToken);
    }

    public static Task<NativeResult<TResult>> QueryGeneratedBufferedAsync<TArguments,TResult>(
        this NativeTransaction transaction,string moduleId,string queryId,string schemaFingerprint,TArguments arguments,
        NativeResultOptions options,CancellationToken cancellationToken=default) where TResult:INativeReplayDecoder<TResult>{
        ArgumentNullException.ThrowIfNull(transaction);
        return Collect<TResult>(options,TResult.Decode,project=>transaction.ProjectResult<TArguments,TResult,bool>(
            moduleId,queryId,schemaFingerprint,arguments,project,cancellationToken),cancellationToken);
    }

    private static async Task<NativeResult<T>> Collect<T>(
        NativeResultOptions options,Func<ReadOnlySpan<byte>,T>? decode,Func<Func<ReadOnlySpan<byte>,bool>,IAsyncEnumerable<bool>> receive,
        CancellationToken cancellationToken,global::Seamless.Execution.Codecs.RecordGraphValidationLimits? recordLimits=null){
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MemoryThresholdBytes);
        cancellationToken.ThrowIfCancellationRequested();
        if(!BitConverter.IsLittleEndian){throw new PlatformNotSupportedException("Native result replay requires little-endian byte order.");}

        NativeResult<T>? result=null;
        Action<ReadOnlySpan<byte>> validate;
        if(recordLimits is not null){
            decode=payload=>NativeRecordCodec<T>.Deserialize(payload,result!.Budget,limits:recordLimits);
            validate=payload=>NativeRecordCodec<T>.Validate(payload,result!.Budget,cancellationToken,recordLimits);
        }else{
            decode??=ScalarDecoder<T>();var validator=NativeWireValidation.For<T>();validate=payload=>validator.Validate(payload);
        }
        result=new NativeResult<T>(options,decode);
        try{
            await foreach(var _ in receive(payload=>{
                validate(payload);
                result.Storage.Append(payload);
                return true;
            }).ConfigureAwait(false)){}
            result.Storage.Complete();
            return result;
        }catch{
            result.Dispose();
            throw;
        }
    }

    private static Func<ReadOnlySpan<byte>,T> ScalarDecoder<T>(){
        if(typeof(T)!=typeof(int)&&typeof(T)!=typeof(long)&&typeof(T)!=typeof(string)){
            throw new NotSupportedException("No verified native result replay decoder is available for this type.");
        }
        return Decode<T>;
    }

    private static T Decode<T>(ReadOnlySpan<byte> payload){
        // 受信時に検証済み。後から登録されたformatterで保存済みの行の意味を変えない。
        if(typeof(T)==typeof(int)){
            var value=BinaryPrimitives.ReadInt32LittleEndian(payload);
            return Unsafe.As<int,T>(ref value);
        }
        if(typeof(T)==typeof(long)){
            var value=BinaryPrimitives.ReadInt64LittleEndian(payload);
            return Unsafe.As<long,T>(ref value);
        }
        var header=BinaryPrimitives.ReadInt32LittleEndian(payload);
        var text=header switch{
            -1=>null,
            0=>string.Empty,
            >0=>new string(MemoryMarshal.Cast<byte,char>(payload[4..])),
            _=>Encoding.UTF8.GetString(payload[8..])
        };
        return (T)(object?)text!;
    }
}
