using Sample.NetShop.Core.Shop;

namespace Sample.NetShop.Core;

/// <summary>章の実行中に使える道具。商店DB・作業フォルダ・画面への報告・間の取り方をまとめる。</summary>
public sealed class 章の文脈{
    private readonly 物語の再生器 再生器;
    private readonly 物語の章 章;
    private readonly List<string>作業フォルダ一覧=[];

    internal 章の文脈(物語の再生器 再生器,物語の章 章,int 番号,int 周回,CancellationToken 取消){
        this.再生器=再生器;
        this.章=章;
        this.番号=番号;
        this.周回=周回;
        this.取消=取消;
        this.乱数=new Random(周回*100+番号);
    }

    public int 番号{get;}
    public int 周回{get;}
    public CancellationToken 取消{get;}
    /// <summary>周回と章ごとに決まる乱数。同じ周回なら毎回同じ流れになる。</summary>
    public Random 乱数{get;}
    public 実行設定 設定=>this.再生器.設定;
    public 資源設定 資源=>this.再生器.設定.資源;
    /// <summary>物語全体で共有する、ディスクに残る商店DB。</summary>
    public 商店DB 商店=>this.再生器.商店;

    /// <summary>商店DBを閉じて開き直す。</summary>
    public void 商店を開き直す()=>this.再生器.商店を開き直す();

    /// <summary>この章だけで使うフォルダを作る。章が終わると消える。</summary>
    public string 作業フォルダを作る(string 名前){
        var フォルダ=Path.Combine(this.再生器.再生フォルダ,$"{this.番号+1:D2}-{名前}");
        Directory.CreateDirectory(フォルダ);
        this.作業フォルダ一覧.Add(フォルダ);
        return フォルダ;
    }

    internal IReadOnlyList<string>作った作業フォルダ=>this.作業フォルダ一覧;

    public void 報告(章の報告 報告)=>this.再生器.報告する(this.番号,報告);
    public void ログ(string 文,ログ種別 種別=ログ種別.情報)=>this.報告(new ログ報告(文,種別));
    public void 指標(string 名前,string 値)=>this.報告(new 指標報告(名前,値));
    public void メーター(string 名前,long 値,long 上限)=>this.報告(new メーター報告(名前,値,上限));
    public void 棒グラフ(string 題名,string 単位,IReadOnlyList<棒>棒一覧)=>this.報告(new 棒グラフ報告(題名,単位,棒一覧));

    /// <summary>見せているコードのうち、目印の文字列を含む最初の行を強調する。</summary>
    public void コードを指す(string 目印){
        var 行一覧=this.章.コード行;
        for(var 行番号=0;行番号<行一覧.Count;行番号++){
            if(行一覧[行番号].Contains(目印,StringComparison.Ordinal)){
                this.報告(new コード強調報告(行番号));
                return;
            }
        }
        throw new ArgumentException($"コードに目印 '{目印}' がありません。章: {this.章.題名}");
    }

    /// <summary>見る人が追えるように間を置く。速度と一時停止に従う。速度0なら待たない。</summary>
    public Task 間を置くAsync(double 秒=1)=>this.再生器.間を置くAsync(秒,this.取消);
}
