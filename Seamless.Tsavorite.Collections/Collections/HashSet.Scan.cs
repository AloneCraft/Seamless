namespace Seamless.Tsavorite.Collections;

internal interface IHashSetScanStrategy<TSelf, T>
    where TSelf : struct, IHashSetScanStrategy<TSelf,T> {
    static abstract bool Invoke(ref TSelf self,T item);
}

public partial class HashSet<TKey> {
    internal void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, IHashSetScanStrategy<TStrategy,TKey> {
        var state = new ScanState<TStrategy>(strategy);
        var adapter = new ScanAdapter<TStrategy>(state);
        this.RunScanCursor(adapter,()=>adapter.Stop);
        strategy=state.Strategy;
    }

    internal void ScanUnsafeDelegate(Func<object?,bool> body) {
        var adapter = new UnsafeDelegateScanAdapter(body);
        this.RunScanCursor(adapter,()=>adapter.Stop);
    }

    private void RunScanCursor(IScanIteratorFunctions adapter,Func<bool> shouldStop) {
        var cursor = 0L;
        var untilAddress = this.Store.Log.TailAddress;
        while(!shouldStop()) {
            var mayHaveMore = this.ReadSession.ScanCursor(
                ref cursor,
                Const.Chunk_Length,
                adapter,
                untilAddress,
                true,
                untilAddress,
                false,
                false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            if(!mayHaveMore) {
                break;
            }
        }
    }

    private sealed class ScanState<TStrategy>(TStrategy strategy) {
        public TStrategy Strategy = strategy;
    }

    private sealed class ScanAdapter<TStrategy>(ScanState<TStrategy> state):IScanIteratorFunctions
        where TStrategy : struct, IHashSetScanStrategy<TStrategy,TKey> {
        private readonly ScanState<TStrategy> state = state;
        public bool Stop { get; private set; }

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var item = AutoSerializer<TKey>.Deserialize(logRecord.Key);
            if(TStrategy.Invoke(ref this.state.Strategy,item)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            this.Stop=true;
            cursorRecordResult=CursorRecordResult.Accept;
            return false;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;

        public void OnStop(bool completed,long numberOfRecords) {
        }

        public void OnException(Exception exception,long numberOfRecords) {
        }
    }

    private sealed class UnsafeDelegateScanAdapter(Func<object?,bool> body):IScanIteratorFunctions {
        private readonly Func<object?,bool> body = body;
        public bool Stop { get; private set; }

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var item = AutoSerializer<TKey>.Deserialize(logRecord.Key);
            if(this.body(item)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            this.Stop=true;
            cursorRecordResult=CursorRecordResult.Accept;
            return false;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;

        public void OnStop(bool completed,long numberOfRecords) {
        }

        public void OnException(Exception exception,long numberOfRecords) {
        }
    }
}

internal static class HashSetScanUnsafe {
    internal static void Scan<T>(HashSet<T> source,Func<object?,bool> body) =>
        source.ScanUnsafeDelegate(body);

    internal static void ScanInt32(HashSet<int> source,Func<object?,bool> body) =>
        source.ScanUnsafeDelegate(body);
}
