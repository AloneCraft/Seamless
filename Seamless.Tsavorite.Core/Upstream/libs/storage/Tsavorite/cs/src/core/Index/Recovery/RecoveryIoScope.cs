// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// 回復時だけの所有候補。公開APIは追加しない。
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Tsavorite.core;

internal sealed class RecoveryIoScope{
    private readonly object gate=new();
    private readonly Dictionary<IDevice,IDevice> devices=new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object,ContextState> contexts=new(ReferenceEqualityComparer.Instance);
    private readonly List<Exception> errors=[];
    private readonly TaskCompletionSource drained=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stoppedSignal=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long pending=1;
    private bool stopped;
    private sealed class ContextState{
        internal long Pending;
        internal readonly List<Action> Retired=[];
    }
    internal Task ProducerStopped=>this.stoppedSignal.Task;
    internal long Outstanding{get{lock(this.gate){if(this.stopped){return this.pending;}else{return this.pending-1;}}}}
    internal IDevice Wrap(IDevice inner){
        if(inner is null){return null;}
        if(inner is BorrowedDevice same&&ReferenceEquals(same.Scope,this)){return same;}
        lock(this.gate){
            if(this.devices.TryGetValue(inner,out var wrapper)){return wrapper;}
            wrapper=new BorrowedDevice(this,inner);
            this.devices.Add(inner,wrapper);
            return wrapper;
        }
    }
    private Lease Begin(object context,bool trustedSubmission){
        lock(this.gate){
            if(this.pending==0){throw new InvalidOperationException("Recovery issued I/O after its scope drained.");}
            var hasState=this.contexts.TryGetValue(context,out var state);
            if(!hasState){state=new ContextState();}
            var lease=new Lease(this,context,state,trustedSubmission);
            if(!hasState){this.contexts.Add(context,state);}
            state.Pending++;
            this.pending++;
            return lease;
        }
    }
    internal Task CloseAndDrainAsync(){
        lock(this.gate){
            if(!this.stopped){
                this.stopped=true;
                if(--this.pending==0){this.drained.TrySetResult();}
            }
        }
        this.stoppedSignal.TrySetResult();
        return this.drained.Task;
    }
    internal void RecordCleanupError(Exception error){lock(this.gate){this.errors.Add(error);}}
    internal static void AttachCleanupError(Exception primary,Exception error){
        lock(primary){
            var errors=new List<Exception>();
            if(primary.Data["Tsavorite.RecoveryCleanupErrors"] is AggregateException previous){errors.AddRange(previous.InnerExceptions);}
            errors.Add(error);
            primary.Data["Tsavorite.RecoveryCleanupErrors"]=new AggregateException("Recovery cleanup failures",errors);
        }
    }
    internal List<Exception> GetCleanupErrors(){lock(this.gate){return new List<Exception>(this.errors);}}
    private void Retire(Action action){
        try{action();}catch(Exception error){lock(this.gate){this.errors.Add(error);}}
    }
    internal void RetireContext(object context,Action dispose){
        lock(this.gate){
            if(this.contexts.TryGetValue(context,out var state)&&state.Pending!=0){state.Retired.Add(dispose);return;}
        }
        this.Retire(dispose);
    }
    private void End(object context,ContextState state){
        Action[] retired=[];
        lock(this.gate){
            if(--state.Pending==0){
                this.contexts.Remove(context);
                retired=state.Retired.ToArray();
                state.Retired.Clear();
            }
        }
        foreach(var action in retired){this.Retire(action);}
        lock(this.gate){if(--this.pending==0){this.drained.TrySetResult();}}
    }
    private sealed class Lease(RecoveryIoScope owner,object context,ContextState state,bool trustedSubmission):IRecoverySubmission{
        private const int MayHaveEscaped=1;
        private const int CallbackStarted=2;
        private const int CompensationClaimed=4;
        private int submissionState=InitialState(trustedSubmission);
        private int ended;
        private static int InitialState(bool trusted){if(trusted){return 0;}return MayHaveEscaped;}
        void IRecoverySubmission.MarkMayHaveEscaped(){Interlocked.Or(ref this.submissionState,MayHaveEscaped);}
        internal bool TryMarkCallbackStarted(){
            while(true){
                var observed=Volatile.Read(ref this.submissionState);
                if((observed&CallbackStarted)!=0){return false;}
                if(Interlocked.CompareExchange(ref this.submissionState,observed|CallbackStarted,observed)==observed){return true;}
            }
        }
        internal bool TryClaimPreSubmissionFailure()=>Interlocked.CompareExchange(ref this.submissionState,CompensationClaimed,0)==0;
        internal void End(){if(Interlocked.Exchange(ref this.ended,1)==0){owner.End(context,state);}}
    }
    internal sealed class BorrowedDevice(RecoveryIoScope scope,IDevice inner):IDevice{
        private readonly RecoverySubmissionAdapter submissionAdapter=RecoverySubmissionAdapter.TryCreate(inner);
        internal RecoveryIoScope Scope=>scope;
        private void Submit(object context,DeviceIOCompletionCallback callback,Action<DeviceIOCompletionCallback,IRecoverySubmission> issue){
            var lease=scope.Begin(context,this.submissionAdapter is not null);
            void Completed(uint error,uint bytes,object actual,Exception exception){
                if(!lease.TryMarkCallbackStarted()){
                    // 二度目はuser/contextへ触れず、Endも繰り返さない。protocol faultを黙って捨てない。
                    var protocolError=new InvalidOperationException("Recovery device completed one operation more than once.");
                    scope.RecordCleanupError(protocolError);
                    throw protocolError;
                }
                try{callback(error,bytes,actual,exception);}
                catch(Exception callbackError){
                    // callback自身のfinally/最終accessが済んでから、drain成立より前にfaultを記録する。
                    scope.RecordCleanupError(callbackError);
                    throw;
                }finally{lease.End();}
            }
            try{issue(Completed,lease);}
            catch(Exception issueError){
                // state0は、trusted経路で外部にも将来I/Oを発行できる主体にも渡していない場合だけ。
                // markerまたはcallback開始後は補償せず、実callbackのfinallyまで所有を保持する。
                if(lease.TryClaimPreSubmissionFailure()){
                    try{Completed(uint.MaxValue,0,context,issueError);}
                    // Completedで記録済み。synthetic通知のfaultで元issueErrorを置換しない。
                    catch(Exception){}
                }
                throw;
            }
        }
        public void ReadAsync(int segmentId,ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context)
            =>this.Submit(context,callback,(completed,submission)=>{
                if(this.submissionAdapter is not null){this.submissionAdapter.ReadAsync(segmentId,address,destination,length,completed,context,submission);}
                else{inner.ReadAsync(segmentId,address,destination,length,completed,context);}
            });
        public void ReadAsync(ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context)
            =>this.Submit(context,callback,(completed,submission)=>{
                if(this.submissionAdapter is not null){this.submissionAdapter.ReadAsync(address,destination,length,completed,context,submission);}
                else{inner.ReadAsync(address,destination,length,completed,context);}
            });
        public void WriteAsync(IntPtr source,int segmentId,ulong address,uint length,DeviceIOCompletionCallback callback,object context)
            =>this.Submit(context,callback,(completed,submission)=>{
                if(this.submissionAdapter is not null){this.submissionAdapter.WriteAsync(source,segmentId,address,length,completed,context,submission);}
                else{inner.WriteAsync(source,segmentId,address,length,completed,context);}
            });
        public void WriteAsync(IntPtr source,ulong address,uint length,DeviceIOCompletionCallback callback,object context)
            =>this.Submit(context,callback,(completed,submission)=>{
                if(this.submissionAdapter is not null){this.submissionAdapter.WriteAsync(source,address,length,completed,context,submission);}
                else{inner.WriteAsync(source,address,length,completed,context);}
            });
        public void Dispose(){}
        public void Initialize(long segmentSize,LightEpoch epoch=null,bool omitSegmentIdFromFilename=false)=>inner.Initialize(segmentSize,epoch,omitSegmentIdFromFilename);
        public bool TryComplete()=>inner.TryComplete();
        public bool TryCompleteMine()=>inner.TryCompleteMine();
        public bool Throttle()=>inner.Throttle();
        public uint SectorSize=>inner.SectorSize;
        public string FileName=>inner.FileName;
        public long Capacity=>inner.Capacity;
        public long SegmentSize=>inner.SegmentSize;
        public int StartSegment=>inner.StartSegment;
        public int EndSegment=>inner.EndSegment;
        public int ThrottleLimit{get=>inner.ThrottleLimit;set=>inner.ThrottleLimit=value;}
        public long GetFileSize(int segment)=>inner.GetFileSize(segment);
        public void Reset()=>inner.Reset();
        public void TruncateUntilAddressAsync(long address,AsyncCallback callback,IAsyncResult result)=>inner.TruncateUntilAddressAsync(address,callback,result);
        public void TruncateUntilAddress(long address)=>inner.TruncateUntilAddress(address);
        public void TruncateUntilSegmentAsync(int segment,AsyncCallback callback,IAsyncResult result)=>inner.TruncateUntilSegmentAsync(segment,callback,result);
        public void TruncateUntilSegment(int segment)=>inner.TruncateUntilSegment(segment);
        public void RemoveSegmentAsync(int segment,AsyncCallback callback,IAsyncResult result)=>inner.RemoveSegmentAsync(segment,callback,result);
        public void RemoveSegment(int segment)=>inner.RemoveSegment(segment);
    }
}
