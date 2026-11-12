using Microsoft.JSInterop;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private Guid 表示状態インスタンスID;
    private IJSObjectReference? 表示状態モジュール,表示状態操作;
    private bool 表示状態情報読込済みか,表示状態初期化済みか,表示状態復元中か;
    private string? 表示状態前値,表示状態診断,表示状態キー;
    private readonly HashSet<string> 表示状態復元待ちDB=new(StringComparer.Ordinal);
    private async Task ブラウザー表示状態を反映Async(){
        if(this.破棄済みか||!this.表示状態情報読込済みか||this.表示状態復元中か){return;}
        if(!this.認証有効か){if(this.表示状態操作 is not null){await this.表示状態操作.InvokeVoidAsync("dispose");}return;}
        if(!this.表示状態初期化済みか){
            if(this.表示状態インスタンスID==Guid.Empty||this.利用者 is null||this.利用者.Disabled){this.表示状態初期化済みか=true;this.表示状態診断="認証済み利用者とインスタンスIDを確認できないため、表示状態を保存・復元できません。";await this.InvokeAsync(this.StateHasChanged);return;}
            this.表示状態初期化済みか=true;this.表示状態復元中か=true;
            try{
                var キー=ブラウザー表示状態.保存キー(this.表示状態インスタンスID,this.利用者);
                this.表示状態キー=キー;
                this.表示状態モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import","./_content/Seamless.Management.Components/browser-state.js");
                this.表示状態操作=await this.表示状態モジュール.InvokeAsync<IJSObjectReference>("initialize",this.根,キー,this.参照);
                var 状態=await this.表示状態操作.InvokeAsync<ブラウザー表示状態?>("read");
                if(状態 is not null){状態.検証();this.システム展開か=状態.SystemExpanded;this.表示状態復元待ちDB.UnionWith(状態.OpenDatabaseIds);if(状態.DatabaseOrder is{} 順序){this.DBの表示順.復元(順序);}this.文書復元を準備(状態);}
            }catch(Exception 例外)when(例外 is JSException or InvalidOperationException){this.表示状態診断="このブラウザーの表示状態を保存・復元できません。";}
            finally{this.表示状態復元中か=false;}
            if(!this.破棄済みか){await this.InvokeAsync(this.StateHasChanged);}
        }
        if(this.表示状態操作 is null||this.表示状態診断=="このブラウザーの表示状態を保存・復元できません。"){return;}
        this.表示状態復元中か=true;
        try{
            if(this.表示状態キー!=ブラウザー表示状態.保存キー(this.表示状態インスタンスID,this.利用者)){await this.表示状態操作.InvokeVoidAsync("dispose");throw new InvalidOperationException("認証主体が変更されています。");}
            var 復元したか=false;
            foreach(var DB in this.DB一覧.Where(DB=>!DB.IsSystem&&this.表示状態復元待ちDB.Contains(DB.Id)).ToArray()){
                this.表示状態復元待ちDB.Remove(DB.Id);
                try{await this.DBツリーを開閉Async(DB.Id,true);}
                catch(ManagementException){this.表示状態診断="一部のデータベースの表示状態を復元できませんでした。";}
                復元したか=true;
            }
            復元したか|=await this.保存文書を復元Async();
            var 一覧=this.DBツリー一覧.Where(項目=>項目.Value.展開中か).Select(項目=>項目.Key).Concat(this.表示状態復元待ちDB).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var 状態=new ブラウザー表示状態(1,一覧,this.システム展開か,0);状態.検証();
            var 順序=this.DBの表示順.保存値;
            var 文書状態=this.保存文書を取得();object? 文書参照=null;if(文書状態 is{} 保存){文書参照=new{documents=保存.文書,selectedDocument=保存.選択};}
            var 比較値=System.Text.Json.JsonSerializer.Serialize(new{状態,順序,文書参照});
            if(this.表示状態前値!=比較値){await this.表示状態操作.InvokeVoidAsync("save",状態,順序,文書参照);this.表示状態前値=比較値;}
            if(復元したか){await this.InvokeAsync(this.StateHasChanged);}
            else{await this.表示状態操作.InvokeVoidAsync("restoreScroll");}
        }catch(JSDisconnectedException){}
        catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        catch(Exception 例外)when(例外 is JSException or InvalidOperationException){this.表示状態診断="このブラウザーの表示状態を保存・復元できません。";await this.InvokeAsync(this.StateHasChanged);}
        finally{this.表示状態復元中か=false;}
    }
    [JSInvokable]public Task 表示状態保存失敗Async(){this.表示状態診断="このブラウザーの表示状態を保存・復元できません。";return this.InvokeAsync(this.StateHasChanged);}
}
