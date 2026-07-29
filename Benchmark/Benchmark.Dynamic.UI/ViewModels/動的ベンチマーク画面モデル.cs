using Benchmark.Dynamic.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Benchmark.Dynamic.UI;

public sealed class 動的ベンチマーク画面モデル:ObservableObject {
    private const string 選択ソース設定キー="Benchmark.Dynamic.選択ソース";
    private readonly I動的実行調停サービス 実行調停;
    private readonly I動的UI設定 UI設定;
    private int Query数状態=10;
    private int N状態=8;
    private int 入力要素数状態=1024;
    private int 最大深度状態=8;
    private string Seed文字列状態="0x5EED5EED5EED5EED";
    private OperatorSetMode 演算子集合モード状態=OperatorSetMode.Common;
    private bool 実行中状態か;
    private bool キャンセル要求状態か;
    private string 進捗文字列状態="待機中";
    private int 完了数状態;
    private int コンパイル失敗数状態;
    private int 意味不一致数状態;
    private int 非収束数状態;
    private int 実行例外数状態;
    private 動的結果行? 選択結果状態;
    private double 設定領域幅状態;
    private double 詳細領域高さ状態;
    private bool 設定領域折畳状態か;
    private bool 詳細領域折畳状態か;

    public 動的ベンチマーク画面モデル(I動的実行調停サービス 実行調停,I動的UI設定 UI設定) {
        this.実行調停=実行調停;
        this.UI設定=UI設定;
        this.結果一覧=[];
        this.履歴一覧=[];
        this.ごみ箱一覧=[];
        foreach(var 項目 in this.実行調停.履歴を取得(false)) {
            this.履歴一覧.Add(項目);
        }
        foreach(var 項目 in this.実行調停.履歴を取得(true)) {
            this.ごみ箱一覧.Add(項目);
        }
        this.ソース選択一覧=作成ソース選択一覧();
        this.ソース画面一覧=this.ソース選択一覧.ToDictionary(
            項目=>項目.対象,
            項目=>new 動的ソース結果画面モデル(項目.対象,this.結果一覧));
        foreach(var 項目 in this.ソース選択一覧) {
            項目.PropertyChanged+=this.ソース選択変更時;
        }

        this.Query数状態=Math.Clamp(this.UI設定.取得("Benchmark.Dynamic.Query数",10),1,1_000);
        this.N状態=Math.Clamp(this.UI設定.取得("Benchmark.Dynamic.N",8),1,64);
        this.入力要素数状態=Math.Clamp(this.UI設定.取得("Benchmark.Dynamic.入力要素数",1024),1,100_000);
        this.最大深度状態=Math.Min(
            Math.Max(1,this.UI設定.取得("Benchmark.Dynamic.最大深度",Math.Min(this.N状態,8))),
            Math.Min(this.N状態,32));
        var 保存Seed=this.UI設定.取得("Benchmark.Dynamic.Seed","0x5EED5EED5EED5EED");
        this.Seed文字列状態=Seed256.TryParse(保存Seed,out _)
            ?保存Seed
            :"0x5EED5EED5EED5EED";
        this.演算子集合モード状態=this.UI設定.取得("Benchmark.Dynamic.演算子集合モード","Common")=="Maximum"
            ?OperatorSetMode.Maximum
            :OperatorSetMode.Common;
        this.設定領域幅状態=Math.Clamp(this.UI設定.取得("Benchmark.Dynamic.設定領域幅",320d),280d,480d);
        this.詳細領域高さ状態=Math.Max(180d,this.UI設定.取得("Benchmark.Dynamic.詳細領域高さ",280d));
        this.設定領域折畳状態か=this.UI設定.取得("Benchmark.Dynamic.設定領域折畳",false);
        this.詳細領域折畳状態か=this.UI設定.取得("Benchmark.Dynamic.詳細領域折畳",false);

        this.実行コマンド=new AsyncRelayCommand(this.実行Async,()=>!this.実行中か);
        this.キャンセルコマンド=new AsyncRelayCommand(this.キャンセルAsync,()=>this.実行中か&&!this.キャンセル要求済みか);
        this.新規Seedコマンド=new RelayCommand(this.新規Seedを作成,()=>!this.実行中か);
        this.設定領域折畳コマンド=new RelayCommand(this.設定領域を切り替える);
        this.詳細領域折畳コマンド=new RelayCommand(this.詳細領域を切り替える);
        this.ごみ箱へ移動コマンド=new AsyncRelayCommand<動的履歴項目>(this.ごみ箱へ移動Async,項目=>項目 is not null&&!this.実行中か);
        this.復元コマンド=new AsyncRelayCommand<動的履歴項目>(this.復元Async,項目=>項目 is not null&&!this.実行中か);
        this.完全削除コマンド=new AsyncRelayCommand<動的履歴項目>(this.完全削除Async,項目=>項目 is not null&&!this.実行中か);
        this.Run再実行コマンド=new AsyncRelayCommand<動的履歴項目>(this.Runを再実行Async,項目=>項目 is not null&&!this.実行中か);
        this.Query再実行コマンド=new AsyncRelayCommand<動的結果行>(this.Queryを再実行Async,行=>行 is not null&&!this.実行中か);
        this.Query縮小コマンド=new AsyncRelayCommand<動的結果行>(this.Queryを縮小Async,行=>行 is not null&&!this.実行中か);
        this.選択Query再実行コマンド=new AsyncRelayCommand<IReadOnlyList<動的結果行>>(
            this.選択Queryを再実行Async,
            一覧=>一覧 is{Count:>0}&&!this.実行中か);
        this.設定復元Seed再生成コマンド=new RelayCommand<動的履歴項目>(this.設定を復元してSeedを再生成,項目=>項目 is not null&&!this.実行中か);
    }

    public ObservableCollection<動的ソース選択項目> ソース選択一覧{get;}
    public IReadOnlyDictionary<SourceTarget,動的ソース結果画面モデル> ソース画面一覧{get;}
    public ObservableCollection<動的結果行> 結果一覧{get;}
    public ObservableCollection<動的履歴項目> 履歴一覧{get;}
    public ObservableCollection<動的履歴項目> ごみ箱一覧{get;}
    public IAsyncRelayCommand 実行コマンド{get;}
    public IAsyncRelayCommand キャンセルコマンド{get;}
    public IRelayCommand 新規Seedコマンド{get;}
    public IRelayCommand 設定領域折畳コマンド{get;}
    public IRelayCommand 詳細領域折畳コマンド{get;}
    public IAsyncRelayCommand<動的履歴項目> ごみ箱へ移動コマンド{get;}
    public IAsyncRelayCommand<動的履歴項目> 復元コマンド{get;}
    public IAsyncRelayCommand<動的履歴項目> 完全削除コマンド{get;}
    public IAsyncRelayCommand<動的履歴項目> Run再実行コマンド{get;}
    public IAsyncRelayCommand<動的結果行> Query再実行コマンド{get;}
    public IAsyncRelayCommand<動的結果行> Query縮小コマンド{get;}
    public IAsyncRelayCommand<IReadOnlyList<動的結果行>> 選択Query再実行コマンド{get;}
    public IRelayCommand<動的履歴項目> 設定復元Seed再生成コマンド{get;}

    public int Query数 {
        get=>this.Query数状態;
        set {
            var 値=Math.Clamp(value,1,1000);
            if(this.SetProperty(ref this.Query数状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.Query数",値);
            }
        }
    }

    public int N {
        get=>this.N状態;
        set {
            var 値=Math.Clamp(value,1,64);
            if(this.SetProperty(ref this.N状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.N",値);
                this.最大深度=Math.Min(this.最大深度,Math.Min(値,32));
                this.OnPropertyChanged(nameof(this.最大深度上限));
            }
        }
    }

    public int 入力要素数 {
        get=>this.入力要素数状態;
        set {
            var 値=Math.Clamp(value,1,100000);
            if(this.SetProperty(ref this.入力要素数状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.入力要素数",値);
            }
        }
    }

    public int 最大深度 {
        get=>this.最大深度状態;
        set {
            var 値=Math.Clamp(value,1,this.最大深度上限);
            if(this.SetProperty(ref this.最大深度状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.最大深度",値);
            }
        }
    }

    public int 最大深度上限=>Math.Min(this.N,32);

    public string Seed文字列 {
        get=>this.Seed文字列状態;
        set {
            if(this.SetProperty(ref this.Seed文字列状態,value)) {
                this.UI設定.保存("Benchmark.Dynamic.Seed",value);
            }
        }
    }

    public OperatorSetMode 演算子集合モード {
        get=>this.演算子集合モード状態;
        set {
            if(this.SetProperty(ref this.演算子集合モード状態,value)) {
                this.UI設定.保存("Benchmark.Dynamic.演算子集合モード",value.ToString());
                this.OnPropertyChanged(nameof(this.Common選択か));
                this.OnPropertyChanged(nameof(this.Maximum選択か));
            }
        }
    }

    public bool Common選択か {
        get=>this.演算子集合モード==OperatorSetMode.Common;
        set {
            if(value) {
                this.演算子集合モード=OperatorSetMode.Common;
            }
        }
    }

    public bool Maximum選択か {
        get=>this.演算子集合モード==OperatorSetMode.Maximum;
        set {
            if(value) {
                this.演算子集合モード=OperatorSetMode.Maximum;
            }
        }
    }

    public bool 実行中か {
        get=>this.実行中状態か;
        private set {
            if(this.SetProperty(ref this.実行中状態か,value)) {
                this.OnPropertyChanged(nameof(this.設定入力可能か));
                this.コマンド状態を更新();
            }
        }
    }

    public bool 設定入力可能か=>!this.実行中か;

    public bool キャンセル要求済みか {
        get=>this.キャンセル要求状態か;
        private set {
            if(this.SetProperty(ref this.キャンセル要求状態か,value)) {
                this.コマンド状態を更新();
            }
        }
    }

    public string 進捗文字列 {
        get=>this.進捗文字列状態;
        private set=>this.SetProperty(ref this.進捗文字列状態,value);
    }

    public int 完了数 {
        get=>this.完了数状態;
        private set=>this.SetProperty(ref this.完了数状態,value);
    }

    public int コンパイル失敗数 {
        get=>this.コンパイル失敗数状態;
        private set=>this.SetProperty(ref this.コンパイル失敗数状態,value);
    }

    public int 意味不一致数 {
        get=>this.意味不一致数状態;
        private set=>this.SetProperty(ref this.意味不一致数状態,value);
    }

    public int 非収束数 {
        get=>this.非収束数状態;
        private set=>this.SetProperty(ref this.非収束数状態,value);
    }

    public int 実行例外数 {
        get=>this.実行例外数状態;
        private set=>this.SetProperty(ref this.実行例外数状態,value);
    }

    public 動的結果行? 選択結果 {
        get=>this.選択結果状態;
        set=>this.SetProperty(ref this.選択結果状態,value);
    }

    public double 設定領域幅 {
        get=>this.設定領域幅状態;
        set {
            var 値=Math.Clamp(value,280d,480d);
            if(this.SetProperty(ref this.設定領域幅状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.設定領域幅",値);
            }
        }
    }

    public double 詳細領域高さ {
        get=>this.詳細領域高さ状態;
        set {
            var 値=Math.Max(180d,value);
            if(this.SetProperty(ref this.詳細領域高さ状態,値)) {
                this.UI設定.保存("Benchmark.Dynamic.詳細領域高さ",値);
            }
        }
    }

    public bool 設定領域折畳済みか {
        get=>this.設定領域折畳状態か;
        private set {
            if(this.SetProperty(ref this.設定領域折畳状態か,value)) {
                this.UI設定.保存("Benchmark.Dynamic.設定領域折畳",value);
            }
        }
    }

    public bool 詳細領域折畳済みか {
        get=>this.詳細領域折畳状態か;
        private set {
            if(this.SetProperty(ref this.詳細領域折畳状態か,value)) {
                this.UI設定.保存("Benchmark.Dynamic.詳細領域折畳",value);
            }
        }
    }

    public void 進捗を適用(動的実行進捗 進捗) {
        this.進捗文字列=$"Query {進捗.QueryIndex+1}/{進捗.Query総数} ・ {進捗.対象} ・ {進捗.段階}";
        this.完了数=進捗.完了数;
        this.コンパイル失敗数=進捗.コンパイル失敗数;
        this.意味不一致数=進捗.意味不一致数;
        this.非収束数=進捗.非収束数;
        this.実行例外数=進捗.実行例外数;
    }

    private async Task 実行Async() {
        var 選択対象=this.ソース選択一覧.Where(項目=>項目.選択済みか).Select(項目=>項目.対象).Order().ToArray();
        if(選択対象.Length==0) {
            await 通知Async("入力エラー","少なくとも1つのSourceTargetを選択してください。");
            return;
        }
        if(!Seed256.TryParse(this.Seed文字列,out var Seed)) {
            await 通知Async("入力エラー","Seedは符号なし64bitの10進または0x付き16進で入力してください。");
            return;
        }

        this.実行中か=true;
        this.キャンセル要求済みか=false;
        this.進捗文字列="実行を開始しています";
        this.完了数=0;
        this.コンパイル失敗数=0;
        this.意味不一致数=0;
        this.非収束数=0;
        this.実行例外数=0;
        var 設定=new 動的実行設定(
            Guid.NewGuid().ToString("N"),
            this.Query数,
            this.N,
            this.入力要素数,
            this.最大深度,
            Seed,
            this.演算子集合モード,
            選択対象);
        try {
            await using var UI更新=new 動的UI更新Channel(
                行=>this.結果一覧.Add(行),
                this.進捗を適用);
            var 履歴=await this.実行調停.実行Async(設定,UI更新.結果を追加,UI更新.進捗を更新);
            await UI更新.完了Async();
            this.履歴一覧.Insert(0,履歴);
            this.進捗文字列=履歴.状態;
        } catch(OperationCanceledException) {
            this.進捗文字列="キャンセル済み";
        } catch(Exception 例外) {
            this.進捗文字列="Run致命障害: "+例外.Message;
        } finally {
            this.実行中か=false;
        }
    }

    private async Task キャンセルAsync() {
        this.キャンセル要求済みか=true;
        this.進捗文字列="キャンセル要求中";
        await this.実行調停.キャンセルAsync();
    }

    private Task Runを再実行Async(動的履歴項目? 項目) {
        return 項目 is null
            ?Task.CompletedTask
            :this.再実行Async((結果受信時,進捗受信時)=>
                this.実行調停.Runを再実行Async(項目.実行ID,結果受信時,進捗受信時));
    }

    private Task Queryを再実行Async(動的結果行? 行) {
        return 行 is null
            ?Task.CompletedTask
            :this.再実行Async((結果受信時,進捗受信時)=>
                this.実行調停.Queryを再実行Async(行.QueryID,結果受信時,進捗受信時));
    }

    private Task Queryを縮小Async(動的結果行? 行) {
        return 行 is null
            ?Task.CompletedTask
            :this.再実行Async((結果受信時,進捗受信時)=>
                this.実行調停.Queryを縮小Async(行.QueryID,結果受信時,進捗受信時));
    }

    private Task 選択Queryを再実行Async(IReadOnlyList<動的結果行>? 一覧) {
        return 一覧 is not{Count:>0}
            ?Task.CompletedTask
            :this.再実行Async((結果受信時,進捗受信時)=>
                this.実行調停.複数Queryを再実行Async(
                    一覧.Select(static 行=>行.QueryID).Distinct().ToArray(),
                    結果受信時,
                    進捗受信時));
    }

    private async Task 再実行Async(
        Func<Action<動的結果行>,Action<動的実行進捗>,Task<動的履歴項目>> 開始){
        this.実行中か=true;
        this.キャンセル要求済みか=false;
        this.進捗文字列="再実行を開始しています";
        this.完了数=0;
        this.コンパイル失敗数=0;
        this.意味不一致数=0;
        this.非収束数=0;
        this.実行例外数=0;
        try{
            await using var UI更新=new 動的UI更新Channel(
                行=>this.結果一覧.Add(行),
                this.進捗を適用);
            var 履歴=await 開始(UI更新.結果を追加,UI更新.進捗を更新);
            await UI更新.完了Async();
            this.履歴一覧.Insert(0,履歴);
            this.進捗文字列=履歴.状態;
        }catch(OperationCanceledException){
            this.進捗文字列="キャンセル済み";
        }catch(Exception 例外){
            this.進捗文字列="再実行失敗: "+例外.Message;
        }finally{
            this.実行中か=false;
        }
    }

    private void 設定を復元してSeedを再生成(動的履歴項目? 項目) {
        if(項目 is null) {
            return;
        }
        var 条件=this.実行調停.実行条件を取得(項目.実行ID);
        this.Query数=条件.QueryCount;
        this.N=条件.OperatorCount;
        this.入力要素数=条件.InputElementCount;
        this.最大深度=条件.MaximumDepth;
        this.演算子集合モード=条件.Mode;
        var 選択=条件.SourceTargets.ToHashSet();
        foreach(var Source in this.ソース選択一覧) {
            Source.選択済みか=選択.Contains(Source.対象);
        }
        this.新規Seedを作成();
        this.進捗文字列="設定を復元し、新しいSeedを生成しました";
    }

    private void 新規Seedを作成() {
        Span<byte> バイト=stackalloc byte[8];
        System.Security.Cryptography.RandomNumberGenerator.Fill(バイト);
        this.Seed文字列="0x"+Convert.ToHexString(バイト);
    }

    private void 設定領域を切り替える() {
        this.設定領域折畳済みか=!this.設定領域折畳済みか;
    }

    private void 詳細領域を切り替える() {
        this.詳細領域折畳済みか=!this.詳細領域折畳済みか;
    }

    private async Task ごみ箱へ移動Async(動的履歴項目? 項目) {
        if(項目 is null) {
            return;
        }
        await this.実行調停.ごみ箱へ移動Async(項目.実行ID);
        this.履歴一覧.Remove(項目);
        項目.削除済みか=true;
        this.ごみ箱一覧.Insert(0,項目);
    }

    private async Task 復元Async(動的履歴項目? 項目) {
        if(項目 is null) {
            return;
        }
        await this.実行調停.復元Async(項目.実行ID);
        this.ごみ箱一覧.Remove(項目);
        項目.削除済みか=false;
        this.履歴一覧.Insert(0,項目);
    }

    private async Task 完全削除Async(動的履歴項目? 項目) {
        if(項目 is null) {
            return;
        }
        var 確認=await 確認Async("完全削除","このRunと子データを完全に削除します。元に戻せません。");
        if(!確認) {
            return;
        }
        await this.実行調停.完全削除Async(項目.実行ID);
        this.ごみ箱一覧.Remove(項目);
    }

    private void ソース選択変更時(object? 送信元,PropertyChangedEventArgs 引数) {
        if(引数.PropertyName!=nameof(動的ソース選択項目.選択済みか)) {
            return;
        }
        var 値=string.Join(",",this.ソース選択一覧.Where(項目=>項目.選択済みか).Select(項目=>項目.対象.ToString()));
        this.UI設定.保存(選択ソース設定キー,値);
    }

    private static async Task 通知Async(string 見出し,string 内容) {
        if(Application.Current?.Windows.FirstOrDefault()?.Page is { } Page) {
            await Page.DisplayAlertAsync(見出し,内容,"OK");
        }
    }

    private static async Task<bool> 確認Async(string 見出し,string 内容) {
        return Application.Current?.Windows.FirstOrDefault()?.Page is { } Page
            &&await Page.DisplayAlertAsync(見出し,内容,"削除","キャンセル");
    }

    private void コマンド状態を更新() {
        this.実行コマンド.NotifyCanExecuteChanged();
        this.キャンセルコマンド.NotifyCanExecuteChanged();
        this.新規Seedコマンド.NotifyCanExecuteChanged();
        this.ごみ箱へ移動コマンド.NotifyCanExecuteChanged();
        this.復元コマンド.NotifyCanExecuteChanged();
        this.完全削除コマンド.NotifyCanExecuteChanged();
        this.Run再実行コマンド.NotifyCanExecuteChanged();
        this.Query再実行コマンド.NotifyCanExecuteChanged();
        this.Query縮小コマンド.NotifyCanExecuteChanged();
        this.設定復元Seed再生成コマンド.NotifyCanExecuteChanged();
    }

    private ObservableCollection<動的ソース選択項目> 作成ソース選択一覧() {
        var 保存値=this.UI設定.取得(選択ソース設定キー,string.Empty);
        var 保存済み=保存値.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var 有効な保存済み=Enum.GetValues<SourceTarget>()
            .Where(対象=>保存済み.Contains(対象.ToString()))
            .ToHashSet();
        var 初回=有効な保存済み.Count==0;
        return [
            new(SourceTarget.SystemArray,"System","Array",初回||有効な保存済み.Contains(SourceTarget.SystemArray)),
            new(SourceTarget.SystemIEnumerable,"System","IEnumerable",有効な保存済み.Contains(SourceTarget.SystemIEnumerable)),
            new(SourceTarget.SystemRange,"System","Range",有効な保存済み.Contains(SourceTarget.SystemRange)),
            new(SourceTarget.SystemList,"System","List",有効な保存済み.Contains(SourceTarget.SystemList)),
            new(SourceTarget.SystemPersisted,"System","Persisted",有効な保存済み.Contains(SourceTarget.SystemPersisted)),
            new(SourceTarget.SystemTsavorite,"System","Tsavorite",有効な保存済み.Contains(SourceTarget.SystemTsavorite)),
            new(SourceTarget.SeamlessPersisted,"Seamless","Persisted",有効な保存済み.Contains(SourceTarget.SeamlessPersisted)),
            new(SourceTarget.SeamlessTsavorite,"Seamless","Tsavorite",有効な保存済み.Contains(SourceTarget.SeamlessTsavorite)),
        ];
    }
}
