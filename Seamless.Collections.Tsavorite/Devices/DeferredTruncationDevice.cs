namespace Seamless.Collections.Tsavorite;
/// <summary>
/// ログのデバイスを包み、ファイルの切り捨て(先頭の削除)を保存が確定するまで保留する。
/// Tsavorite はチェックポイントのあとで、先頭アドレスより前のセグメントを消す。共通ストアの保存は「データ本体のチェックポイント → マニフェスト」の順なので、
/// その間に強制終了すると、マニフェストが指す前の保存のファイルが既に消えていて開けなくなる(EXP-0005)。
/// 切り捨ての要求は記録だけして完了を返し、マニフェストを書いたあとに <see cref="Release"/> で実際に消す。
/// 保留中に強制終了しても、消えるはずだったファイルが残るだけで、マニフェストが指す保存は壊れない。
/// </summary>
internal sealed class DeferredTruncationDevice(IDevice inner):IDevice {
    private readonly object gate = new();
    // 保留中の要求。アドレスとセグメントの大きい方を覚える(切り捨ては先頭から消すだけなので、小さい要求は大きい要求に含まれる)。
    private long pendingAddress = -1;
    private int pendingSegment = -1;
    private readonly SortedSet<int> pendingRemovedSegments = [];

    /// <summary>実際に消していない要求があるか。</summary>
    public bool HasPending {
        get {
            lock(this.gate) {
                return this.pendingAddress>=0||this.pendingSegment>=0||this.pendingRemovedSegments.Count>0;
            }
        }
    }

    /// <summary>保留中の要求のうち <paramref name="limitAddress"/> より前だけを実際に消す。残りは保留のまま。ファイルを消せなかったら false(保留のまま残し、次の機会にやり直す)。</summary>
    public bool Release(long limitAddress) {
        lock(this.gate) {
            try {
                var segmentSize = inner.SegmentSize;
                var limitSegment = (int)Math.Min(int.MaxValue,limitAddress/segmentSize);
                if(this.pendingAddress>=0) {
                    var until = Math.Min(this.pendingAddress,limitAddress);
                    if(until>0) {
                        inner.TruncateUntilAddress(until);
                    }
                    if(until==this.pendingAddress) {
                        this.pendingAddress=-1;
                    }
                }
                if(this.pendingSegment>=0) {
                    var until = Math.Min(this.pendingSegment,limitSegment);
                    if(until>0) {
                        inner.TruncateUntilSegment(until);
                    }
                    if(until==this.pendingSegment) {
                        this.pendingSegment=-1;
                    }
                }
                var removable = new List<int>();
                foreach(var segment in this.pendingRemovedSegments) {
                    if(segment<limitSegment) {
                        removable.Add(segment);
                    }
                }
                foreach(var segment in removable) {
                    inner.RemoveSegment(segment);
                    this.pendingRemovedSegments.Remove(segment);
                }
                return true;
            } catch(IOException) {
                return false;
            } catch(UnauthorizedAccessException) {
                return false;
            }
        }
    }

    /// <summary>保留中の要求を、実際には消さずに捨てる。保存を読み込んで、ログを別の状態へ戻したときに呼ぶ(戻した先が必要とするファイルを消さないため)。</summary>
    public void DiscardPending() {
        lock(this.gate) {
            this.pendingAddress=-1;
            this.pendingSegment=-1;
            this.pendingRemovedSegments.Clear();
        }
    }

    private void Defer(Action<DeferredTruncationDevice> record,AsyncCallback? callback,IAsyncResult? result) {
        lock(this.gate) {
            record(this);
        }
        callback?.Invoke(result!);
    }

    public void TruncateUntilAddressAsync(long toAddress,AsyncCallback callback,IAsyncResult result) =>
        this.Defer(device => device.pendingAddress=Math.Max(device.pendingAddress,toAddress),callback,result);
    public void TruncateUntilAddress(long toAddress) =>
        this.Defer(device => device.pendingAddress=Math.Max(device.pendingAddress,toAddress),null,null);
    public void TruncateUntilSegmentAsync(int toSegment,AsyncCallback callback,IAsyncResult result) =>
        this.Defer(device => device.pendingSegment=Math.Max(device.pendingSegment,toSegment),callback,result);
    public void TruncateUntilSegment(int toSegment) =>
        this.Defer(device => device.pendingSegment=Math.Max(device.pendingSegment,toSegment),null,null);
    public void RemoveSegmentAsync(int segment,AsyncCallback callback,IAsyncResult result) =>
        this.Defer(device => device.pendingRemovedSegments.Add(segment),callback,result);
    public void RemoveSegment(int segment) =>
        this.Defer(device => device.pendingRemovedSegments.Add(segment),null,null);

    // ログ全体を捨てて作り直す操作(全削除)は、新しいログが同じファイル名を使い直すので保留できない。そのまま実行する。
    // 保留中の要求は、作り直す前のログのものなので捨てる。
    public void Reset() {
        this.DiscardPending();
        inner.Reset();
    }

    public void Initialize(long segmentSize,LightEpoch epoch,bool omitSegmentIdFromFilename = false) => inner.Initialize(segmentSize,epoch,omitSegmentIdFromFilename);
    public bool TryComplete() => inner.TryComplete();
    public bool TryCompleteMine() => inner.TryCompleteMine();
    public bool Throttle() => inner.Throttle();
    public void WriteAsync(IntPtr sourceAddress,int segmentId,ulong destinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object context) =>
        inner.WriteAsync(sourceAddress,segmentId,destinationAddress,numBytesToWrite,callback,context);
    public void ReadAsync(int segmentId,ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object context) =>
        inner.ReadAsync(segmentId,sourceAddress,destinationAddress,readLength,callback,context);
    public void WriteAsync(IntPtr sourceAddress,ulong destinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object context) =>
        inner.WriteAsync(sourceAddress,destinationAddress,numBytesToWrite,callback,context);
    public void ReadAsync(ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object context) =>
        inner.ReadAsync(sourceAddress,destinationAddress,readLength,callback,context);
    public long GetFileSize(int segment) => inner.GetFileSize(segment);
    public uint SectorSize => inner.SectorSize;
    public string FileName => inner.FileName;
    public long Capacity => inner.Capacity;
    public long SegmentSize => inner.SegmentSize;
    public int StartSegment => inner.StartSegment;
    public int EndSegment => inner.EndSegment;
    public int ThrottleLimit {
        get => inner.ThrottleLimit;
        set => inner.ThrottleLimit=value;
    }
    public void Dispose() => inner.Dispose();
}
