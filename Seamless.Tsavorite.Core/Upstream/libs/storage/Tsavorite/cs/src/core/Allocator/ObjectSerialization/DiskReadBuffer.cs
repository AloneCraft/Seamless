// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Tsavorite.core
{
    internal sealed unsafe class DiskReadBuffer : IDisposable
    {
        internal readonly IDevice device;
        internal readonly ILogger logger;

        /// <summary>Signals when reads are complete. Allows multiple reads of buffer subsections to be enqueed, and uses a ManualResetEvent
        ///     so it remains signaled until Reset() is called (currently only one read for a single span of the buffer is done).</summary>
        internal CountdownEvent countdownEvent;
        private ScanFrameCompletion readCompletion;
        private ScanFrameCompletion frameOwner;

        /// <summary>The buffer to read (part of) the page image into.</summary>
        internal SectorAlignedMemory memory;

        /// <summary>
        /// This is the initialization value for <see cref="currentPosition"/>; it means there is no data available for this buffer and no
        /// in-flight read (we issue reads ahead of the buffer-array traversal, so this means that by the time we got to this buffer all
        /// the data had already been read.
        /// </summary>
        const int NoPosition = -1;

        /// <summary>Current read position (we do not support write in this buffer). This class only supports Read and no Seek,
        /// so currentPosition is always where <see cref="ObjectLogReader{TStoreFunctions}"/> will read from next.</summary>
        /// <remarks>This will be either 0 or greater than or equal to <see cref="PageHeader.Size"/>.</remarks>
        internal int currentPosition;

        /// <summary>Non-inclusive last position in this buffer; the number of byte read. If <see cref="currentPosition"/> equals this, then we are out of space and
        /// must move to the next buffer.</summary>
        internal int endPosition;

        /// <summary>
        /// The starting position in the file that we read this buffer from.
        /// </summary>
        internal ObjectLogFilePositionInfo startFilePosition;

        internal int AvailableLength => endPosition - currentPosition;

        internal ReadOnlySpan<byte> AvailableSpan => new(memory.GetValidPointer() + currentPosition, endPosition - currentPosition);

        internal DiskReadBuffer(SectorAlignedMemory memory, IDevice device, ILogger logger)
        {
            this.memory = memory;
            countdownEvent = new CountdownEvent(0); // Start with 0; we'll increment at the time of read
            this.device = device;
            this.logger = logger;
            Initialize();
        }

        internal void Initialize()
        {
            this.readCompletion?.JoinAndSeal();
            this.readCompletion=null;
            currentPosition = endPosition = NoPosition;
        }

        internal ReadOnlySpan<byte> GetTailSpan(int start) => new(memory.GetValidPointer() + start, currentPosition - start);

        /// <summary>
        /// Read the first chunk of an Object deserialization from the device.
        /// </summary>
        /// <param name="filePosition">Sector-aligned position in the device</param>
        /// <param name="startPosition">Start position in the buffer (relative to start of buffer)</param>
        /// <param name="alignedReadLength">Number of bytes to read</param>
        /// <param name="callback">The <see cref="CircularDiskReadBuffer"/> callback.</param>
        internal void ReadFromDevice(ObjectLogFilePositionInfo filePosition,int startPosition,uint alignedReadLength,DeviceIOCompletionCallback callback){
            this.readCompletion?.AssertNoSelfJoin();
            this.readCompletion?.JoinAndSeal();
            // 一つのread世代。旧callback/Waitのjoin後にだけ次read用Resetを行う。
            this.countdownEvent.Reset(1);
            var operation=new ScanFrameCompletion(0,0,this.countdownEvent,null,null,static()=>{},this.frameOwner,this);
            operation.TryOwnDeferredAction();
            operation.TrustSourceAuditedForwarder(this);
            this.readCompletion=operation;
            Exception issueError=null;
            var escaped=false;
            try{
                this.startFilePosition=filePosition;
                this.currentPosition=startPosition;
                this.endPosition=0;
                var destination=(IntPtr)this.memory.aligned_pointer;
                void Completed(uint error,uint bytes,object actual,Exception exception){
                    operation.BeginOuterCallback();
                    try{
                        if(exception is not null||error!=0){
                            var failure=exception;
                            if(failure is null){failure=new System.IO.IOException("Object buffer I/O failed.",unchecked((int)error));}
                            operation.RecordFailure(failure);
                            if(error==0){error=uint.MaxValue;bytes=0;}
                        }
                        callback(error,bytes,actual,exception);
                    }catch(Exception callbackError){operation.RecordFailure(callbackError);throw;}
                    finally{operation.EndCallback();}
                }
                operation.MarkMayHaveEscaped();
                escaped=true;
                this.device.ReadAsync(filePosition.SegmentId,filePosition.Offset,destination,alignedReadLength,Completed,this);
            }catch(Exception error){
                issueError=error;
                operation.RecordFailure(error);
                if(!escaped){operation.CertifyTrustedPreparationFailure();operation.CommitTrustedUnissued();}
                throw;
            }finally{operation.EndIssuer(issueError);}
        }
        internal void JoinReadCompletion()=>this.readCompletion?.JoinAndSeal();
        internal void BindFrameOwner(ScanFrameCompletion owner){this.JoinReadCompletion();this.frameOwner=owner;}
        internal bool ReadResourcesTerminal=>this.readCompletion is null||this.readCompletion.ResourcesJoined;
        internal Exception ReadFailure=>this.readCompletion?.Failure;

        internal static void IncrementOrResetCountdown(ref CountdownEvent countdownEvent) => DiskWriteBuffer.IncrementOrResetCountdown(ref countdownEvent);

        internal bool HasData=>this.ReadResourcesTerminal&&this.ReadFailure is null&&this.endPosition>0;

        internal bool WaitForDataAvailable(){
            if(this.currentPosition==NoPosition){return false;}
            this.readCompletion?.AssertNoSelfJoin();
            this.readCompletion?.WaitForTerminal();
            var error=this.ReadFailure;
            if(error is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();}
            return true;
        }
        internal bool HasInFlightRead=>!this.ReadResourcesTerminal;


        internal ObjectLogFilePositionInfo GetCurrentFilePosition()
        {
            var bufferFilePos = startFilePosition;
            bufferFilePos.Offset += (uint)currentPosition;

            // We only read from one segment into one buffer, so we should never exceed the segment size with this increment.
            Debug.Assert(bufferFilePos.Offset < bufferFilePos.SegmentSize, $"Incremented bufferFilePos.Offset {bufferFilePos.Offset} should be < bufferFilePos.SegmentSize {bufferFilePos.SegmentSize}");
            return bufferFilePos;
        }

        public void Dispose()
        {
            this.readCompletion?.AssertNoSelfJoin();
            if (device is RecoveryIoScope.BorrowedDevice recoveryDevice)
            {
                recoveryDevice.Scope.RetireContext(this, DisposeCore);
                return;
            }
            DisposeCore();
        }

        private void DisposeCore()
        {
            this.JoinReadCompletion();
            memory?.Return();
            memory = null;
            countdownEvent?.Dispose();
            countdownEvent = null;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"currPos {currentPosition}; endPos {endPosition}; avLen {AvailableLength}; countDown {countdownEvent?.CurrentCount}; buf: {memory}";
    }
}