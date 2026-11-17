using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

public sealed partial class NativeQueryClient{
    public ResourceBudget RecordBudget{get;init;}=NativeRecordResources.DefaultBudget;
    public RecordGraphValidationLimits RecordLimits{get;init;}=RecordGraphValidationLimits.Default;
    public NativeResultStream<TResult> QueryRecordsStream<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.RecordStream<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken);
    public NativeResultStream<TResult> QueryRecordsStream<TArguments,TResult,TContract>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.RecordStream<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>());
    internal NativeResultStream<TResult> RecordStream<TArguments,TResult>(string moduleId,string queryId,string database,string fingerprint,TArguments arguments,NativeTransaction? transaction,CancellationToken cancellationToken,NativeRecordQueryContract? contract=null)=>
        new((project,token)=>this.QueryRecordsProjectedAsync<TArguments,TResult,bool>(moduleId,queryId,database,fingerprint,arguments,transaction,project,token,contract),
            (payload,token)=>NativeRecordCodec<TResult>.Deserialize(payload,this.RecordBudget,token,this.RecordLimits),this.RecordLimits,cancellationToken);
    public IAsyncEnumerable<TResult> QueryRecordsAsync<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.QueryRecordsCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken);
    public IAsyncEnumerable<TResult> QueryRecordsAsync<TArguments,TResult,TContract>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.QueryRecordsCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>());
    internal IAsyncEnumerable<TResult> QueryRecordsCore<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,NativeTransaction? transaction,CancellationToken cancellationToken,NativeRecordQueryContract? contract=null)=>
        this.QueryRecordsProjectedAsync<TArguments,TResult,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,transaction,
            payload=>NativeRecordCodec<TResult>.Deserialize(payload,this.RecordBudget,cancellationToken,this.RecordLimits),cancellationToken,contract);
    internal IAsyncEnumerable<TOutput> QueryRecordsProjectedAsync<TArguments,TResult,TOutput>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,NativeTransaction? transaction,Func<ReadOnlySpan<byte>,TOutput> projectRow,CancellationToken cancellationToken,NativeRecordQueryContract? contract=null)=>
        this.QueryProjectedAsync<TArguments,TResult,TOutput>(moduleId,queryId,database,schemaFingerprint,arguments,transaction,projectRow,cancellationToken,contract??NativeRecordQueryContract.For<TArguments,TResult>());
    public Task<TResult> ExecuteRecordScalarAsync<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        RecordScalar(this.QueryRecordsCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken));
    public Task<TResult> ExecuteRecordScalarAsync<TArguments,TResult,TContract>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        RecordScalar(this.QueryRecordsAsync<TArguments,TResult,TContract>(moduleId,queryId,database,schemaFingerprint,arguments,cancellationToken));
    internal static async Task<T> RecordScalar<T>(IAsyncEnumerable<T> rows){
        var found=false;T result=default!;
        await foreach(var row in rows.ConfigureAwait(false)){if(found){throw new InvalidDataException("A scalar query returned multiple rows.");}result=row;found=true;}
        if(!found){throw new InvalidDataException("A scalar query returned no rows.");}return result;
    }
}

public sealed partial class NativeTransaction{
    internal RecordGraphValidationLimits RecordLimits=>this.client.RecordLimits;
    public NativeResultStream<TResult> QueryRecordsStream<TArguments,TResult>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.client.RecordStream<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken);
    public NativeResultStream<TResult> QueryRecordsStream<TArguments,TResult,TContract>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.client.RecordStream<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>());
    public IAsyncEnumerable<TResult> QueryRecordsAsync<TArguments,TResult>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.client.QueryRecordsCore<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken);
    public IAsyncEnumerable<TResult> QueryRecordsAsync<TArguments,TResult,TContract>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        this.client.QueryRecordsCore<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken,NativeRecordQueryContract.For<TArguments,TResult,TContract>());
    public Task<TResult> ExecuteRecordScalarAsync<TArguments,TResult>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        NativeQueryClient.RecordScalar(this.QueryRecordsAsync<TArguments,TResult>(moduleId,queryId,schemaFingerprint,arguments,cancellationToken));
    public Task<TResult> ExecuteRecordScalarAsync<TArguments,TResult,TContract>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>
        NativeQueryClient.RecordScalar(this.QueryRecordsAsync<TArguments,TResult,TContract>(moduleId,queryId,schemaFingerprint,arguments,cancellationToken));
    internal IAsyncEnumerable<TOutput> ProjectRecordResult<TArguments,TResult,TOutput>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,Func<ReadOnlySpan<byte>,TOutput> projectRow,CancellationToken cancellationToken,NativeRecordQueryContract? contract=null)=>
        this.client.QueryRecordsProjectedAsync<TArguments,TResult,TOutput>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,projectRow,cancellationToken,contract);
}
