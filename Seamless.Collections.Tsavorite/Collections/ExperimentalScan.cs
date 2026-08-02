namespace Seamless.Collections.Tsavorite;

public partial class HashSet<TKey> {
    public long ExperimentalLogScanCount() {
        var log = this.Store.Log;
        long count = 0;
        using var iterator = log.Scan(log.BeginAddress,log.TailAddress);
        while(iterator.GetNext()) {
            if(iterator.Info.Tombstone) {
                continue;
            }
            count++;
        }
        return count;
    }
}
