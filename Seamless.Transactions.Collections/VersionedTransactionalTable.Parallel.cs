using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Seamless.Collections.Iteration;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
namespace Seamless.Transactions.Collections;

public sealed partial class VersionedTransactionalTable<TKey,TRecord> where TKey:notnull{
    private sealed partial class Changes{
        public void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IParallelStrategy<TStrategy,TRecord>{
            this.context.ThrowIfUnavailable();
            var scheduler=CpuScheduler.Shared;
            // 私有変更の退避索引はworker間で共有しない。通常のscope操作もworkerから呼ばない。
            if(!this.changes.IsEmpty||scheduler.IsExecuting||this.table.resources.MaximumQueryWorkers==1){this.Iterate(ref strategy);return;}
            this.context.ObserveTableRead(this.table);
            using var working=this.table.resources.Budget.ReserveMemory(checked(this.table.data.PageBytes*4));
            using var access=this.table.Access();using var scan=access.OpenSnapshot(this.context.ReadVersion);
            var count=Math.Min(this.table.resources.MaximumQueryWorkers,Math.Min(scheduler.Usage.Capacity,scan.AvailablePartitions));
            // worker数に比例した予約だけを取り、全行の配列や全rangeのqueueは作らない。
            ResourceReservation? reservation=null;
            while(count>1){
                try{reservation=this.table.resources.Budget.ReserveMemory(checked(count*(this.table.data.PageBytes*4+Unsafe.SizeOf<TStrategy>()+1024L)));break;}
                catch(ResourceLimitException error)when(error.Kind==ResourceLimitKind.Memory){count/=2;}
            }
            if(count<=1){scan.Dispose();access.Dispose();working.Dispose();this.Iterate(ref strategy);return;}
            using var workerMemory=reservation!;
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(this.table.resources.CancellationToken);
            var tasks=new Task<TStrategy>[count];var seed=strategy;ExceptionDispatchInfo? failure=null;var started=0;
            try{
                for(;started<count;started++){
                    tasks[started]=Task.Run(async()=>{
                        try{
                            var partial=TStrategy.CreatePartial(in seed);var overlay=new ParallelOverlay<TStrategy>(this,partial,stop.Token);
                            while(await scheduler.RunAsync(()=>{this.context.ThrowIfUnavailable();stop.Token.ThrowIfCancellationRequested();return scan.IterateNextRange(ref overlay);},stop.Token).ConfigureAwait(false)){
                                await Task.Yield();
                            }
                            return overlay.Strategy;
                        }catch(Exception error){Interlocked.CompareExchange(ref failure,ExceptionDispatchInfo.Capture(error),null);stop.Cancel();throw;}
                    });
                }
            }catch{
                stop.Cancel();
                try{Task.WhenAll(tasks.Take(started)).GetAwaiter().GetResult();}catch{}
                throw;
            }
            TStrategy[] parts;
            try{parts=Task.WhenAll(tasks).GetAwaiter().GetResult();}
            catch{failure?.Throw();throw;}
            this.context.ThrowIfUnavailable();
            foreach(var part in parts){TStrategy.Merge(ref strategy,in part);}
            // usingの走査保護・native参照・予約は全workerが停止した後に返す。
        }
        private struct ParallelOverlay<TStrategy>(Changes owner,TStrategy strategy,CancellationToken cancellation):IHistoryStrategy<ParallelOverlay<TStrategy>> where TStrategy:struct,IParallelStrategy<TStrategy,TRecord>{
            private readonly Changes owner=owner;
            private readonly CancellationToken cancellation=cancellation;
            internal TStrategy Strategy=strategy;
            public static bool Invoke(ref ParallelOverlay<TStrategy> self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long version,bool deleted){
                self.owner.context.ThrowIfUnavailable();self.cancellation.ThrowIfCancellationRequested();if(deleted){return true;}
                var value=self.owner.table.DecodeStoreRow(row);
                if(!TStrategy.Invoke(ref self.Strategy,value)){throw new InvalidOperationException("A parallel full-scan strategy must not request early termination.");}
                self.owner.context.ThrowIfUnavailable();self.cancellation.ThrowIfCancellationRequested();return true;
            }
        }
    }
}
