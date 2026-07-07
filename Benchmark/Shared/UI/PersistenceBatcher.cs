using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed class PersistenceBatcher{
    private readonly IUiDispatcher dispatcher;
    private readonly UiDiagnostics diagnostics;
    private readonly Action<IReadOnlyList<PersistenceItem>> persist;
    private readonly ConcurrentQueue<PersistenceItem> queue = new();
    private int scheduled;

    public PersistenceBatcher(
        IUiDispatcher dispatcher,
        UiDiagnostics diagnostics,
        Action<IReadOnlyList<PersistenceItem>> persist){
        this.dispatcher=dispatcher;
        this.diagnostics=diagnostics;
        this.persist=persist;
    }

    public void Enqueue(PersistenceItem item){
        this.queue.Enqueue(item);
        this.RequestFlush(PipelineConstants.PersistenceBatchIntervalMs);
    }

    public void FlushNow() => this.RequestFlush(0);

    private void RequestFlush(int delayMilliseconds){
        if(Interlocked.Exchange(ref this.scheduled,1)==1){
            return;
        }

        _=Task.Run(async () => {
            if(delayMilliseconds>0){
                await Task.Delay(delayMilliseconds);
            }

            this.dispatcher.BeginInvoke(this.FlushOnUiThread);
        });
    }

    private void FlushOnUiThread(){
        Interlocked.Exchange(ref this.scheduled,0);
        if(this.queue.IsEmpty){
            this.diagnostics.RecordPersistence(0,0);
            return;
        }

        var items = new List<PersistenceItem>();
        while(this.queue.TryDequeue(out var item)){
            items.Add(item);
        }

        var stopwatch = Stopwatch.StartNew();
        this.persist(items);
        stopwatch.Stop();
        this.diagnostics.RecordPersistence(stopwatch.Elapsed.TotalMilliseconds,this.queue.Count);
        UiThreadBudget.Record("flush-persist",stopwatch.Elapsed.TotalMilliseconds);
    }
}
