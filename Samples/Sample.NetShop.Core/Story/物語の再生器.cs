using Sample.NetShop.Core.Shop;

namespace Sample.NetShop.Core;

public enum 章の進行{
    待機,
    実行中,
    完了,
    失敗,
}

public sealed record 章の状態(int 番号,章の進行 進行,string? 失敗理由);

/// <summary>
/// 章を順に自動で実行し、最後まで行ったら最初から繰り返す。画面には依存せず、イベントで状況を知らせる。
/// イベントは再生しているスレッドから呼ばれる。
/// </summary>
public sealed class 物語の再生器{
    private readonly object 錠=new();
    private TaskCompletionSource? 一時停止ゲート;
    private CancellationTokenSource? 章の取消;
    private int? 移動先;
    private 商店DB? 商店DB本体;
    private string? 再生フォルダ本体;
    private int 再生中=0;

    public 物語の再生器(実行設定 設定,IReadOnlyList<物語の章>? 章一覧=null){
        this.設定=設定;
        this.章一覧=章一覧??既定の章一覧();
    }

    public static IReadOnlyList<物語の章>既定の章一覧()=>[
        new 開店準備章(),
        new 保存と巻き戻し章(),
        new 商店DB章(),
        new 注文処理章(),
        new 繁忙期章(),
        new FLinq章(),
        new 決算章(),
        new 障害復旧章(),
    ];

    public 実行設定 設定{get;}
    public IReadOnlyList<物語の章>章一覧{get;}
    /// <summary>間の長さの倍率の逆数。2なら半分の時間で進む。0なら待たない。</summary>
    public double 速度{get;set;}=1;

    public bool 一時停止中か{
        get{
            lock(this.錠){
                return this.一時停止ゲート is not null;
            }
        }
    }

    public event Action<章の状態>? 章状態変化時;
    public event Action<int,章の報告>? 報告時;
    public event Action<int>? 周回開始時;

    internal 商店DB 商店=>this.商店DB本体??throw new InvalidOperationException("再生していません。");
    internal string 再生フォルダ=>this.再生フォルダ本体??throw new InvalidOperationException("再生していません。");

    public void 一時停止(){
        lock(this.錠){
            this.一時停止ゲート??=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void 再開(){
        TaskCompletionSource? ゲート;
        lock(this.錠){
            ゲート=this.一時停止ゲート;
            this.一時停止ゲート=null;
        }
        ゲート?.TrySetResult();
    }

    /// <summary>実行中の章を打ち切り、指定した章から続ける。</summary>
    public void 章へ移動(int 番号){
        ArgumentOutOfRangeException.ThrowIfNegative(番号);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(番号,this.章一覧.Count);
        lock(this.錠){
            this.移動先=番号;
            this.章の取消?.Cancel();
        }
        this.再開();
    }

    /// <summary>再生する。<paramref name="周回数"/> が null なら取り消されるまで繰り返す。</summary>
    public async Task 再生Async(int? 周回数,CancellationToken 取消){
        if(this.設定.開始章<0||this.設定.開始章>=this.章一覧.Count){
            throw new ArgumentOutOfRangeException(nameof(this.設定.開始章),this.設定.開始章+1,$"章は1〜{this.章一覧.Count}で指定してください。");
        }
        if(Interlocked.Exchange(ref this.再生中,1)!=0){
            throw new InvalidOperationException("すでに再生しています。");
        }
        this.再生フォルダ本体=Path.Combine(this.設定.作業フォルダ,"run-"+Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(this.再生フォルダ本体);
        try{
            this.商店DB本体=商店DB.開く(this.商店DBのフォルダ(),this.設定.資源);
            var 周回=1;
            var 番号=this.設定.開始章;
            this.周回を始める(周回);
            while(true){
                取消.ThrowIfCancellationRequested();
                await this.章を実行するAsync(番号,周回,取消);
                int 次;
                lock(this.錠){
                    if(this.移動先 is int 指定){
                        次=指定;
                        this.移動先=null;
                    }else{
                        次=番号+1;
                    }
                }
                if(次>=this.章一覧.Count){
                    if(周回数 is int 上限&&周回>=上限){
                        break;
                    }
                    周回++;
                    次=0;
                    this.周回を始める(周回);
                }
                番号=次;
            }
        }finally{
            this.商店DB本体?.Dispose();
            this.商店DB本体=null;
            Directory.Delete(this.再生フォルダ本体,true);
            this.再生フォルダ本体=null;
            Volatile.Write(ref this.再生中,0);
        }
    }

    private string 商店DBのフォルダ()=>Path.Combine(this.設定.データフォルダ,"shop");

    private void 周回を始める(int 周回){
        this.周回開始時?.Invoke(周回);
        for(var 番号=0;番号<this.章一覧.Count;番号++){
            this.章状態変化時?.Invoke(new(番号,章の進行.待機,null));
        }
    }

    private async Task 章を実行するAsync(int 番号,int 周回,CancellationToken 取消){
        using var 章の取消=CancellationTokenSource.CreateLinkedTokenSource(取消);
        lock(this.錠){
            this.章の取消=章の取消;
        }
        var 章=this.章一覧[番号];
        var 文脈=new 章の文脈(this,章,番号,周回,章の取消.Token);
        this.章状態変化時?.Invoke(new(番号,章の進行.実行中,null));
        try{
            await 章.実行Async(文脈);
            this.章状態変化時?.Invoke(new(番号,章の進行.完了,null));
        }catch(OperationCanceledException) when(!取消.IsCancellationRequested){
            // 章への移動で打ち切られた。失敗ではない。
            this.章状態変化時?.Invoke(new(番号,章の進行.待機,null));
        }catch(Exception 例外) when(!取消.IsCancellationRequested){
            this.報告する(番号,new ログ報告($"{例外.GetType().Name}: {例外.Message}",ログ種別.失敗));
            this.章状態変化時?.Invoke(new(番号,章の進行.失敗,例外.ToString()));
        }finally{
            lock(this.錠){
                this.章の取消=null;
            }
            foreach(var フォルダ in 文脈.作った作業フォルダ){
                if(Directory.Exists(フォルダ)){
                    Directory.Delete(フォルダ,true);
                }
            }
        }
    }

    internal void 報告する(int 番号,章の報告 報告)=>this.報告時?.Invoke(番号,報告);

    internal void 商店を開き直す(){
        this.商店DB本体?.Dispose();
        this.商店DB本体=null;
        this.商店DB本体=商店DB.開く(this.商店DBのフォルダ(),this.設定.資源);
    }

    internal async Task 間を置くAsync(double 秒,CancellationToken 取消){
        var 残り=TimeSpan.Zero;
        var 速度=this.速度;
        if(速度>0){
            残り=TimeSpan.FromSeconds(秒/速度);
        }
        while(true){
            TaskCompletionSource? ゲート;
            lock(this.錠){
                ゲート=this.一時停止ゲート;
            }
            if(ゲート is not null){
                await ゲート.Task.WaitAsync(取消);
                continue;
            }
            取消.ThrowIfCancellationRequested();
            if(残り<=TimeSpan.Zero){
                return;
            }
            // 一時停止に早く反応するよう、細かく刻んで待つ。
            var 一回=TimeSpan.FromMilliseconds(Math.Min(100,残り.TotalMilliseconds));
            await Task.Delay(一回,取消);
            残り-=一回;
        }
    }
}
