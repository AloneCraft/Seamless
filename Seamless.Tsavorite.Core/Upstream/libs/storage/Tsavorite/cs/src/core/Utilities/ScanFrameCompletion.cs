// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Threading;
namespace Tsavorite.core;

// frame毎の一回のload。eventの状態と、最後の資源accessの終了を別に所有する。
internal sealed class ScanFrameCompletion{
    private readonly object gate=new();
    private readonly Dictionary<int,int> waiterThreads=[];
    private readonly Dictionary<int,int> nestedThreads=[];
    private readonly Action terminalAccounting;
    private readonly ScanFrameCompletion parentFrame;
    private readonly object issuerOwner;
    private int deferredOwner;
    private int issuerThread;
    private int callbackThread;
    private int waitAccesses;
    private int nestedAccesses;
    private bool trustedForwarder;
    private bool trustedPreparationFailure;
    private bool issuerReturned;
    private bool mayHaveEscaped;
    private bool callbackDebt=true;
    private bool callbackStarted;
    private bool nestedResourcesUnresolved;
    private bool outerCallback;
    private bool finalCallbackStarted;
    private bool terminalClaimed;
    private bool terminalPublished;
    private bool sealedForReuse;
    private bool poisoned;
    private Exception failure;
    private List<Exception> secondaryFailures;
    internal long Frame{get;}
    internal long Claim{get;}
    internal CountdownEvent Event{get;}
    internal CancellationTokenSource Cancellation{get;}
    internal CircularDiskReadBuffer ReadBuffers{get;}
    internal ScanFrameCompletion(long frame,long claim,CountdownEvent completion,CancellationTokenSource cancellation,CircularDiskReadBuffer readBuffers,Action terminalAccounting,ScanFrameCompletion parentFrame=null,object issuerOwner=null){
        this.Frame=frame;
        this.Claim=claim;
        this.Event=completion;
        this.Cancellation=cancellation;
        this.ReadBuffers=readBuffers;
        this.terminalAccounting=terminalAccounting;
        this.parentFrame=parentFrame;
        this.issuerOwner=issuerOwner;
    }
    internal bool IssuerReturned{get{lock(this.gate){return this.issuerReturned;}}}
    internal bool TerminalPublished{get{lock(this.gate){return this.terminalPublished;}}}
    internal bool ResourcesJoined{get{lock(this.gate){return this.terminalPublished&&!this.poisoned;}}}
    internal Exception Failure{get{lock(this.gate){return this.failure;}}}
    internal bool HasCurrentThreadAccess{
        get{lock(this.gate){return this.CurrentThreadOwnsAccess();}}
    }
    private bool CurrentThreadOwnsAccess(){
        var thread=Environment.CurrentManagedThreadId;
        return this.issuerThread==thread||this.callbackThread==thread||this.waiterThreads.ContainsKey(thread)||this.nestedThreads.ContainsKey(thread);
    }
    internal void AssertNoSelfJoin(){
        lock(this.gate){if(this.CurrentThreadOwnsAccess()){throw new InvalidOperationException("Reentrant same-frame join would wait for itself.");}}
    }
    internal bool TryOwnDeferredAction()=>this.TryOwnIssuer(1);
    internal bool TryWithdrawBeforeAction()=>this.TryOwnIssuer(2);
    private bool TryOwnIssuer(int owner){
        lock(this.gate){
            if(this.deferredOwner!=0){return false;}
            this.deferredOwner=owner;
            this.issuerThread=Environment.CurrentManagedThreadId;
            return true;
        }
    }
    // source監査済みの既定forwarderだけがarmする。未知overrideがbase helperへ委譲しただけではarmしない。
    internal void TrustSourceAuditedForwarder(object owner){
        lock(this.gate){this.RequireIssuer();if(owner is not null&&ReferenceEquals(owner,this.issuerOwner)){this.trustedForwarder=true;}}
    }
    internal void BindPreparedResources(CountdownEvent completion,CancellationTokenSource cancellation,CircularDiskReadBuffer readBuffers){
        lock(this.gate){
            this.RequireIssuer();
            if(!ReferenceEquals(this.Event,completion)||!ReferenceEquals(this.Cancellation,cancellation)||!ReferenceEquals(this.ReadBuffers,readBuffers)){
                throw this.Violation("Frame resource binding changed during preparation.");
            }
        }
    }
    internal void CertifyTrustedPreparationFailure(){
        lock(this.gate){
            this.RequireIssuer();
            if(this.trustedForwarder&&!this.mayHaveEscaped&&!this.callbackStarted){this.trustedPreparationFailure=true;}
        }
    }
    internal void MarkMayHaveEscaped(){lock(this.gate){this.RequireIssuer();this.mayHaveEscaped=true;}}
    // Cancel/logger/失敗公開の最後accessが終わった後だけ呼ぶ。marker欠如だけではdebtを消さない。
    internal bool CommitTrustedUnissued(){
        lock(this.gate){
            this.RequireIssuer();
            if(this.mayHaveEscaped||this.callbackStarted||!this.callbackDebt){return false;}
            if(this.deferredOwner!=2&&!(this.trustedForwarder&&this.trustedPreparationFailure)){return false;}
            this.callbackDebt=false;
            return true;
        }
    }
    internal void RecordFailure(Exception error){
        if(error is null){return;}
        Exception primary;
        lock(this.gate){
            primary=this.failure;
            if(primary is null){this.failure=error;return;}
            if(ReferenceEquals(primary,error)){return;}
            if(this.secondaryFailures is null){this.secondaryFailures=[];}
            foreach(var previous in this.secondaryFailures){if(ReferenceEquals(previous,error)){return;}}
            this.secondaryFailures.Add(error);
        }
        RecoveryIoScope.AttachCleanupError(primary,error);
    }
    internal void BeginOuterCallback(){
        lock(this.gate){this.ClaimCallback();this.outerCallback=true;}
    }
    internal void VerifyOuterCallbackResources(CountdownEvent completion,CancellationTokenSource cancellation,CircularDiskReadBuffer readBuffers){
        lock(this.gate){
            if(!ReferenceEquals(this.Event,completion)||!ReferenceEquals(this.Cancellation,cancellation)||!ReferenceEquals(this.ReadBuffers,readBuffers)){
                throw this.Violation("Object callback resource binding differs from its frame operation.");
            }
        }
    }
    // Object前段が所有中なら最終通知だけclaimし、外側finallyに資源終端を残す。
    internal bool BeginFinalCallback(CountdownEvent completion,CancellationTokenSource cancellation){
        lock(this.gate){
            if(this.finalCallbackStarted){throw this.Violation("Frame final callback was delivered more than once.");}
            var ownsCallback=this.callbackThread!=Environment.CurrentManagedThreadId||!this.outerCallback;
            if(ownsCallback){this.ClaimCallback();}
            if(!ReferenceEquals(this.Event,completion)||!ReferenceEquals(this.Cancellation,cancellation)){
                throw this.Violation("Callback resource binding differs from its frame operation.");
            }
            this.finalCallbackStarted=true;
            return ownsCallback;
        }
    }
    private void ClaimCallback(){
        // 実callbackそのものがescapeの証拠。未知overrideのmarker欠如を拒否理由にしない。
        if(this.callbackStarted||!this.callbackDebt||this.sealedForReuse){throw this.Violation("Unexpected or duplicate frame callback.");}
        this.callbackStarted=true;
        this.mayHaveEscaped=true;
        this.callbackThread=Environment.CurrentManagedThreadId;
        this.parentFrame?.BeginNestedAccess();
    }
    internal void EndIssuer(Exception error){
        this.RecordFailure(error);
        bool publish;
        lock(this.gate){
            this.RequireIssuer();
            this.issuerReturned=true;
            this.issuerThread=0;
            publish=this.TakeTerminalAccounting();
            Monitor.PulseAll(this.gate);
        }
        if(publish){this.PublishTerminal();}
    }
    internal void EndCallback(){
        bool publish;
        lock(this.gate){
            if(this.callbackThread!=Environment.CurrentManagedThreadId){throw this.Violation("Frame callback owner mismatch.");}
            this.callbackThread=0;
            this.callbackDebt=this.nestedResourcesUnresolved;
            publish=this.TakeTerminalAccounting();
        }
        // 下位callbackの最後access後、child terminal公開前にparentの自己join追跡を返す。
        this.parentFrame?.EndNestedAccess();
        if(publish){this.PublishTerminal();}
    }
    private void BeginNestedAccess(){
        lock(this.gate){
            var thread=Environment.CurrentManagedThreadId;
            this.nestedThreads.TryGetValue(thread,out var count);
            this.nestedThreads[thread]=count+1;
            this.nestedAccesses++;
        }
    }
    private void EndNestedAccess(){
        bool publish;
        lock(this.gate){
            var thread=Environment.CurrentManagedThreadId;
            if(!this.nestedThreads.TryGetValue(thread,out var count)){throw this.Violation("Nested callback owner mismatch.");}
            if(count==1){this.nestedThreads.Remove(thread);}else{this.nestedThreads[thread]=count-1;}
            this.nestedAccesses--;
            publish=this.TakeTerminalAccounting();
            Monitor.PulseAll(this.gate);
        }
        if(publish){this.PublishTerminal();}
    }
    internal void RetainNestedResourceDebt(){
        lock(this.gate){
            if(this.callbackThread!=Environment.CurrentManagedThreadId){throw this.Violation("Nested resource debt owner mismatch.");}
            this.nestedResourcesUnresolved=true;
        }
    }
    private bool TakeTerminalAccounting(){
        if(this.issuerReturned&&!this.callbackDebt&&this.callbackThread==0&&this.nestedAccesses==0&&!this.terminalClaimed&&!this.poisoned){
            this.terminalClaimed=true;
            return true;
        }
        return false;
    }
    private void PublishTerminal(){
        // このdelegateはiterator自身のcounter減算だけ。外部observer/loggerを間に置かない。
        this.terminalAccounting();
        lock(this.gate){this.terminalPublished=true;Monitor.PulseAll(this.gate);}
    }
    internal bool TryBeginWaitAccess(){
        lock(this.gate){
            if(this.CurrentThreadOwnsAccess()){throw new InvalidOperationException("Reentrant same-frame wait would wait for itself.");}
            if(this.sealedForReuse){return false;}
            var thread=Environment.CurrentManagedThreadId;
            this.waiterThreads.TryGetValue(thread,out var nested);
            this.waiterThreads[thread]=nested+1;
            this.waitAccesses++;
            return true;
        }
    }
    internal void EndWaitAccess(){
        lock(this.gate){
            var thread=Environment.CurrentManagedThreadId;
            if(!this.waiterThreads.TryGetValue(thread,out var nested)){throw this.Violation("Frame wait owner mismatch.");}
            if(nested==1){this.waiterThreads.Remove(thread);}else{this.waiterThreads[thread]=nested-1;}
            this.waitAccesses--;
            Monitor.PulseAll(this.gate);
        }
    }
    internal void WaitForTerminal(Action report=null){
        while(true){
            bool reportWait;
            lock(this.gate){
                if(this.terminalPublished){return;}
                if(this.poisoned){throw new InvalidOperationException("Frame protocol violation remains unresolved.");}
                reportWait=!Monitor.Wait(this.gate,15_000);
            }
            // 既存間隔のstall診断だけ。時間経過で所有解放/成功にしない。
            if(reportWait){report?.Invoke();}
        }
    }
    internal void JoinAndSeal(){
        lock(this.gate){
            if(this.CurrentThreadOwnsAccess()){throw new InvalidOperationException("Reentrant same-frame join would wait for itself.");}
            if(this.poisoned){throw new InvalidOperationException("Frame protocol violation remains unresolved.");}
            while(!this.terminalPublished||this.waitAccesses!=0){
                if(this.poisoned){throw new InvalidOperationException("Frame protocol violation remains unresolved.");}
                Monitor.Wait(this.gate);
            }
            this.sealedForReuse=true;
        }
    }
    private void RequireIssuer(){
        if(this.issuerThread!=Environment.CurrentManagedThreadId||this.issuerReturned){throw this.Violation("Frame issuer owner mismatch.");}
    }
    private Exception Violation(string message){
        this.poisoned=true;
        Monitor.PulseAll(this.gate);
        return new InvalidOperationException(message);
    }
}
