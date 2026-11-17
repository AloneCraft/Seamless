using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Seamless.Management;

namespace Seamless.Management.Components;

public partial class 生成コードフロー{
    [Parameter,EditorRequired]public GeneratedExecutionCode コード{get;set;}=default!;
    private GeneratedExecutionCode? 前コード;
    private 生成コード配置 配置=new();
    private int? 選択ID;
    private GeneratedOperation? 選択操作;
    private int 操作頁;
    private string 検索="";
    private bool ハッシュ適合か,全文表示か,全文選択待ちか,全体表示か=true,破棄済みか;
    private double 倍率=100,実倍率;
    private double? 窓上,窓下;
    private long 世代,通知済み世代,最終通知;
    private 生成配置ブロック[] 詳細=[],簡略=[];
    private 生成配置接続[] 表示接続=[];
    private readonly Dictionary<int,string> 説明一覧=[];
    private ElementReference 画面;
    private コードエディター? 全文;
    private IJSObjectReference? モジュール,操作;
    private DotNetObjectReference<生成コードフロー>? 参照;
    private readonly CancellationTokenSource 寿命=new();
    private readonly SemaphoreSlim 初期化=new(1,1);
    private readonly string 矢印ID="generated-flow-"+Guid.NewGuid().ToString("N");
    private 生成配置ブロック? 選択=>this.配置.ブロック.FirstOrDefault(項目=>項目.元.Id==this.選択ID);
    private string 図寸法{get{if(this.全体表示か){return "width:100%;height:620px";}return FormattableString.Invariant($"width:{this.配置.幅*this.倍率/100}px;height:{this.配置.高さ*this.倍率/100}px");}}
    private string 倍率名{get{if(this.全体表示か){return 表示文言.取得("全体表示");}return this.倍率.ToString("0.#",CultureInfo.CurrentCulture)+"%";}}
    private static string 到達性名(bool 到達か){if(到達か){return "静的に到達可能";}return "静的に到達不能";}
    protected override void OnParametersSet(){
        if(ReferenceEquals(this.前コード,this.コード)){return;}
        this.前コード=this.コード;this.配置=生成コード配置.作成(this.コード);this.世代++;this.最終通知=0;this.窓上=null;this.窓下=null;this.実倍率=0;
        this.選択ID=null;this.選択操作=null;this.操作頁=0;this.全文表示か=false;this.ハッシュ適合か=false;this.説明一覧.Clear();
        if(this.コード.BodyText is{} 本文&&本文.Length<=65536){
            try{this.ハッシュ適合か=string.Equals(this.コード.BodyHash,Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false,true).GetBytes(本文))),StringComparison.OrdinalIgnoreCase);}
            catch(EncoderFallbackException){}
        }
        foreach(var ブロック in this.配置.ブロック){
            var 説明=$"B{ブロック.元.Id} · {ブロック.元.Kind}";
            var 元=ブロック.元.Operations.FirstOrDefault(項目=>項目.Start>=0&&項目.Length>0)??ブロック.元.BranchValue;
            if(this.範囲本文(元,512) is{} 断片){説明+="\n"+断片;}
            this.説明一覧.Add(ブロック.元.Id,説明);
        }
        var 最初=this.配置.ブロック.FirstOrDefault(項目=>項目.元.Operations.Length>0)??this.配置.ブロック.FirstOrDefault();
        if(最初 is not null){this.ブロック選択時(最初.元.Id);}
        this.描画を更新();
    }
    private string? 範囲本文(GeneratedOperation? 項目,int 上限=int.MaxValue){
        if(!this.ハッシュ適合か||this.コード.BodyText is not{} 本文||項目 is null||項目.Start<0||項目.Length<=0||項目.Start>本文.Length-項目.Length){return null;}
        var 長さ=Math.Min(項目.Length,上限);
        if(長さ<項目.Length&&長さ>0&&char.IsHighSurrogate(本文[項目.Start+長さ-1])&&char.IsLowSurrogate(本文[項目.Start+長さ])){長さ--;}
        return 本文.Substring(項目.Start,長さ);
    }
    private void 描画を更新(){
        var 大規模か=this.配置.ブロック.Length>64;var 可読か=this.窓上 is not null&&this.実倍率>=20;
        if(!this.全体表示か&&Math.Abs(this.実倍率-this.倍率)>0.01){可読か=false;}
        var 候補=this.配置.ブロック;
        if(大規模か&&可読か){候補=候補.Where(項目=>項目.下端>=this.窓上&&項目.Y<=this.窓下).ToArray();}
        this.詳細=[];this.簡略=[];
        if(大規模か&&!可読か){this.簡略=候補;}else{
            this.詳細=候補.Take(256).ToArray();
            if(this.選択 is{} 選択&&!this.詳細.Contains(選択)){this.詳細=[..this.詳細.Take(255),選択];}
            var 識別=this.詳細.Select(項目=>項目.元.Id).ToHashSet();this.簡略=候補.Where(項目=>!識別.Contains(項目.元.Id)).ToArray();
        }
        this.表示接続=this.配置.接続.Where(辺=>!大規模か||!可読か||辺.下端>=this.窓上&&辺.上端<=this.窓下).ToArray();
    }
    private void ブロック選択時(int ID){
        this.選択ID=ID;this.操作頁=0;var 元=this.選択?.元;
        this.選択操作=元?.Operations.FirstOrDefault(項目=>項目.Start>=0&&項目.Length>0)??元?.BranchValue??元?.Operations.FirstOrDefault();this.描画を更新();
    }
    private async Task 移動Async(int ID){
        this.ブロック選択時(ID);
        if(this.操作 is not null&&this.選択 is{} 項目){await this.操作.InvokeVoidAsync("ノードを表示",this.寿命.Token,"b"+ID,生成配置ブロック.X,項目.Y,生成配置ブロック.幅,生成配置ブロック.高さ);}
    }
    private async Task 検索時Async(){
        if(string.IsNullOrWhiteSpace(this.検索)){return;}
        var 一致=this.配置.ブロック.Where(項目=>("B"+項目.元.Id).Contains(this.検索,StringComparison.OrdinalIgnoreCase)||項目.元.Kind.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||項目.元.Operations.Any(操作=>操作.Kind.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||this.ハッシュ適合か&&操作.Symbol?.Contains(this.検索,StringComparison.OrdinalIgnoreCase)==true)).ToArray();
        if(一致.Length==0){return;}var 番号=Array.FindIndex(一致,項目=>項目.元.Id==this.選択ID);await this.移動Async(一致[(番号+1)%一致.Length].元.Id);
    }
    private Task 検索キー時Async(KeyboardEventArgs 引数){if(引数.Key=="Enter"){return this.検索時Async();}return Task.CompletedTask;}
    [JSInvokable]public Task 表示範囲変更Async(long 世代,long 番号,double 左,double 上,double 幅,double 高さ,double 倍率){
        if(this.破棄済みか||世代!=this.世代||番号<=this.最終通知||!double.IsFinite(左)||!double.IsFinite(上)||!double.IsFinite(幅)||!double.IsFinite(高さ)||!double.IsFinite(上+高さ)||!double.IsFinite(倍率)||幅<=0||高さ<=0||倍率<=0){return Task.CompletedTask;}
        return this.InvokeAsync(()=>{if(this.破棄済みか||世代!=this.世代||番号<=this.最終通知){return;}this.最終通知=番号;this.窓上=上-200;this.窓下=上+高さ+200;this.実倍率=倍率;this.描画を更新();this.StateHasChanged();});
    }
    [JSInvokable]public Task キーボードノード移動Async(long 世代,int 方向){
        if(this.破棄済みか||世代!=this.世代||方向 is not(-1 or 1)||this.配置.ブロック.Length==0){return Task.CompletedTask;}
        return this.InvokeAsync(async()=>{if(this.破棄済みか||世代!=this.世代){return;}var 番号=Array.FindIndex(this.配置.ブロック,項目=>項目.元.Id==this.選択ID);await this.移動Async(this.配置.ブロック[Math.Clamp(番号+方向,0,this.配置.ブロック.Length-1)].元.Id);this.StateHasChanged();});
    }
    private void 標準倍率時(){this.倍率=100;this.全体表示か=false;this.描画を更新();}
    private void 全体表示時(){this.全体表示か=true;this.窓上=null;this.描画を更新();}
    private async Task 倍率変更時Async(double 係数){var 基準=this.倍率;if(this.全体表示か&&this.操作 is not null){基準=await this.操作.InvokeAsync<double>("表示倍率を取得");}if(!double.IsFinite(基準)||基準<=0){基準=100;}this.倍率=Math.Clamp(基準*係数,0.1,400);this.全体表示か=false;this.描画を更新();}
    private void 本文へ移動時(){this.全文表示か=true;this.全文選択待ちか=true;}
    protected override async Task OnAfterRenderAsync(bool firstRender){
        await this.初期化.WaitAsync();
        try{
            if(this.破棄済みか){return;}
            if(firstRender){this.参照=DotNetObjectReference.Create(this);this.モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import",this.寿命.Token,"./_content/Seamless.Management.Components/execution-plan.js");this.操作=await this.モジュール.InvokeAsync<IJSObjectReference>("計画グラフを初期化",this.寿命.Token,this.画面,this.参照,this.世代);this.通知済み世代=this.世代;}
            else if(this.操作 is not null&&this.通知済み世代!=this.世代){this.通知済み世代=this.世代;await this.操作.InvokeVoidAsync("設定を更新",this.寿命.Token,this.世代);}
            if(this.全文選択待ちか&&this.全文 is not null&&this.選択操作 is{} 項目&&this.範囲本文(項目) is not null&&this.モジュール is not null){this.全文選択待ちか=false;await this.モジュール.InvokeVoidAsync("生成範囲を表示",this.寿命.Token,this.全文.入力要素,項目.Start,項目.Length);}
        }catch(JSDisconnectedException){}catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}finally{this.初期化.Release();}
    }
    public async ValueTask DisposeAsync(){if(this.破棄済みか){return;}this.破棄済みか=true;this.世代++;this.寿命.Cancel();await this.初期化.WaitAsync();try{await スクリプト参照.解放Async(this.操作,this.モジュール);}finally{this.参照?.Dispose();this.初期化.Release();this.寿命.Dispose();}}
}
