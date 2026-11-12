using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Seamless.Management.Components;

/// <summary>部分宣言の入力を元ファイルへ差し戻して解析するための、版固定の範囲。</summary>
public sealed record コード補完範囲(SourceSnapshot ソース,string パス,int 開始,int 長さ);
public sealed record コード補完適用結果(int Position,string Text);

public partial class コードエディター{
    [Parameter]public コード補完範囲? 補完範囲{get;set;}
    private readonly CSharpCodeCompletion 補完器=new();
    private CodeCompletionResult? 補完結果;
    private コード補完範囲? 直前補完範囲;
    private string? 直前補完入力,補完案内;
    private CancellationTokenSource? 補完取消;
    private IJSObjectReference? 補完モジュール,補完操作;
    private DotNetObjectReference<コードエディター>? 補完参照;
    private long 補完世代;
    private bool 補完中か;

    private void 補完の入力を照合(){
        if(this.直前補完入力!=this.値||this.直前補完範囲!=this.補完範囲||this.読み取り専用か){this.補完を取消();}
        this.直前補完入力=this.値;this.直前補完範囲=this.補完範囲;
    }
    private void 補完を取消(){
        this.補完世代++;this.補完取消?.Cancel();this.補完結果=null;this.補完中か=false;this.補完案内=null;
    }
    private SourceSnapshot 補完入力を作る(string 入力値){
        var 範囲=this.補完範囲??throw new ManagementException("InvalidRequest","補完するソース範囲がありません。");
        return new SourceEditor().Apply(範囲.ソース,new(範囲.ソース.Hash(),範囲.パス,範囲.開始,範囲.長さ,入力値));
    }
    [JSInvokable]public async Task 補完要求Async(int 位置,string 入力値){
        if(this.破棄済みか||this.読み取り専用か||this.補完範囲 is null||位置<0||位置>入力値.Length){return;}
        this.補完を取消();
        this.直前補完入力=入力値;
        var 世代=this.補完世代;var 範囲=this.補完範囲;
        using var 取消=new CancellationTokenSource();this.補完取消=取消;
        this.補完中か=true;await this.InvokeAsync(this.StateHasChanged);
        try{
            var ソース=this.補完入力を作る(入力値);
            var 結果=await Task.Run(()=>this.補完器.Complete(ソース,範囲.パス,範囲.開始+位置,取消.Token),取消.Token);
            if(世代!=this.補完世代||this.破棄済みか){return;}
            if(結果.Start<範囲.開始||結果.Start+結果.Length>範囲.開始+入力値.Length){return;}
            this.補完結果=結果;
            if(結果.Items.Length==0){this.補完案内=表示文言.取得("この位置で解決できる補完候補がありません。");}
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){}
        catch(ManagementException 例外){if(世代==this.補完世代){this.補完案内=例外.Message;}}
        finally{
            if(ReferenceEquals(this.補完取消,取消)){this.補完取消=null;}
            if(世代==this.補完世代&&!this.破棄済みか){this.補完中か=false;await this.InvokeAsync(this.StateHasChanged);}
        }
    }
    [JSInvokable]public async Task<コード補完適用結果?> 補完適用Async(int 番号,string 入力値){
        if(this.破棄済みか||this.読み取り専用か||this.補完結果 is not{} 結果||this.補完範囲 is not{} 範囲){return null;}
        try{
            var ソース=this.補完器.Apply(this.補完入力を作る(入力値),結果,番号);
            var ファイル=ソース.Files.Single(ファイル=>ファイル.Path==範囲.パス);
            var 差=結果.Items[番号].InsertText.Length-結果.Length;
            var 次の値=ファイル.Text.Substring(範囲.開始,入力値.Length+差);
            var 次の位置=結果.Start-範囲.開始+結果.Items[番号].InsertText.Length;
            // 値変更はブラウザーが最新の入力・キャレットを照合した後、通常のoninputで通知する。
            this.補完を取消();await this.InvokeAsync(this.StateHasChanged);
            return new(次の位置,次の値);
        }catch(ManagementException){
            this.補完を取消();this.補完案内=表示文言.取得("入力が変わったため補完を取り消しました。再取得してください。");await this.InvokeAsync(this.StateHasChanged);return null;
        }
    }
    [JSInvokable]public Task 補完取消Async(){this.補完を取消();return this.InvokeAsync(this.StateHasChanged);}
    private async Task 補完を初期化Async(){
        if(this.補完範囲 is null||this.読み取り専用か){return;}
        if(this.補完操作 is null){
            this.補完モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import","./_content/Seamless.Management.Components/code-completion.js");
            this.補完参照=DotNetObjectReference.Create(this);
            this.補完操作=await this.補完モジュール.InvokeAsync<IJSObjectReference>("初期化",this.根,this.入力,this.補完参照,表示文言.取得("補完操作に失敗しました。入力は保持しています。"));
        }
        await this.補完操作.InvokeVoidAsync("update");
    }
    private async Task 補完を破棄Async(){
        await スクリプト参照.解放Async(this.補完操作,this.補完モジュール);
        this.補完参照?.Dispose();
    }
}
