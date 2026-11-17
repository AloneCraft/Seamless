using System.Reflection;
using System.Text.Json;

namespace Seamless.Management.Server;

internal sealed partial class NativeRuntime{
    internal const int FunctionResponseByteLimit=MaximumRowResponseBytes;
    internal FunctionExecutionCapture PrepareFunctionExecution(FunctionExecutionRequest request,string database,Func<bool,ObjectExecutionResult> snapshot,Action authorize,CancellationToken cancellation)=>new(this,request,database,snapshot,authorize,cancellation);
    internal IEnumerable<ExecutionRow> ExecuteFunction(Definition definition,ExecuteRequest request,UserAccount user,FunctionExecutionCapture capture,CancellationToken cancellation){
        using var iterator=this.ExecuteCore(definition,request,user,cancellation,null,capture).GetEnumerator();
        while(true){
            bool more;
            try{more=iterator.MoveNext();}
            catch(TargetInvocationException error)when(error.InnerException is not null){capture.Fail(error.InnerException);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
            catch(Exception error){capture.Fail(error);throw;}
            if(!more){break;}yield return iterator.Current;
        }
    }
    internal sealed class FunctionExecutionCapture:IDisposable{
        private readonly NativeRuntime owner;
        private readonly FunctionExecutionRequest request;
        private readonly ObjectExecutionRequest measurementRequest;
        private readonly Func<bool,ObjectExecutionResult> snapshot;
        private readonly Action authorize;
        private readonly CancellationToken cancellation;
        private QueryTraceRecording? recording;
        private QueryTraceCapture? trace;
        private IDisposable? sampling;
        private ObjectExecutionMeasurement? measurement;
        private QueryResourceCapture? resources;
        private Seamless.Execution.QueryProfiler? profiler;
        private DefinitionVersion? version;
        private Exception? failure;
        private bool ended;
        internal bool HasMore{get;set;}
        internal bool IsSequence{get;set;}
        internal ObjectExecutionStatistics? Statistics{get;private set;}
        internal Guid? TraceExecutionId{get;private set;}
        internal ResourceHeatTrace? ResourceHeat{get;private set;}
        internal ObjectExecutionProfile? Profile{get;private set;}
        internal FunctionExecutionCapture(NativeRuntime owner,FunctionExecutionRequest request,string database,Func<bool,ObjectExecutionResult> snapshot,Action authorize,CancellationToken cancellation){
            this.owner=owner;this.request=request;this.snapshot=snapshot;this.authorize=authorize;this.cancellation=cancellation;
            this.measurementRequest=new(database,request.Target.ObjectId,request.Arguments,request.Limit){MeasurementMode=request.MeasurementMode,TraceOptions=request.TraceOptions,ResourceHeatOptions=request.ResourceHeatOptions};
            if(request.TraceOptions is{} options){this.recording=(owner.TraceRecorder??throw new ManagementException("Unsupported","トレース記録を利用できません。")).Prepare(options,cancellation);}
        }
        internal void Begin(){
            if(this.request.MeasurementMode!=ObjectExecutionMeasurementMode.None||this.recording is not null||this.request.ResourceHeatOptions is not null){this.measurement=new();}
            if(this.request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.profiler=Seamless.Execution.QueryProfiler.Begin(this.owner.store.ResourceBudget,this.measurement!.ExecutionId,ObjectExecutionProfiling.MaximumRegions);}
            if(this.request.ResourceHeatOptions is{} options){this.resources=new(options,this.owner.store.ResourceBudget,this.cancellation);}
            if(this.recording is null){return;}
            var recorder=this.owner.TraceRecorder??throw new ManagementException("Unsupported","トレース記録を利用できません。");
            this.trace=new(this.measurement!.ExecutionId,this.request.Target.DefinitionId,this.request.Target.ObjectId,this.request.TraceOptions!,this.measurement.StartedAtUtc,this.measurement.StartedTimestamp,
                this.recording.Record,recorder.ReportLateSamples,recorder.RecordObservationFailure,this.cancellation);
            this.trace.Started();this.TraceExecutionId=this.measurement.ExecutionId;
            if(this.request.TraceOptions!.RecordMode==QueryTraceRecordMode.TimeSampled){this.sampling=this.owner.TraceSampler?.Register(this.trace);}
        }
        internal void EnteredGate(){this.measurement?.EnteredGate();this.trace?.EnteredGate();this.authorize();}
        internal void WaitingForGate()=>this.measurement?.WaitingForGate();
        internal void Bound(DefinitionVersion value){this.version=value;this.trace?.Bound(value.Version,value.ArtifactHash);}
        internal void Invoking(){if(this.request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.authorize();}this.trace?.Invoking();}
        internal void Output(){this.measurement?.ObservedOutput();this.trace?.Output();}
        internal void BeforeCompletion(){
            // 確定前に列メタデータを含む実際のJSONサイズを検査する。実測欄とRPC envelopeの余白も確保する。
            this.cancellation.ThrowIfCancellationRequested();
            if(this.request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.authorize();}
            var maximum=FunctionResponseByteLimit-16384;if(this.request.ResourceHeatOptions is not null){maximum-=QueryResourceCapture.MaximumResponseBytes;}
            if(this.profiler is not null){this.Profile=ObjectExecutionProfiling.Complete(this.profiler,this.measurement!,this.request.Target.DefinitionId,this.request.Target.ObjectId,this.version!);}
            using var budget=new ResponseBudgetStream(maximum,this.cancellation);
            JsonSerializer.Serialize(budget,this.snapshot(this.IsSequence) with{Profile=this.Profile},ManagementJson.Options);
            this.cancellation.ThrowIfCancellationRequested();
        }
        internal void Fail(Exception error)=>this.failure=error;
        internal void Complete(){
            if(this.ended){return;}this.ended=true;
            try{
                this.trace?.StopSampling();
                if(this.measurement is null){return;}
                if(this.resources is not null){this.ResourceHeat=this.owner.PublishResourceHeat(this.resources.Complete(this.measurement,this.request.Target.DefinitionId,this.request.Target.ObjectId,this.version));}
                var response=this.snapshot(this.IsSequence) with{HasMore=this.HasMore};
                var observation=this.measurement.Complete(this.measurementRequest,this.request.Target.DefinitionId,this.version,response,this.failure);
                this.trace?.End(observation,this.owner.TraceRecorder!.StoppingToken);
                try{this.owner.ExecutionObserved?.Invoke(observation);}catch(Exception){Interlocked.Increment(ref this.owner.executionObservationFailures);}
                if(this.request.MeasurementMode!=ObjectExecutionMeasurementMode.None&&observation.IdentityResolved){this.Statistics=ObjectExecutionMeasurement.Statistics(observation);}
            }finally{try{this.profiler?.Dispose();}finally{this.resources?.Dispose();this.sampling?.Dispose();this.recording?.Dispose();this.sampling=null;this.recording=null;}}
        }
        public void Dispose()=>this.Complete();
    }
    private sealed class ResponseBudgetStream(long maximum,CancellationToken cancellation):Stream{
        private long written;
        public override bool CanRead=>false;
        public override bool CanSeek=>false;
        public override bool CanWrite=>true;
        public override long Length=>this.written;
        public override long Position{get=>this.written;set=>throw new NotSupportedException();}
        public override void Flush(){}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count){
            cancellation.ThrowIfCancellationRequested();
            if(count<0||count>maximum-this.written){throw new ManagementException("Limit","関数の応答全体が上限を超えたため、処理を取り消しました。");}
            this.written+=count;
        }
    }
}
