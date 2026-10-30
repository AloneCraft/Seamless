// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Tsavorite.core
{
#pragma warning disable IDE0065 // Misplaced using directive
    using static LogAddress;

    /// <summary>
    /// Scan iterator for hybrid log
    /// </summary>
    public abstract class ScanIteratorBase<TAllocatorWrapper>
        where TAllocatorWrapper : IAllocator
    {
        // lifecycle gateを保持してI/O/epoch/callbackをjoinしない。
        private readonly object frameLifecycleGate=new();
        private readonly Dictionary<int,int> frameCallers=[];
        private ScanFrameCompletion[] frameOperations;
        private bool[] frameClaims;
        private bool frameClosing;
        private bool frameDisposed;
        private bool frameResetting;
        private int frameLifecycleOwner;
        private bool frameDisposeActive;
        private bool frameCloseCompleted;
        private long frameCloseReceipt;
        private ScanFrameCompletion[] frameClosedOperations;
        private Exception frameLifecycleFault;
        private AllocatorBase frameReadBufferAllocator;
        /// <summary>Frame size (1 or 2)</summary>
        protected readonly int frameSize;

        /// <summary>Begin address of the scan. Cannot be readonly due to SnapCursorToLogicalAddress</summary>
        protected long beginAddress;

        /// <summary>End address of the scan</summary>
        protected readonly long endAddress;

        /// <summary>Epoch from the store</summary>
        protected readonly LightEpoch epoch;

        /// <summary>Number of deferred DoReadPage drain callbacks that have been registered but not yet executed.</summary>
        protected int pendingDrainCallbacks;

        /// <summary>Current address for iteration</summary>
        protected long currentAddress;
        /// <summary>Next address for iteration</summary>
        protected long nextAddress;

        /// <summary><see cref="CountdownEvent"/> vector for waiting for frame-load completion.</summary>
        /// <remarks>This array is in parallel with <see cref="loadCTSs"/>, <see cref="loadedPages"/>, and <see cref="nextLoadedPages"/>.</remarks>
        private CountdownEvent[] loadCompletionEvents;

        /// <summary><see cref="CancellationTokenSource"/> vector for canceling the wait for frame-load completion.</summary>
        /// <remarks>This array is in parallel with <see cref="loadCompletionEvents"/>, <see cref="loadedPages"/>, and <see cref="nextLoadedPages"/>.</remarks>
        private CancellationTokenSource[] loadCTSs;

        /// <summary>Vector of endAddresses for the currently loaded pages of the frames.</summary>
        /// <remarks>This array is in parallel with <see cref="loadCompletionEvents"/>, <see cref="loadCTSs"/>, and <see cref="nextLoadedPages"/>.</remarks>
        private long[] loadedPages;

        /// <summary>Vector of endAddresses for the currently in-flight, and possibly completed, loading of pages of the frames.
        /// This is updated atomically when we start the <see cref="BufferAndLoad"/> of a page.</summary>
        /// <remarks>This array is in parallel with <see cref="loadCompletionEvents"/>, <see cref="loadCTSs"/>, and <see cref="loadedPages"/>.</remarks>
        private long[] nextLoadedPages;

        /// <summary>The circular buffer we cycle through for object-log deserialization.</summary>
        CircularDiskReadBuffer[] objectReadBuffers;

        /// <summary>Number of bits in the size of the log page</summary>
        private readonly int logPageSizeBits;

        /// <summary>The allocator struct wrapper used to map a logical address to its page via the allocator-specific address
        /// interpretation. Stored as the concrete <typeparamref name="TAllocatorWrapper"/> rather than the <see cref="IAllocator"/>
        /// interface so the call is inlined instead of a virtual interface dispatch. Main-store allocators mask off the
        /// read-cache bit; TsavoriteLog uses the full range.</summary>
        private readonly TAllocatorWrapper allocator;

        /// <summary>Whether to include closed records in the scan</summary>
        protected readonly bool includeClosedRecords;

        /// <summary>
        /// Current address
        /// </summary>
        public long CurrentAddress => currentAddress;

        /// <summary>
        /// Next address
        /// </summary>
        public long NextAddress => nextAddress;

        /// <summary>
        /// The starting address of the scan
        /// </summary>
        public long BeginAddress => beginAddress;

        /// <summary>
        /// The ending address of the scan
        /// </summary>
        public long EndAddress => endAddress;

        /// <summary>
        /// Logger instance
        /// </summary>
        protected ILogger logger;

        /// <summary>
        /// Buffering for holding copies of in-memory records
        /// </summary>
        protected InMemoryScanBufferingMode memScanBufferingMode;

        /// <summary>
        /// Constructor
        /// </summary>
        public unsafe ScanIteratorBase(long beginAddress, long endAddress, DiskScanBufferingMode diskScanBufferingMode, InMemoryScanBufferingMode memScanBufferingMode,
                bool includeClosedRecords, LightEpoch epoch, int logPageSizeBits, TAllocatorWrapper allocator, bool initForReads = true, ILogger logger = null)
        {
            this.logger = logger;
            this.memScanBufferingMode = memScanBufferingMode;

            // If we are protected when creating the iterator, we do not need per-GetNext protection
            if (epoch != null && !epoch.ThisInstanceProtected())
                this.epoch = epoch;

            this.beginAddress = beginAddress;
            this.endAddress = endAddress;
            this.logPageSizeBits = logPageSizeBits;
            this.allocator = allocator;

            this.includeClosedRecords = includeClosedRecords;
            currentAddress = -1;
            nextAddress = beginAddress;

            if (diskScanBufferingMode == DiskScanBufferingMode.SinglePageBuffering)
                frameSize = 1;
            else if (diskScanBufferingMode == DiskScanBufferingMode.DoublePageBuffering)
                frameSize = 2;
            else if (diskScanBufferingMode == DiskScanBufferingMode.NoBuffering)
            {
                frameSize = 0;
                return;
            }
            if (initForReads)
                InitializeForReads();
        }

        /// <summary>Initialize fields for read callback management</summary>
        public virtual void InitializeForReads(){
            lock(this.frameLifecycleGate){
                this.AssertNoFrameSelfJoin();
                this.ThrowFrameLifecycleFault();
                if(this.frameOperations is null&&!this.frameClosing&&!this.frameDisposed){this.InitializeFrameReads();return;}
            }
            this.Reset();
        }
        private void InitializeFrameReads(){
            var events=new CountdownEvent[this.frameSize];
            var cancellations=new CancellationTokenSource[this.frameSize];
            var loaded=new long[this.frameSize];
            var next=new long[this.frameSize];
            for(var i=0;i<this.frameSize;i++){loaded[i]=-1;next[i]=-1;}
            this.loadCompletionEvents=events;
            this.loadCTSs=cancellations;
            this.loadedPages=loaded;
            this.nextLoadedPages=next;
            this.frameOperations=new ScanFrameCompletion[this.frameSize];
            this.frameClaims=new bool[this.frameSize];
            this.frameDisposed=false;
            this.frameCloseCompleted=false;
            this.frameClosedOperations=null;
            this.currentAddress=-1;
            this.nextAddress=this.beginAddress;
        }

        /// <summary>Initialize read buffers</summary>
        public virtual void InitializeReadBuffers(AllocatorBase allocatorBase=default){
            lock(this.frameLifecycleGate){
                this.AssertNoFrameSelfJoin();
                this.ThrowFrameLifecycleFault();
                if(this.frameClosing||this.frameDisposed||this.frameCallers.Count!=0){throw new InvalidOperationException("Cannot replace scan read buffers while their lifecycle is active.");}
                if(this.frameOperations is not null){foreach(var operation in this.frameOperations){if(operation is not null){throw new InvalidOperationException("Read buffers must be initialized before issuing frame loads.");}}}
                this.frameReadBufferAllocator=allocatorBase;
                this.CreateFrameReadBuffers();
            }
        }
        private void CreateFrameReadBuffers(){
            var thread=Environment.CurrentManagedThreadId;
            this.frameCallers.Add(thread,1);
            try{
                var buffers=new CircularDiskReadBuffer[this.frameSize];
                for(var i=0;i<this.frameSize;i++){buffers[i]=this.frameReadBufferAllocator?.CreateCircularReadBuffers();}
                this.objectReadBuffers=buffers;
            }finally{this.frameCallers.Remove(thread);}
        }

        /// <summary>
        /// Buffer and load
        /// </summary>
        /// <param name="currentIterationAddress">The current logical address</param>
        /// <param name="currentPage">The page containing the current logical address</param>
        /// <param name="currentFrame">The frame index of the current page (the page modulo the number of frames)</param>
        /// <param name="headAddress">Head address of the log</param>
        /// <param name="endIterationAddress">Address to stop the scan at</param>
        /// <returns>True we had to await the event here; </returns>
        /// <returns></returns>
        protected bool BufferAndLoad(long currentIterationAddress,long currentPage,long currentFrame,long headAddress,long endIterationAddress){
            this.EnterFrameCall();
            try{
                for(var i=0;i<this.frameSize;i++){
                    var nextPage=currentPage+i;
                    var start=GetLogicalAddressOfStartOfPage(nextPage,this.logPageSizeBits);
                    if(start>=headAddress||start>=endIterationAddress){continue;}
                    var claim=GetLogicalAddressOfStartOfPage(nextPage+1,this.logPageSizeBits);
                    if(endIterationAddress<claim){claim=endIterationAddress;}
                    var frame=(int)((currentFrame+i)%this.frameSize);
                    while(true){
                        ScanFrameCompletion previous;
                        long priorClaim;
                        var claimed=false;
                        lock(this.frameLifecycleGate){
                            if(this.frameClosing){throw new ObjectDisposedException(this.GetType().Name);}
                            previous=this.frameOperations[frame];
                            // 同frame所有threadのjoinはclaim/vector変更より前に拒否する。
                            previous?.AssertNoSelfJoin();
                            priorClaim=this.nextLoadedPages[frame];
                            if(priorClaim>=claim&&Volatile.Read(ref this.loadedPages[frame])>=claim){break;}
                            if(!this.frameClaims[frame]&&priorClaim<claim){this.frameClaims[frame]=true;this.nextLoadedPages[frame]=claim;claimed=true;}
                        }
                        if(!claimed){this.epoch?.ProtectAndDrain();continue;}
                        ScanFrameCompletion operation=null;
                        CountdownEvent preparedEvent=null;
                        CancellationTokenSource preparedCancellation=null;
                        var createdEvent=false;
                        try{
                            this.WaitForPriorFrameLoad(previous);
                            var completion=this.loadCompletionEvents[frame];
                            if(completion is null){completion=new CountdownEvent(1);createdEvent=true;}
                            preparedEvent=completion;
                            var cancellation=new CancellationTokenSource();
                            preparedCancellation=cancellation;
                            var readBuffer=this.objectReadBuffers?[frame];
                            operation=new ScanFrameCompletion(frame,claim,completion,cancellation,readBuffer,this.CompleteFrameAccounting,issuerOwner:this);
                            // 旧callback/wait accessはjoin済み。CTSの最後access後だけ破棄する。
                            this.loadCTSs[frame]?.Dispose();
                            lock(this.frameLifecycleGate){
                                this.loadCompletionEvents[frame]=completion;
                                this.loadCTSs[frame]=cancellation;
                                Interlocked.Increment(ref this.pendingDrainCallbacks);
                                this.frameOperations[frame]=operation;
                                this.frameClaims[frame]=false;
                            }
                        }catch(Exception error){
                            lock(this.frameLifecycleGate){this.frameClaims[frame]=false;this.nextLoadedPages[frame]=priorClaim;}
                            Exception primary=error;
                            CaptureFrameCleanup(()=>preparedCancellation?.Dispose(),ref primary);
                            if(createdEvent){CaptureFrameCleanup(()=>preparedEvent?.Dispose(),ref primary);}
                            throw;
                        }
                        var frameIndex=i;
                        void DoReadPage(){
                            if(!operation.TryOwnDeferredAction()){return;}
                            Exception issueError=null;
                            try{
                                var readPage=frameIndex+this.allocator.GetPageOfAddress(currentIterationAddress,this.logPageSizeBits);
                                var completion=operation.Event;
                                this.AsyncReadPageFromDeviceToFrame(operation.ReadBuffers,readPage,endIterationAddress,operation,ref completion,0,null,null,operation.Cancellation);
                                if(!ReferenceEquals(completion,operation.Event)){throw new InvalidOperationException("Frame helper replaced the bound completion event.");}
                            }catch(Exception error){
                                issueError=error;
                                this.FailFrameLoad(operation,error);
                                operation.CommitTrustedUnissued();
                            }finally{
                                Volatile.Write(ref this.loadedPages[frame],claim);
                                operation.EndIssuer(issueError);
                            }
                        }
                        if(this.epoch is null){DoReadPage();}
                        else{
                            try{this.epoch.BumpCurrentEpoch(DoReadPage);}
                            catch(Exception error){
                                // 未開始withdraw勝者だけ未発行補償。敗者は実issuer/callbackの所有を変更しない。
                                if(operation.TryWithdrawBeforeAction()){
                                    try{this.FailFrameLoad(operation,error);operation.CommitTrustedUnissued();}
                                    finally{Volatile.Write(ref this.loadedPages[frame],claim);operation.EndIssuer(error);}
                                }
                                throw;
                            }
                        }
                        break;
                    }
                }
                return this.WaitForFrameLoad(currentIterationAddress,currentFrame);
            }finally{this.ExitFrameCall();}
        }

        /// <summary>
        /// Whether we need to buffer new page from disk
        /// </summary>
        protected bool NeedBufferAndLoad(long currentAddress, long currentPage, long currentFrame, long headAddress, long endAddress)
        {
            for (var i = 0; i < frameSize; i++)
            {
                // Read the next page. If i == 0 this is the page we are about to iterate; if i > 0, then we are issuing read-ahead for efficiency.
                var nextPage = currentPage + i;

                var pageStartAddress = GetLogicalAddressOfStartOfPage(nextPage, logPageSizeBits);

                // Cannot load nextPage if it is entirely in memory or beyond the end address
                if (pageStartAddress >= headAddress || pageStartAddress >= endAddress)
                    continue;

                // Determine the endAddress on nextPage, which may be limited by endAddress or headAddress to be before end of page.
                var pageEndAddress = GetLogicalAddressOfStartOfPage(nextPage + 1, logPageSizeBits);
                if (endAddress < pageEndAddress)
                    pageEndAddress = endAddress;
                if (headAddress < pageEndAddress)
                    pageEndAddress = headAddress;

                // Calculate the nextFrame we will load nextPage into
                var nextFrame = (currentFrame + i) % frameSize;

                // If the endAddress of the next page being loaded for this frame is already loaded, as indicated by being >= the required endAddress,
                // we don't need to load.
                var operation=Volatile.Read(ref this.frameOperations[nextFrame]);
                if(nextLoadedPages[nextFrame]<pageEndAddress||Volatile.Read(ref loadedPages[nextFrame])<pageEndAddress||(operation is not null&&(!operation.TerminalPublished||operation.Failure is not null)))
                    return true;
            }
            return false;
        }

        internal abstract void AsyncReadPageFromDeviceToFrame<TContext>(CircularDiskReadBuffer readBuffers, long readPage, long untilAddress, TContext context, ref CountdownEvent completed,
                long devicePageOffset = 0, IDevice device = null, IDevice objectLogDevice = null, CancellationTokenSource cts = null);

        // issue failureでもeventをReset/Signalしない。unknownのcallback debtは保持する。
        private void FailFrameLoad(ScanFrameCompletion operation,Exception error){
            operation.RecordFailure(error);
            try{
                this.logger?.LogError(error,"Failed to issue scan page read. Frame: {frame}, pageEndAddress: {pageEndAddress}",operation.Frame,AddressString(operation.Claim));
            }catch(Exception loggingError){operation.RecordFailure(loggingError);}
            finally{
                try{operation.Cancellation.Cancel();}
                catch(Exception cancelError){operation.RecordFailure(cancelError);}
            }
        }
        protected void AsyncReadPageFromDeviceToFrameCallback(uint errorCode,uint numBytes,object context,Exception ioException){
            var result=(PageAsyncReadResult<ScanFrameCompletion>)context;
            var operation=result.context;
            var ownsCallback=operation.BeginFinalCallback(result.handle,result.cts);
            Exception callbackError=null;
            try{
                if(errorCode==0&&ioException is null){result.handle?.Signal();}
                else{
                    var failure=ioException;
                    if(failure is null){failure=new System.IO.IOException("Scan page I/O failed.",unchecked((int)errorCode));}
                    operation.RecordFailure(failure);
                    try{this.logger?.LogError(failure,"Scan page callback failed. Frame: {frame}",operation.Frame);}
                    catch(Exception loggingError){callbackError=loggingError;operation.RecordFailure(loggingError);}
                    finally{
                        try{result.cts?.Cancel();}
                        catch(Exception cancelError){if(callbackError is null){callbackError=cancelError;}operation.RecordFailure(cancelError);}
                    }
                    if(callbackError is not null){ExceptionDispatchInfo.Capture(callbackError).Throw();}
                }
            }catch(Exception error){
                operation.RecordFailure(error);
                try{result.cts?.Cancel();}catch(Exception cancelError){operation.RecordFailure(cancelError);}
                throw;
            }finally{
                // Object外側が所有する場合はFree/最後accessの後までEndを委譲する。
                if(ownsCallback){operation.EndCallback();}
            }
        }
        private void CompleteFrameAccounting(){Interlocked.Decrement(ref this.pendingDrainCallbacks);}
        private void AwaitIssuer(ScanFrameCompletion operation){
            while(!operation.IssuerReturned){this.epoch?.ProtectAndDrain();}
        }
        private bool SuspendFrameEpoch(){
            if(this.epoch is not null&&this.epoch.ThisInstanceProtected()){this.epoch.Suspend();return true;}
            return false;
        }
        private void WaitForPriorFrameLoad(ScanFrameCompletion operation){
            if(operation is null){return;}
            operation.AssertNoSelfJoin();
            this.AwaitIssuer(operation);
            var suspended=this.SuspendFrameEpoch();
            try{operation.JoinAndSeal();}finally{if(suspended){this.epoch.Resume();}}
        }
        private bool WaitForFrameLoad(long currentAddress,long currentFrame){
            ScanFrameCompletion operation;
            lock(this.frameLifecycleGate){operation=this.frameOperations[currentFrame];}
            if(operation is null){return false;}
            operation.AssertNoSelfJoin();
            this.AwaitIssuer(operation);
            var waited=!operation.TerminalPublished;
            var borrow=operation.TryBeginWaitAccess();
            var suspended=this.SuspendFrameEpoch();
            Exception failure=null;
            try{
                operation.WaitForTerminal(()=>this.logger?.LogWarning("Still waiting for scan frame ownership to end. CurrentAddress: {address}, frame: {frame}",AddressString(currentAddress),currentFrame));
                failure=operation.Failure;
                // captured bindingだけへアクセス。旧Signal/Cancel/issuer終了後もborrowでReset/Disposeを防ぐ。
                if(borrow&&failure is null&&operation.Cancellation.IsCancellationRequested){failure=new OperationCanceledException(operation.Cancellation.Token);}
            }finally{
                if(borrow){operation.EndWaitAccess();}
                if(suspended){this.epoch.Resume();}
            }
            if(failure is not null){
                lock(this.frameLifecycleGate){
                    if(ReferenceEquals(this.frameOperations[currentFrame],operation)){this.loadedPages[currentFrame]=-1;this.nextLoadedPages[currentFrame]=-1;}
                }
                Utility.MonotonicUpdate(ref this.nextAddress,GetLogicalAddressOfStartOfPage(1+this.allocator.GetPageOfAddress(currentAddress,this.logPageSizeBits),this.logPageSizeBits),out _);
                throw new OperationCanceledException("Scan page read failed; its resources have reached terminal ownership.",failure);
            }
            return waited;
        }
        private void EnterFrameCall(){
            lock(this.frameLifecycleGate){
                this.ThrowFrameLifecycleFault();
                if(this.frameClosing||this.frameDisposed){throw new ObjectDisposedException(this.GetType().Name);}
                var thread=Environment.CurrentManagedThreadId;
                this.frameCallers.TryGetValue(thread,out var count);
                this.frameCallers[thread]=count+1;
            }
        }
        private void ExitFrameCall(){
            lock(this.frameLifecycleGate){
                var thread=Environment.CurrentManagedThreadId;
                var count=this.frameCallers[thread];
                if(count==1){this.frameCallers.Remove(thread);}else{this.frameCallers[thread]=count-1;}
                Monitor.PulseAll(this.frameLifecycleGate);
            }
        }
        private void AssertNoFrameSelfJoin(){
            if(this.frameCallers.ContainsKey(Environment.CurrentManagedThreadId)){throw new InvalidOperationException("Reentrant scan lifecycle would wait for its own frame call.");}
            if(this.frameOperations is not null){foreach(var operation in this.frameOperations){operation?.AssertNoSelfJoin();}}
        }
        internal static void CaptureFrameCleanup(Action cleanup,ref Exception primary){
            try{cleanup();}
            catch(Exception error){if(primary is null){primary=error;}else{RecoveryIoScope.AttachCleanupError(primary,error);}}
        }
        // 既知派生型のphysical/record cleanupもlifecycleを閉じたまま実行する。
        internal virtual void DisposeFrameStorage(){}
        // 以下のreceipt/fault操作はframeLifecycleGate内だけで行う。
        private void PublishFrameCloseReceipt(){
            this.frameCloseCompleted=true;
            this.frameClosedOperations=this.frameOperations;
            this.frameCloseReceipt=unchecked(this.frameCloseReceipt+1);
        }
        private void RecordFrameLifecycleFault(Exception error){
            this.frameLifecycleFault??=error;
            this.frameCloseCompleted=false;
            this.frameDisposed=true;
            this.frameClosing=true;
            this.frameLifecycleOwner=0;
            Monitor.PulseAll(this.frameLifecycleGate);
        }
        private void ThrowFrameLifecycleFault(){
            if(this.frameLifecycleFault is not null){ExceptionDispatchInfo.Capture(this.frameLifecycleFault).Throw();}
        }
        public virtual void Dispose(){
            var thread=Environment.CurrentManagedThreadId;
            lock(this.frameLifecycleGate){
                this.AssertNoFrameSelfJoin();
                this.ThrowFrameLifecycleFault();
                while(this.frameClosing&&this.frameLifecycleOwner!=thread&&this.frameLifecycleFault is null){Monitor.Wait(this.frameLifecycleGate);}
                this.ThrowFrameLifecycleFault();
                if(this.frameClosing&&!(this.frameResetting&&this.frameLifecycleOwner==thread&&!this.frameDisposeActive)){throw new InvalidOperationException("Reentrant scan Dispose.");}
                if(this.frameDisposed){
                    if(!this.frameCloseCompleted||!ReferenceEquals(this.frameClosedOperations,this.frameOperations)){
                        this.RecordFrameLifecycleFault(new InvalidOperationException("Scan frame lifecycle has no completed close receipt."));
                        this.ThrowFrameLifecycleFault();
                    }
                    // 既に閉鎖済みでも、このbase呼出しをfresh receiptで確認する。
                    this.PublishFrameCloseReceipt();
                    return;
                }
                this.frameClosing=true;
                this.frameLifecycleOwner=thread;
                this.frameDisposeActive=true;
            }
            var suspended=false;
            var closed=false;
            Exception primary=null;
            Exception closeError=null;
            try{
                suspended=this.SuspendFrameEpoch();
                lock(this.frameLifecycleGate){while(this.frameCallers.Count!=0){Monitor.Wait(this.frameLifecycleGate);}}
                if(this.frameOperations is not null){foreach(var operation in this.frameOperations){operation?.JoinAndSeal();}}
                for(var i=0;i<this.frameSize;i++){
                    var index=i;
                    CaptureFrameCleanup(()=>this.loadCompletionEvents?[index]?.Dispose(),ref primary);
                    CaptureFrameCleanup(()=>this.loadCTSs?[index]?.Dispose(),ref primary);
                    CaptureFrameCleanup(()=>this.objectReadBuffers?[index]?.Dispose(),ref primary);
                }
                CaptureFrameCleanup(this.DisposeFrameStorage,ref primary);
                if(primary is not null){ExceptionDispatchInfo.Capture(primary).Throw();}
                closed=true;
            }catch(Exception error){closeError=error;throw;}
            finally{
                Exception resumeError=null;
                try{if(suspended){this.epoch.Resume();}}
                catch(Exception error){
                    resumeError=error;
                    if(closeError is null){throw;}
                    RecoveryIoScope.AttachCleanupError(closeError,error);
                }finally{
                    lock(this.frameLifecycleGate){
                        this.frameDisposeActive=false;
                        this.frameDisposed=true;
                        if(closeError is not null){this.RecordFrameLifecycleFault(closeError);}
                        else if(resumeError is not null){this.RecordFrameLifecycleFault(resumeError);}
                        else if(!closed){this.RecordFrameLifecycleFault(new InvalidOperationException("Scan frame close did not reach completion."));}
                        else{
                            this.PublishFrameCloseReceipt();
                            if(!this.frameResetting){this.frameClosing=false;this.frameLifecycleOwner=0;}
                            Monitor.PulseAll(this.frameLifecycleGate);
                        }
                    }
                }
            }
        }
        public void Reset(){
            var thread=Environment.CurrentManagedThreadId;
            ScanFrameCompletion[] resetOperations;
            long receiptBefore;
            lock(this.frameLifecycleGate){
                this.AssertNoFrameSelfJoin();
                this.ThrowFrameLifecycleFault();
                while(this.frameClosing&&this.frameLifecycleOwner!=thread&&this.frameLifecycleFault is null){Monitor.Wait(this.frameLifecycleGate);}
                this.ThrowFrameLifecycleFault();
                if(this.frameClosing){throw new InvalidOperationException("Reentrant scan Reset.");}
                resetOperations=this.frameOperations;
                receiptBefore=this.frameCloseReceipt;
                this.frameClosing=true;
                this.frameResetting=true;
                this.frameLifecycleOwner=thread;
            }
            var completed=false;
            try{
                this.Dispose();
                lock(this.frameLifecycleGate){
                    this.ThrowFrameLifecycleFault();
                    if(!this.frameCloseCompleted||this.frameCloseReceipt==receiptBefore||!ReferenceEquals(this.frameClosedOperations,resetOperations)||!ReferenceEquals(this.frameOperations,resetOperations)||!this.frameDisposed||!this.frameClosing||!this.frameResetting||this.frameLifecycleOwner!=thread||this.frameDisposeActive){
                        throw new InvalidOperationException("Dispose override did not complete this scan frame lifecycle close.");
                    }
                    // fresh receiptと同世代のdrain/close確認後だけ新stateをpublishする。
                    this.InitializeFrameReads();
                    this.CreateFrameReadBuffers();
                    completed=true;
                }
            }catch(Exception error){
                lock(this.frameLifecycleGate){this.RecordFrameLifecycleFault(error);}
                throw;
            }finally{
                lock(this.frameLifecycleGate){
                    this.frameResetting=false;
                    if(completed){this.frameClosing=false;this.frameLifecycleOwner=0;}
                    Monitor.PulseAll(this.frameLifecycleGate);
                }
            }
        }
        /// <inheritdoc/>
        public override string ToString() => $"BA {AddressString(BeginAddress)}, EA {AddressString(EndAddress)}, CA {AddressString(CurrentAddress)}, NA {AddressString(NextAddress)}";
    }
}