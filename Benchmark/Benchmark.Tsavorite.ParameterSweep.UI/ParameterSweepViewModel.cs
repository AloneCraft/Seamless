using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Benchmark.Tsavorite.ParameterSweep.UI;

public sealed class パラメータスイープビューモデル:INotifyPropertyChanged{
    private readonly パラメータスイープ実行器 実行器=new();
    private readonly UI実行ストア ストア;
    private UI実行記録? 選択前回実行値;
    private bool 実行中値か;
    private string 状態表示値="準備完了";
    private string 最大成功Count表示値="65536";
    private string 最大スイープ手順数表示値="8";

    public パラメータスイープビューモデル(){
        this.ストア=new(
            UI実行DBパスリゾルバー.解決(UI実行アプリ種別.パラメータスイープ,ソリューションルートを検索()),
            UI実行アプリ種別.パラメータスイープ);
        this.ストア.スキーマを確認();
        this.実行履歴を更新();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<パラメータスイープ比較表示行> 行一覧{get;}=[];
    public ObservableCollection<UI実行記録> 実行履歴{get;}=[];

    public UI実行記録? 選択前回実行{
        get=>this.選択前回実行値;
        set{
            if(this.選択前回実行値==value){return;}
            this.選択前回実行値=value;
            this.プロパティ変更時();
            this.前回比較を適用();
        }
    }

    public bool 実行中か{
        get=>this.実行中値か;
        private set{
            if(this.実行中値か==value){return;}
            this.実行中値か=value;
            this.プロパティ変更時();
            this.プロパティ変更時(nameof(this.実行可能か));
        }
    }

    public bool 実行可能か=>!this.実行中か;

    public string 状態表示{
        get=>this.状態表示値;
        private set{if(this.状態表示値==value){return;}this.状態表示値=value;this.プロパティ変更時();}
    }

    public string 最大成功Count表示{
        get=>this.最大成功Count表示値;
        set{if(this.最大成功Count表示値==value){return;}this.最大成功Count表示値=value;this.プロパティ変更時();}
    }

    public string 最大スイープ手順数表示{
        get=>this.最大スイープ手順数表示値;
        set{if(this.最大スイープ手順数表示値==value){return;}this.最大スイープ手順数表示値=value;this.プロパティ変更時();}
    }

    public async Task 全件実行Async()=>await this.実行Async(false);
    public async Task スモーク実行Async()=>await this.実行Async(true);

    public void 実行履歴を更新(){
        var 選択ID=this.選択前回実行?.実行ID;
        this.実行履歴.Clear();
        foreach(var 項目 in this.ストア.実行一覧を取得(UI実行アプリ種別.パラメータスイープ)){this.実行履歴.Add(項目);}
        this.選択前回実行=this.実行履歴.FirstOrDefault(項目=>項目.実行ID==選択ID)??this.実行履歴.FirstOrDefault();
    }

    private async Task 実行Async(bool スモークか){
        if(this.実行中か){return;}
        this.実行中か=true;
        this.行一覧.Clear();
        UI実行記録? 実行=null;
        try{
            var オプション=this.オプションを作成(スモークか);
            var ケース一覧=スモークか ? パラメータスイープ計画.スモークケース一覧を作成(オプション) : パラメータスイープ計画.既定ケース一覧を作成(オプション);
            実行=this.ストア.実行を作成(new(UI実行アプリ種別.パラメータスイープ,
                (スモークか ? "スモーク " : "全件 ")+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",CultureInfo.InvariantCulture),
                スモークか ? "Smoke" : "Full","ParameterSweep",DateTimeOffset.UtcNow));
            this.ストア.実行状態を設定(実行.実行ID,UI実行状態.実行中,"パラメータスイープを開始しました。");
            var 進捗=new Progress<パラメータスイープ結果>(結果=>{
                this.結果を保存(実行.実行ID,結果);
                this.行一覧.Add(new(結果,null));
                this.状態表示="実行中 "+this.行一覧.Count+"/"+ケース一覧.Count+" "+結果.ケース名;
            });
            var 結果一覧=await this.実行器.実行Async(ケース一覧,オプション,進捗);
            foreach(var 結果 in 結果一覧){this.結果を保存(実行.実行ID,結果);}
            this.ストア.実行状態を設定(実行.実行ID,UI実行状態.完了,結果一覧.Count.ToString(CultureInfo.InvariantCulture)+"ケース完了");
            this.実行履歴を更新();
            this.選択前回実行=this.実行履歴.FirstOrDefault(項目=>項目.実行ID==実行.実行ID);
            this.状態表示=結果一覧.Count+"ケース完了。UI DB実行ID "+実行.実行ID.ToString(CultureInfo.InvariantCulture)+"へ保存しました。";
        }catch(Exception 例外){
            if(実行 is not null){this.ストア.実行状態を設定(実行.実行ID,UI実行状態.失敗,例外.Message);}
            this.状態表示="失敗: "+例外.Message;
        }finally{
            this.実行中か=false;
        }
    }

    private パラメータスイープ実行オプション オプションを作成(bool スモークか){
        var 最大成功Count=int.TryParse(this.最大成功Count表示,out var 解析最大値)&&解析最大値>0 ? 解析最大値 : 65536;
        var 最大手順数=int.TryParse(this.最大スイープ手順数表示,out var 解析手順数)&&解析手順数>0 ? 解析手順数 : 8;
        if(スモークか){最大成功Count=Math.Min(最大成功Count,256);最大手順数=Math.Min(最大手順数,2);}
        return new(){
            最大成功Count=最大成功Count,
            最大スイープ手順数=最大手順数,
            検索試行Count=Math.Min(4096,最大成功Count),
            作業ディレクトリ=Path.Combine(Path.GetDirectoryName(this.ストア.データベースパス)??Path.GetTempPath(),"Temp"),
        };
    }

    private void 前回比較を適用(){
        var 前回実行=this.選択前回実行;
        if(前回実行 is null||this.行一覧.Count==0){return;}
        IReadOnlyList<パラメータスイープ結果> 前回結果;
        try{前回結果=this.結果を読み込む(前回実行);}catch{return;}
        var 比較一覧=パラメータスイープ比較器.比較する(this.行一覧.Select(static 行=>行.今回).ToArray(),前回結果);
        this.行一覧.Clear();
        foreach(var 比較 in 比較一覧){this.行一覧.Add(new(比較.今回,比較));}
    }

    private static string? ソリューションルートを検索(){
        var ディレクトリ=new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null){if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))){return ディレクトリ.FullName;}ディレクトリ=ディレクトリ.Parent;}
        return null;
    }

    private void プロパティ変更時([CallerMemberName]string? プロパティ名 = null)=>this.PropertyChanged?.Invoke(this,new(プロパティ名));

    private void 結果を保存(long 実行ID,パラメータスイープ結果 結果){
        this.ストア.パラメータスイープ結果を保存(new(実行ID,結果.ケース名,コレクションを保存値へ(結果.コレクション),結果.ジェネリック名,バックエンドを保存値へ(結果.バックエンド),スイープを保存値へ(結果.スイープ軸),
            結果.Indexサイズ,結果.Pageサイズ,結果.Segmentサイズ,結果.Memoryサイズ,結果.可変Fraction,結果.最大成功Count,結果.エントリCount,
            失敗種別を保存値へ(結果.失敗種別),結果.失敗メッセージ,結果.追加経過ミリ秒,結果.検索平均ナノ秒,結果.計測日時));
    }

    private IReadOnlyList<パラメータスイープ結果> 結果を読み込む(UI実行記録 実行)=>this.ストア.パラメータスイープ結果一覧を取得(実行.実行ID).Select(項目=>new パラメータスイープ結果(
        項目.実行ID.ToString(CultureInfo.InvariantCulture),項目.計測日時,項目.ケース名,保存値をコレクションへ(項目.コレクション),項目.ジェネリック名,保存値をバックエンドへ(項目.バックエンド),
        保存値をスイープへ(項目.スイープ),項目.Indexサイズ,項目.Pageサイズ,項目.Segmentサイズ,項目.Memoryサイズ,項目.可変Fraction,
        項目.最大成功Count,項目.エントリCount,保存値を失敗種別へ(項目.失敗種別),項目.失敗理由,項目.追加経過ミリ秒,項目.検索平均ナノ秒)).ToArray();

    private static string コレクションを保存値へ(パラメータスイープコレクション種別 値)=>値 switch{パラメータスイープコレクション種別.ハッシュセット=>"HashSet",パラメータスイープコレクション種別.セット=>"Set",パラメータスイープコレクション種別.辞書=>"Dictionary",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static パラメータスイープコレクション種別 保存値をコレクションへ(string 値)=>値 switch{"HashSet"=>パラメータスイープコレクション種別.ハッシュセット,"Set"=>パラメータスイープコレクション種別.セット,"Dictionary"=>パラメータスイープコレクション種別.辞書,_=>throw new InvalidDataException("不明なコレクションです: "+値)};
    private static string バックエンドを保存値へ(パラメータスイープバックエンド 値)=>値 switch{パラメータスイープバックエンド.メモリ=>"Memory",パラメータスイープバックエンド.ストレージ=>"Storage",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static パラメータスイープバックエンド 保存値をバックエンドへ(string 値)=>値 switch{"Memory"=>パラメータスイープバックエンド.メモリ,"Storage"=>パラメータスイープバックエンド.ストレージ,_=>throw new InvalidDataException("不明なバックエンドです: "+値)};
    private static string スイープを保存値へ(パラメータスイープ軸 値)=>値 switch{パラメータスイープ軸.Indexサイズ=>"IndexSize",パラメータスイープ軸.Pageサイズ=>"PageSize",パラメータスイープ軸.Segmentサイズ=>"SegmentSize",パラメータスイープ軸.Memoryサイズ=>"MemorySize",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static パラメータスイープ軸 保存値をスイープへ(string 値)=>値 switch{"IndexSize"=>パラメータスイープ軸.Indexサイズ,"PageSize"=>パラメータスイープ軸.Pageサイズ,"SegmentSize"=>パラメータスイープ軸.Segmentサイズ,"MemorySize"=>パラメータスイープ軸.Memoryサイズ,_=>throw new InvalidDataException("不明なスイープです: "+値)};
    private static string 失敗種別を保存値へ(パラメータスイープ失敗種別 値)=>値 switch{パラメータスイープ失敗種別.完了=>"Completed",パラメータスイープ失敗種別.ログ満杯=>"LogFull",パラメータスイープ失敗種別.メモリ不足=>"OutOfMemory",パラメータスイープ失敗種別.タイムアウト=>"Timeout",パラメータスイープ失敗種別.無効な構成=>"InvalidConfiguration",パラメータスイープ失敗種別.例外=>"Exception",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static パラメータスイープ失敗種別 保存値を失敗種別へ(string 値)=>値 switch{"Completed"=>パラメータスイープ失敗種別.完了,"LogFull"=>パラメータスイープ失敗種別.ログ満杯,"OutOfMemory"=>パラメータスイープ失敗種別.メモリ不足,"Timeout"=>パラメータスイープ失敗種別.タイムアウト,"InvalidConfiguration"=>パラメータスイープ失敗種別.無効な構成,"Exception"=>パラメータスイープ失敗種別.例外,_=>throw new InvalidDataException("不明な失敗種別です: "+値)};
}

public sealed class パラメータスイープ比較表示行(パラメータスイープ結果 今回,パラメータスイープ比較行? 比較){
    private const double ゲージ全幅=76;
    public パラメータスイープ結果 今回{get;}=今回;
    public string ケース名=>this.今回.ケース名;
    public string コレクション=>this.今回.コレクション.ToString();
    public string ジェネリック=>this.今回.ジェネリック名;
    public string バックエンド=>this.今回.バックエンド.ToString();
    public string スイープ=>this.今回.スイープ軸.ToString();
    public string Indexサイズ=>パラメータスイープフォーマッター.サイズを整形(this.今回.Indexサイズ);
    public string Pageサイズ=>パラメータスイープフォーマッター.サイズを整形(this.今回.Pageサイズ);
    public string Segmentサイズ=>パラメータスイープフォーマッター.サイズを整形(this.今回.Segmentサイズ);
    public string Memoryサイズ=>パラメータスイープフォーマッター.サイズを整形(this.今回.Memoryサイズ);
    public string 可変Fraction=>this.今回.可変Fraction.ToString("0.###",CultureInfo.InvariantCulture);
    public string 最大成功Count=>this.今回.最大成功Count.ToString("N0",CultureInfo.InvariantCulture);
    public string 失敗種別=>this.今回.失敗種別.ToString();
    public string 追加経過=>this.今回.追加経過ミリ秒.ToString("0.###",CultureInfo.InvariantCulture)+" ms";
    public string 検索平均=>this.今回.検索平均ナノ秒?.ToString("0.###",CultureInfo.InvariantCulture)+" ns"??"-";
    public string 最大Count差分=>比較?.最大Count差分?.ToString("+0;-0;0",CultureInfo.InvariantCulture)??"-";
    public string 追加時間シェア表示=>シェアを表示(比較?.今回追加時間シェア,比較?.前回追加時間シェア);
    public string 検索時間シェア表示=>シェアを表示(比較?.今回検索時間シェア,比較?.前回検索時間シェア);
    public double 今回追加時間ゲージ幅=>(比較?.今回追加時間シェア??0)*ゲージ全幅;
    public double 前回追加時間ゲージ幅=>(比較?.前回追加時間シェア??0)*ゲージ全幅;
    public double 今回検索時間ゲージ幅=>(比較?.今回検索時間シェア??0)*ゲージ全幅;
    public double 前回検索時間ゲージ幅=>(比較?.前回検索時間シェア??0)*ゲージ全幅;
    public string 失敗種別変更表示=>比較 is { 失敗種別変更か:true } ? "変更" : "";
    private static string シェアを表示(double? 今回,double? 前回)=>今回 is { } 今回値&&前回 is { } 前回値 ? 今回値.ToString("0.00",CultureInfo.InvariantCulture)+":"+前回値.ToString("0.00",CultureInfo.InvariantCulture) : "-";
}
