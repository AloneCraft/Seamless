using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using Benchmark.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Benchmark.Dynamic.UI;

public sealed class 回帰ケース画面:ContentView{
    public アプリ操作排他? 実行排他{get;set;}
    private readonly SearchBar 検索=new(){Placeholder="ケースID・型・メソッドで検索",AutomationId="回帰ケース.検索"};
    private readonly CollectionView 一覧=new(){SelectionMode=SelectionMode.Single,HeightRequest=260,AutomationId="回帰ケース.一覧"};
    private readonly Editor 詳細=new(){IsReadOnly=true,HeightRequest=260,AutomationId="回帰ケース.詳細"};
    private readonly Label 状態=new(){Text="ケース一覧を読み込んでください。",AutomationId="回帰ケース.状態"};
    private readonly Button 読込=new(){Text="一覧を読み込む",AutomationId="回帰ケース.読込"};
    private readonly Button 選択実行=new(){Text="選択ケースを実行・再実行",AutomationId="回帰ケース.実行"};
    private readonly Button 全実行=new(){Text="すべて実行",AutomationId="回帰ケース.全実行"};
    private IReadOnlyList<RegressionCaseDescriptor> ケース=[];
    private CancellationTokenSource? 取消;
    private Task? 実行タスク;
    private string? 展開ファイル;
    public 回帰ケース画面(){
        this.一覧.ItemTemplate=new DataTemplate(()=>{
            var 表示=new Label{FontSize=12,Padding=4};表示.SetBinding(Label.TextProperty,nameof(RegressionCaseDescriptor.DisplayName));return 表示;
        });
        this.検索.TextChanged+=(_,_)=>this.一覧を更新();
        this.一覧.SelectionChanged+=(_,変更)=>{
            if(変更.CurrentSelection.FirstOrDefault() is not RegressionCaseDescriptor 対象ケース){return;}
            var 実行状態="実行可能";
            if(対象ケース.Skip is{} 未実行理由){実行状態="未実行: "+未実行理由;}
            var 比較対象="ケース固有のassertion（ZLinq比較は共通カタログのケースで実行）";
            if(対象ケース.Comparisons.Count>0){比較対象=string.Join(" / ",対象ケース.Comparisons);}
            this.詳細.Text=対象ケース.DisplayName+"\n"+実行状態+"\n比較: "+比較対象+"\n\n"+
                string.Join("\n\n",RegressionCaseSources.Legacy.Documents.Where(文書=>対象ケース.SourcePaths.Contains(文書.Path)).Select(文書=>文書.Path+"\n"+文書.Text));
        };
        this.読込.Clicked+=async (_,_)=>await this.開始Async(true,null);
        this.選択実行.Clicked+=async (_,_)=>{
            if(this.一覧.SelectedItem is RegressionCaseDescriptor 対象ケース){await this.開始Async(false,対象ケース.ID);}
            else{this.状態.Text="ケースを選択してください。";}
        };
        this.全実行.Clicked+=async (_,_)=>await this.開始Async(false,null);
        var 中止=new Button{Text="中止",AutomationId="回帰ケース.中止"};中止.Clicked+=(_,_)=>this.取消?.Cancel();
        var 展開表示=new Button{Text="今回の展開コードをプレビュー（全ケース）",AutomationId="回帰ケース.展開コード"};
        展開表示.Clicked+=async (_,_)=>{
            try{
                if(this.展開ファイル is{} パス&&File.Exists(パス)){
                    using var 入力=File.OpenText(パス);
                    var バッファ=new char[16*1024];
                    var 文字数=await 入力.ReadBlockAsync(バッファ.AsMemory());
                    this.詳細.Text="展開コードのプレビュー（最大16,384文字）\n全文ファイル: "+パス+"\n\n"+new string(バッファ,0,文字数);
                }
                else{this.詳細.Text="展開コードは、ケースを実行すると保存されます。";}
            }
            catch(Exception 例外){this.詳細.Text=例外.Message;}
        };
        this.Content=new ScrollView{Content=new VerticalStackLayout{Spacing=8,Padding=12,Children={
            new Label{Text="FLinq回帰ケース",FontSize=20},
            new Label{Text="自動テストと同じソース・期待値で、結果、例外、評価回数、解放とフォールバックを確認します。ZLinq比較を含むケースも同じ一覧から実行できます。"},
            this.検索,this.読込,this.一覧,this.選択実行,this.全実行,中止,展開表示,this.状態,this.詳細,
        }}};
    }
    private async Task 開始Async(bool 一覧だけか,string? ケースID){
        if(this.取消 is not null){return;}
        using var 予約=this.実行排他?.予約する();
        if(this.実行排他 is not null&&予約 is null){this.状態.Text="別の操作の終了を待っています。";return;}
        using var 取消=new CancellationTokenSource();this.取消=取消;
        this.読込.IsEnabled=this.選択実行.IsEnabled=this.全実行.IsEnabled=false;
        var 処理=this.実行Async(一覧だけか,ケースID,取消.Token);this.実行タスク=処理;
        try{await 処理;}finally{this.取消=null;this.読込.IsEnabled=this.選択実行.IsEnabled=this.全実行.IsEnabled=true;}
    }
    private async Task 実行Async(bool 一覧だけか,string? ケースID,CancellationToken 取消トークン){
        try{
            this.状態.Text="Workerでソース一式をコンパイルしています。";
            var 開始情報=VersionedJsonLineWorkerProcessClient.CreateStartInfo(動的実行調停サービス.Workerパスを解決());
            開始情報.ArgumentList.Add("--regressions");if(一覧だけか){開始情報.ArgumentList.Add("--list");}
            string? 結果ファイル=null;
            if(!一覧だけか){
                var データベース=DynamicDatabasePathResolver.Resolve();結果ファイル=Path.Combine(Path.GetDirectoryName(データベース)!,"RegressionRuns",Guid.NewGuid().ToString("N")+".jsonl");
                this.展開ファイル=結果ファイル+".generated.cs";
                foreach(var 引数 in new[]{"--database",データベース,"--results",結果ファイル}){開始情報.ArgumentList.Add(引数);}
            }
            if(ケースID is not null){開始情報.ArgumentList.Add("--case");開始情報.ArgumentList.Add(ケースID);}
            using var プロセス=Process.Start(開始情報)??throw new IOException("Workerを開始できませんでした。");
            using var 中止登録=取消トークン.Register(()=>{try{if(!プロセス.HasExited){プロセス.Kill(true);}}catch(InvalidOperationException){}});
            var エラー=プロセス.StandardError.ReadToEndAsync();var 出力=プロセス.StandardOutput.ReadToEndAsync();
            try{
                await プロセス.WaitForExitAsync(取消トークン);var テキスト=await 出力;
                if(プロセス.ExitCode!=0){this.詳細.Text=テキスト+"\n"+await エラー;this.状態.Text="失敗（未実行・中止を合格に含めません）";return;}
                var 行一覧=テキスト.Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(行=>行.TrimStart().StartsWith('{')).ToArray();
                if(一覧だけか){
                    this.ケース=行一覧.Select(行=>JsonSerializer.Deserialize<RegressionCaseDescriptor>(行)!).ToArray();
                    this.一覧を更新();this.状態.Text=$"{this.ケース.Count}ケース。選択して実行できます。";
                }else{
                    var 結果一覧=行一覧.Select(行=>JsonSerializer.Deserialize<RegressionCaseResult>(行)!).ToArray();
                    this.詳細.Text=string.Join("\n\n",結果一覧.Select(結果=>結果.ID+"\n"+結果.Status+" fallback="+結果.FallbackCount+"\n"+結果.Error+"\n"+結果.Output));
                    this.状態.Text=$"完了: 合格 {結果一覧.Count(結果=>結果.Status=="Passed")} / 失敗 {結果一覧.Count(結果=>結果.Status=="Failed")} / 未実行 {結果一覧.Count(結果=>結果.Status=="Skipped")}\n保存先: {結果ファイル}";
                }
            }finally{if(!プロセス.HasExited){プロセス.Kill(true);await プロセス.WaitForExitAsync();}}
        }catch(OperationCanceledException){this.状態.Text="中止しました。途中結果は合格扱いにしません。";}
        catch(Exception 例外){this.状態.Text="実行失敗";this.詳細.Text=例外.ToString();}
    }
    private void 一覧を更新()=>this.一覧.ItemsSource=this.ケース.Where(対象ケース=>対象ケース.DisplayName.Contains(this.検索.Text??"",StringComparison.OrdinalIgnoreCase)).ToArray();
    public void 終了する()=>this.取消?.Cancel();
    public async Task 終了Async(){this.取消?.Cancel();if(this.実行タスク is{} 処理){await 処理;}}
}
