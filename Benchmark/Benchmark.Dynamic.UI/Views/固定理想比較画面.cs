using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using Benchmark.Infrastructure;
using System.Diagnostics;

namespace Benchmark.Dynamic.UI;

public sealed class 固定理想比較画面:ContentView{
    public アプリ操作排他? 実行排他{get;set;}
    private readonly Picker ケース=new(){Title="固定ケース",AutomationId="理想比較.ケース"};
    private readonly Entry 件数=new(){Text="256",Keyboard=Keyboard.Numeric,AutomationId="理想比較.件数"};
    private readonly Label 状態=new(){Text="待機中",AutomationId="理想比較.状態"};
    private readonly Button 開始=new(){Text="保存した参照コードと実測比較",AutomationId="理想比較.開始"};
    private CancellationTokenSource? 取消;
    private Task? 実行タスク;
    public 固定理想比較画面(Func<int> 最適化を取得){
        this.ケース.Items.Add("すべて（25件）");
        foreach(var 項目 in FixedCaseCatalog.All){this.ケース.Items.Add(項目.ID+" "+string.Join(" → ",項目.Methods));}
        this.ケース.SelectedIndex=0;
        var 中止=new Button{Text="中止"};中止.Clicked+=(_,_)=>this.取消?.Cancel();
        this.開始.Clicked+=async (_,_)=>{
            if(this.取消 is not null||this.実行排他?.実行中か==true){return;}
            var 実行=this.実行Async(最適化を取得);
            this.実行タスク=実行;
            await 実行;
        };
        this.Content=new ScrollView{Content=new VerticalStackLayout{Padding=12,Spacing=8,Children={
            new Label{Text="理想コード・手書き参照との比較",FontSize=20},new Label{Text="旧チェーン検証器の25件を同じ式・入力で実測します。Idealは保存した参照実装です。一部はLINQを含み、最適性は保証されていません。対応する参照実装がないクエリには理想時間を作りません。"},this.ケース,this.件数,this.開始,中止,this.状態
        }}};
    }
    private async Task 実行Async(Func<int> 最適化を取得){
        if(this.取消 is not null){return;}
        using var 予約=this.実行排他?.予約する();
        if(this.実行排他 is not null&&予約 is null){this.状態.Text="別の操作の実行またはcleanupを待っています。";return;}
        using var 取消=new CancellationTokenSource();this.取消=取消;this.開始.IsEnabled=false;
        try{
            SystemResources.Check(true);
            var 起動情報=VersionedJsonLineWorkerProcessClient.CreateStartInfo(動的実行調停サービス.Workerパスを解決());
            foreach(var 引数 in new[]{"--fixed","--count",int.Parse(this.件数.Text).ToString(),"--flags",最適化を取得().ToString(),"--database",DynamicDatabasePathResolver.Resolve()}){起動情報.ArgumentList.Add(引数);}
            if(this.ケース.SelectedIndex>0){起動情報.ArgumentList.Add("--case");起動情報.ArgumentList.Add(FixedCaseCatalog.All[this.ケース.SelectedIndex-1].ID);}
            using var プロセス=Process.Start(起動情報)??throw new IOException("Workerを開始できませんでした。");
            using var 登録=取消.Token.Register(()=>{try{if(!プロセス.HasExited){プロセス.Kill(true);}}catch(InvalidOperationException){}});
            var エラー=プロセス.StandardError.ReadToEndAsync();
            var 標準出力=プロセス.StandardOutput.ReadToEndAsync();
            try{
                while(!プロセス.HasExited){await Task.Delay(500,取消.Token);SystemResources.Check();}
                var 出力=await 標準出力;
                if(プロセス.ExitCode!=0){throw new InvalidOperationException(await エラー);}
                this.状態.Text="保存済み。共有履歴でDynamic.Fixedを選ぶと、System・FLinqUnoptimized・FLinq・Idealの実測値を比較できます。\n"+出力;
            }finally{if(!プロセス.HasExited){プロセス.Kill(true);await プロセス.WaitForExitAsync();}}
        }catch(OperationCanceledException){this.状態.Text="中止しました。途中の測定を合格として扱いません。";}
        catch(Exception 例外){this.状態.Text=例外.Message;}
        finally{this.取消=null;this.開始.IsEnabled=true;}
    }
    public void 終了する()=>this.取消?.Cancel();
    public async Task 終了Async(){
        this.取消?.Cancel();
        if(this.実行タスク is{} 実行){await 実行;}
    }
}
