using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Seamless.Database;

namespace Seamless.Management.Server;

internal sealed partial class NativeRuntime{
    internal async Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,string admittedId,CancellationToken cancellation,Action? authorize=null){
        if(string.IsNullOrWhiteSpace(request.Database)||string.IsNullOrWhiteSpace(request.ObjectId)||request.Arguments is null||request.Limit is <1 or >MaximumRowBatch){
            throw new ManagementException("InvalidRequest","DB、問い合わせ、引数、1〜1000行の表示上限を指定してください。");
        }
        if(request.MeasurementMode is not (ObjectExecutionMeasurementMode.None or ObjectExecutionMeasurementMode.Summary or ObjectExecutionMeasurementMode.Profiled)){
            throw new ManagementException("InvalidRequest","対応する計測モードを指定してください。");
        }
        request.TraceOptions?.Validate();
        request.ResourceHeatOptions?.Validate();
        var responseBytes=MaximumRowResponseBytes;
        if(request.ResourceHeatOptions is not null){responseBytes-=QueryResourceCapture.MaximumResponseBytes;}
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){responseBytes-=ObjectExecutionProfiling.MaximumResponseBytes;}
        if(request.Arguments.Sum(value=>(long)Encoding.UTF8.GetByteCount(value.GetRawText()))>MaximumRowResponseBytes){
            throw new ManagementException("Limit","引数が要求上限を超えています。");
        }
        ObjectExecutionMeasurement? measurement=null;
        QueryTraceRecording? recording=null;
        QueryTraceCapture? trace=null;
        IDisposable? sampling=null;
        QueryResourceCapture? resources=null;
        ResourceHeatTrace? resourceHeat=null;
        Seamless.Execution.QueryProfiler? profiler=null;
        ObjectExecutionProfile? profile=null;
        var gate=this.Gate("db:"+admittedId);
        var acquired=false;
        DefinitionVersion? observedVersion=null;
        QueryExecutionObservation? observation=null;
        ObjectExecutionResult? response=null;
        Exception? failure=null;
        try{
            if(request.TraceOptions is{} traceOptions){
                var recorder=this.TraceRecorder??throw new ManagementException("Unsupported","トレース記録を利用できません。");
                recording=recorder.Prepare(traceOptions,cancellation);
            }
            if(request.MeasurementMode!=ObjectExecutionMeasurementMode.None||recording is not null||request.ResourceHeatOptions is not null){measurement=new();}
            if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){profiler=Seamless.Execution.QueryProfiler.Begin(this.store.ResourceBudget,measurement!.ExecutionId,ObjectExecutionProfiling.MaximumRegions);}
            if(request.ResourceHeatOptions is{} resourceOptions){resources=new(resourceOptions,this.store.ResourceBudget,cancellation);}
            if(recording is not null){
                var recorder=this.TraceRecorder!;
                trace=new(measurement!.ExecutionId,admittedId,request.ObjectId,request.TraceOptions!,measurement.StartedAtUtc,measurement.StartedTimestamp,
                    recording.Record,recorder.ReportLateSamples,recorder.RecordObservationFailure,cancellation);
            }
            trace?.Started();
            if(trace is not null&&request.TraceOptions!.RecordMode==QueryTraceRecordMode.TimeSampled){sampling=this.TraceSampler?.Register(trace);}
            measurement?.WaitingForGate();await gate.WaitAsync(cancellation).ConfigureAwait(false);acquired=true;measurement?.EnteredGate();
            authorize?.Invoke();
            trace?.EnteredGate();
            var version=this.RowEditingVersion(request.Database,request.ExpectedVersion,admittedId);observedVersion=version;
            trace?.Bound(version.Version,version.ArtifactHash);
            var database=this.Open(version).Database;
            var descriptor=NativeCatalog.Read(database.GetType(),version).SingleOrDefault(item=>item.Details.Object.Id==request.ObjectId)?.Details;
            if(descriptor is null||descriptor.Object.Kind is not ("View" or "ScalarFunction" or "TableFunction")){
                throw new ManagementException("NotFound","公開された問い合わせがありません: "+request.ObjectId);
            }
            object owner=database;
            var parts=request.ObjectId.Split('.');
            foreach(var part in parts[..^1]){
                var property=owner.GetType().GetProperty(part,BindingFlags.Public|BindingFlags.Instance);
                if(property is null||!typeof(DatabaseSchema).IsAssignableFrom(property.PropertyType)){throw new ManagementException("NotFound","スキーマがありません。");}
                owner=property.GetValue(owner)??throw new ManagementException("NotFound","スキーマがありません。");
            }
            using var read=database.BeginReadSession(cancellation);
            trace?.Invoking();
            object? result;
            Type resultType;
            var member=owner.GetType().GetProperty(parts[^1],BindingFlags.Public|BindingFlags.Instance);
            if(member is not null&&request.Arguments.Length==0){result=member.GetValue(owner);resultType=member.PropertyType;}
            else{
                var method=owner.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).SingleOrDefault(method=>method.Name==parts[^1]&&!method.ContainsGenericParameters&&!method.IsSpecialName)
                    ??throw new ManagementException("NotFound","関数がありません。");
                var parameters=method.GetParameters();
                if(parameters.Length!=request.Arguments.Length){throw new ManagementException("InvalidRequest","引数の数が一致しません。");}
                var arguments=parameters.Select((parameter,index)=>RuntimeValueJson.Deserialize(request.Arguments[index],parameter.ParameterType)).ToArray();
                cancellation.ThrowIfCancellationRequested();
                result=method.Invoke(owner,arguments);resultType=method.ReturnType;
            }
            if(descriptor.Object.Kind=="ScalarFunction"){
                var output=resultType.GetProperty("Value")!.GetValue(result);measurement?.ObservedOutput();trace?.Output();
                var value=RuntimeValueJson.Serialize(output);
                if(Encoding.UTF8.GetByteCount(value.GetRawText())>responseBytes){throw new ManagementException("Limit","戻り値が応答上限を超えています。");}
                response=new(version.Version,false,value,[],[],false);
            }else{
                if(result is not IEnumerable sequence){throw new ManagementException("Validation","問い合わせの戻り値を列挙できません。");}
                var rows=new List<JsonElement>();var bytes=0;var more=false;
                var enumerator=sequence.GetEnumerator();
                try{
                    while(enumerator.MoveNext()){
                        cancellation.ThrowIfCancellationRequested();measurement?.ObservedOutput();trace?.Output();
                        if(rows.Count==request.Limit){more=true;break;}
                        var row=RuntimeValueJson.Serialize(enumerator.Current);
                        var size=Encoding.UTF8.GetByteCount(row.GetRawText())+256;
                        if(size>responseBytes-bytes){
                            if(rows.Count==0){throw new ManagementException("Limit","1行の表示データが応答上限を超えています。");}
                            more=true;break;
                        }
                        rows.Add(row);bytes+=size;
                    }
                }finally{(enumerator as IDisposable)?.Dispose();}
                var rowType=resultType.GetGenericArguments()[^1];
                response=new(version.Version,true,null,rows.ToArray(),RuntimeValueJson.Columns(rowType),more);
            }
            authorize?.Invoke();
        }catch(TargetInvocationException exception) when(exception.InnerException is not null){
            failure=exception.InnerException;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();throw;
        }catch(Exception exception){failure=exception;throw;}
        finally{
            try{
                try{if(profiler is not null&&failure is null&&observedVersion is not null){profile=ObjectExecutionProfiling.Complete(profiler,measurement!,admittedId,request.ObjectId,observedVersion);}}
                catch(Exception error){failure=error;throw;}
                finally{profiler?.Dispose();}
            }finally{
                trace?.StopSampling();
                if(acquired){gate.Release();}
                try{if(resources is not null&&measurement is not null){resourceHeat=this.PublishResourceHeat(resources.Complete(measurement,admittedId,request.ObjectId,observedVersion));}}
                finally{resources?.Dispose();}
                if(measurement is not null){
                    observation=measurement.Complete(request,admittedId,observedVersion,response,failure);
                    trace?.End(observation,this.TraceRecorder!.StoppingToken);
                    try{this.ExecutionObserved?.Invoke(observation);}catch(Exception){Interlocked.Increment(ref this.executionObservationFailures);}
                }
                sampling?.Dispose();recording?.Dispose();
            }
        }
        if(observation is not null&&request.MeasurementMode!=ObjectExecutionMeasurementMode.None){response=response! with{Statistics=ObjectExecutionMeasurement.Statistics(observation)};}
        if(trace is not null){response=response! with{TraceExecutionId=measurement!.ExecutionId};}
        if(resourceHeat is not null){response=response! with{ResourceHeat=resourceHeat};}
        if(profile is not null){response=response! with{Profile=profile};}
        return response!;
    }
}
