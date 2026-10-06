namespace Seamless.Collections.Tsavorite;
/// <summary>
/// ログのデバイスを包み、ファイルの切り捨て(先頭の削除)を保存が確定するまで保留する。
/// Tsavorite はチェックポイントのあとで、先頭アドレスより前のセグメントを消す。共通ストアの保存は「データ本体のチェックポイント → マニフェスト」の順なので、
/// その間に強制終了すると、マニフェストが指す前の保存のファイルが既に消えていて開けなくなる(EXP-0005)。
/// 切り捨ての要求は記録だけして完了を返し、マニフェストを書いたあとに <see cref="Release"/> で実際に消す。
/// 保留中に強制終了しても、消えるはずだったファイルが残るだけで、マニフェストが指す保存は壊れない。
/// </summary>
internal sealed class DeferredTruncationDevice:IDevice {
    private readonly object gate = new();
    // ログのファイルは世代ごとに別の名前にする。全削除(Reset)は同じ名前でログを先頭から作り直すので、保存済みの前の保存のファイルを上書きしてしまう(BUG-0023)。
    // 全削除では新しい世代のファイルへ切り替え、古い世代は、新しい保存が確定して前の保存が記録から外れたあと(RetireBefore)に消す。
    private readonly Func<int,IDevice> create;
    private readonly Dictionary<int,IDevice> generations = [];
    private IDevice inner;
    private int generation;
    private bool initialized;
    private long initializedSegmentSize;
    private LightEpoch? initializedEpoch;
    private bool initializedOmitSegmentId;
    public DeferredTruncationDevice(Func<int,IDevice> create,int generation) {
        this.create=create;
        this.generation=generation;
        this.inner=create(generation);
        this.generations[generation]=this.inner;
    }
    /// <summary>いま使っているログのファイルの世代。保存の状態に記録し、開き直したときに同じ世代のファイルを開く。</summary>
    public int Generation {
        get {
            lock(this.gate) {
                return this.generation;
            }
        }
    }
    private IDevice OpenGeneration(int target) {
        var device = this.create(target);
        if(this.initialized) {
            device.Initialize(this.initializedSegmentSize,this.initializedEpoch!,this.initializedOmitSegmentId);
        }
        this.generations[target]=device;
        return device;
    }
    /// <summary>指定した世代より古い世代のファイルを、閉じて消す。新しい保存が確定して、前の保存が記録から外れたあとに呼ぶ。消せなければ false(次の機会にやり直す)。</summary>
    public bool RetireBefore(int keepFrom) {
        lock(this.gate) {
            var ok = true;
            foreach(var old in this.KeysWhere(key => key<keepFrom&&key!=this.generation)) {
                ok&=this.DisposeAndDelete(old);
            }
            return ok;
        }
    }
    /// <summary>開いている世代のうち、いまの世代より古いものがあるか。</summary>
    public bool HasRetired {
        get {
            lock(this.gate) {
                return this.KeysWhere(key => key!=this.generation).Count>0;
            }
        }
    }
    private List<int> KeysWhere(Func<int,bool> predicate) {
        var keys = new List<int>();
        foreach(var key in this.generations.Keys) {
            if(predicate(key)) {
                keys.Add(key);
            }
        }
        return keys;
    }
    private bool DisposeAndDelete(int target) {
        var device = this.generations[target];
        this.generations.Remove(target);
        var name = device.FileName;
        try {
            device.Dispose();
        } catch(IOException) {
        }
        return DeleteFiles(name);
    }
    /// <summary>デバイスのセグメントのファイル(名前.番号)を消す。同じ接頭辞の別の名前(名前-g1.0 など)は消さない。</summary>
    internal static bool DeleteFiles(string deviceFileName) {
        if(string.IsNullOrEmpty(deviceFileName)) {
            return true;
        }
        var path = Path.GetFullPath(deviceFileName);
        var directory = Path.GetDirectoryName(path);
        if(directory is null||!Directory.Exists(directory)) {
            return true;
        }
        var fileName = Path.GetFileName(path);
        var ok = true;
        foreach(var file in Directory.EnumerateFiles(directory,fileName+".*")) {
            var suffix = Path.GetFileName(file).AsSpan(fileName.Length+1);
            if(suffix.IsEmpty||suffix.IndexOfAnyExceptInRange('0','9')>=0) {
                continue;
            }
            try {
                File.Delete(file);
            } catch(IOException) {
                ok=false;
            } catch(UnauthorizedAccessException) {
                ok=false;
            }
        }
        return ok;
    }
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

    /// <summary>
    /// 全削除の前に呼ぶ。ログを同じ名前のファイルへ先頭から書き直すと前の保存のファイルを上書きするので、新しい世代のファイルへ切り替える。
    /// 古い世代のファイルは触らない(前の保存が使う)。保留中の要求は、古い世代のものなので捨てる。
    /// Tsavorite は復元のときにも内部で Reset を呼ぶので、世代の切り替えを Reset には結び付けない。
    /// </summary>
    public void StartNewGeneration() {
        lock(this.gate) {
            this.DiscardPending();
            var next = 0;
            foreach(var key in this.generations.Keys) {
                next=Math.Max(next,key+1);
            }
            this.inner=this.OpenGeneration(next);
            this.generation=next;
        }
    }
    // ログの状態を捨てて作り直す操作。復元の前にも呼ばれる。いまの世代のデバイスへそのまま渡し、保留中の要求は捨てる。
    public void Reset() {
        lock(this.gate) {
            this.DiscardPending();
            this.inner.Reset();
        }
    }

    public void Initialize(long segmentSize,LightEpoch epoch,bool omitSegmentIdFromFilename = false) {
        lock(this.gate) {
            this.initialized=true;
            this.initializedSegmentSize=segmentSize;
            this.initializedEpoch=epoch;
            this.initializedOmitSegmentId=omitSegmentIdFromFilename;
            // 古い世代を再初期化すると、そのデバイスが覚えているセグメントの範囲が壊れ、前の保存へ戻せなくなる。いまの世代だけにする。
            this.inner.Initialize(segmentSize,epoch,omitSegmentIdFromFilename);
        }
    }
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
    public void Dispose() {
        lock(this.gate) {
            foreach(var device in this.generations.Values) {
                device.Dispose();
            }
            this.generations.Clear();
        }
    }
}
