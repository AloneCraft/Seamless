namespace Seamless.Collections.Tsavorite;

/// <summary>
/// 並列区間。データ本体の <c>BeginParallel</c> で開き、スレッドごとに <see cref="Create"/> でマルチ用の操作用オブジェクトを作る。
/// 区間の中では件数を出さず、索引を広げない。区間を閉じ、区間から作った操作用オブジェクトをすべて破棄すると、
/// 区間を開いたときの件数に各マルチ用の増減(新しく足した・消した数)の合計を足したものが件数になり、索引は混んでいれば広げられる。
/// 開いている間は、シングル用の作成(<c>Create()</c>)・2つ目の区間・保存の読み込みはできない。
/// </summary>
public sealed class ParallelRegion<TAccessor>:IDisposable where TAccessor : class {
    private readonly ParallelRegionState state;
    private readonly Func<ParallelRegionState,TAccessor> create;
    internal ParallelRegion(ParallelRegionState state,Func<ParallelRegionState,TAccessor> create) {
        this.state=state;
        this.create=create;
    }
    /// <summary>マルチ用の操作用オブジェクトを作る。スレッドごとに 1 つ作り、使い終わったら破棄する。区間を破棄したあとは ObjectDisposedException。</summary>
    public TAccessor Create() => this.create(this.state);
    /// <summary>
    /// 区間を閉じる。例外は投げない。区間から作った操作用オブジェクトが残っていれば、最後の 1 つを破棄したときに仕上げる。
    /// </summary>
    public void Dispose() {
        this.state.Close(fromFinalizer: false);
        GC.SuppressFinalize(this);
    }
    // using を忘れても、データ本体がいつまでも使えなくならないようにする(索引の拡大は次のシングル用の作成に任せる)。
    ~ParallelRegion() => this.state.Close(fromFinalizer: true);
}

/// <summary>
/// 並列区間の状態。開いている操作用オブジェクトの数と「閉じ中」の印を 1 つの値で持ち、比較交換で書き換える。
/// 閉じ中で操作用オブジェクトが 0 になったときに、データ本体へ 1 回だけ仕上げを頼む。
/// </summary>
internal sealed class ParallelRegionState(CollectionStore owner,long? baseCount) {
    private const long ClosingFlag = 1L<<62;
    private long state;
    private int finished;
    private long countDelta;

    /// <summary>区間を開いたときの件数。不明だったら null(閉じたあとも不明のまま、次に求めたときに数え直す)。</summary>
    internal long? BaseCount => baseCount;
    /// <summary>破棄したマルチ用の件数の増減の合計。</summary>
    internal long CountDelta => Interlocked.Read(ref this.countDelta);
    /// <summary>マルチ用を破棄したとき(<see cref="Leave"/> の前)に、そのオブジェクトの増減を足し込む。</summary>
    internal void AddCountDelta(long delta) {
        if(delta!=0) {
            Interlocked.Add(ref this.countDelta,delta);
        }
    }

    /// <summary>操作用オブジェクトを作るとき(データ本体の関所の中)。閉じ中なら ObjectDisposedException。</summary>
    internal void Enter() {
        while(true) {
            var current = Volatile.Read(ref this.state);
            if((current&ClosingFlag)!=0)
                throw new ObjectDisposedException(nameof(ParallelRegion<object>),"The parallel region has been disposed.");
            if(Interlocked.CompareExchange(ref this.state,current+1,current)==current) {
                return;
            }
        }
    }

    /// <summary>操作用オブジェクトを破棄したとき。閉じ中で最後の 1 つなら仕上げる。</summary>
    internal void Leave(bool fromFinalizer) {
        if(Interlocked.Decrement(ref this.state)==ClosingFlag) {
            this.Finish(fromFinalizer);
        }
    }

    /// <summary>区間を閉じる。開いている操作用オブジェクトが無ければすぐ仕上げる。2 回目以降は何もしない。</summary>
    internal void Close(bool fromFinalizer) {
        while(true) {
            var current = Volatile.Read(ref this.state);
            if((current&ClosingFlag)!=0) {
                return;
            }
            if(Interlocked.CompareExchange(ref this.state,current|ClosingFlag,current)==current) {
                if(current==0) {
                    this.Finish(fromFinalizer);
                }
                return;
            }
        }
    }

    private void Finish(bool fromFinalizer) {
        if(Interlocked.Exchange(ref this.finished,1)!=0) {
            return;
        }
        owner.FinishParallelRegion(this,fromFinalizer);
    }
}
