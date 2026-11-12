using Microsoft.AspNetCore.Components;

namespace Seamless.Management.Components;

public partial class 利用者管理ビュー{
    [Parameter]public bool 表示中か{get;set;}
    [Parameter]public string[] DB候補{get;set;}=[];
    [Parameter]public EventCallback<bool> 未保存変更時{get;set;}
    private static readonly (ManagementPermission 値,string 名前)[] 権限候補=[(ManagementPermission.View,"表示"),(ManagementPermission.Execute,"クエリ実行"),(ManagementPermission.ReadRows,"行参照"),(ManagementPermission.WriteRows,"行更新"),(ManagementPermission.Edit,"定義編集"),(ManagementPermission.Validate,"検証"),(ManagementPermission.Apply,"明示適用"),(ManagementPermission.Manage,"管理")];
    private UserProfile[] 一覧=[];
    private UserProfile? 原本;
    private readonly List<権限行> 権限一覧=[];
    private string 名前="",パスワード="",案内="";
    private bool 無効か,編集中か,新規か,読込中か,保存中か,読込済みか,続きありか,エラーか;
    private long 次位置,読込世代;
    private readonly CancellationTokenSource 寿命=new();
    private bool 処理中か=>this.読込中か||this.保存中か;
    private bool 編集ありか{
        get{
            if(!this.編集中か){return false;}
            if(this.パスワード.Length>0){return true;}
            if(this.原本 is null){return this.名前.Length>0||this.権限一覧.Count>0||this.無効か;}
            return this.無効か!=this.原本.User.Disabled||!this.権限を取得().SequenceEqual(this.原本.User.Grants);
        }
    }
    protected override async Task OnParametersSetAsync(){if(this.表示中か&&!this.読込済みか){await this.一覧取得Async(false);}}
    public async Task 一覧取得Async(bool 続きか){
        if(this.処理中か){return;}
        if(this.編集ありか){this.エラーか=true;this.案内=表示文言.取得("編集を保存するか取り消してから、一覧を切り替えてください。");return;}
        var 世代=++this.読込世代;this.読込中か=true;this.エラーか=false;this.案内="";
        try{
            long 位置=0;if(続きか){位置=this.次位置;}
            var 結果=await this.管理操作.UserProfilesAsync(位置,this.寿命.Token);if(世代!=this.読込世代){return;}
            this.一覧=結果.Items;this.次位置=結果.NextCursor;this.続きありか=結果.HasMore;this.読込済みか=true;
            if(this.原本 is{} 元&&this.一覧.FirstOrDefault(項目=>項目.User.Name==元.User.Name) is{} 最新){this.原本=最新;this.入力を復元();}
        }catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){
        }catch(Exception 例外){if(世代==this.読込世代){this.エラーか=true;this.案内=表示文言.取得("利用者の取得に失敗しました。")+例外.Message;}}
        finally{if(世代==this.読込世代){this.読込中か=false;this.StateHasChanged();}}
    }
    private Task 新規開始時Async(){if(this.処理中か||this.編集を保護するか()){return Task.CompletedTask;}this.原本=null;this.新規か=true;this.編集中か=true;this.入力を復元();return this.編集変更時Async();}
    private Task 編集開始時Async(UserProfile 利用者){if(this.処理中か||this.編集を保護するか()){return Task.CompletedTask;}this.原本=利用者;this.新規か=false;this.編集中か=true;this.入力を復元();return this.編集変更時Async();}
    private bool 編集を保護するか(){if(this.編集ありか){this.エラーか=true;this.案内=表示文言.取得("編集を保存するか取り消してから、別の利用者を開いてください。");return true;}return false;}
    private void 入力を復元(){
        this.名前=this.原本?.User.Name??"";this.パスワード="";this.無効か=this.原本?.User.Disabled??false;this.権限一覧.Clear();
        foreach(var 許可 in this.原本?.User.Grants??[]){this.権限一覧.Add(new(){DB=許可.Database,許可=許可.Permissions});}
    }
    private Task 編集変更時Async()=>this.未保存変更時.InvokeAsync(this.編集ありか);
    private Task 権限追加時Async(){this.権限一覧.Add(new());return this.編集変更時Async();}
    private Task 権限削除時Async(権限行 行){this.権限一覧.Remove(行);return this.編集変更時Async();}
    private Task 権限変更時Async(権限行 行,ManagementPermission 許可,bool 有効か){if(有効か){行.許可|=許可;}else{行.許可&=~許可;}return this.編集変更時Async();}
    private Task 編集取消時Async(){this.入力を復元();if(this.原本 is null){this.編集中か=false;}this.案内="";this.エラーか=false;return this.編集変更時Async();}
    private DatabaseGrant[] 権限を取得()=>this.権限一覧.Select(static 行=>new DatabaseGrant(行.DB,行.許可)).ToArray();
    public async Task 保存Async(){
        if(this.処理中か||!this.編集ありか){return;}
        var 世代=this.読込世代;this.保存中か=true;this.エラーか=false;this.案内="";
        try{
            string? 秘密=null;if(this.パスワード.Length>0){秘密=this.パスワード;}
            var 要求=new UserProfileEdit(new(this.名前,秘密,this.権限を取得(),this.無効か),this.原本?.Stamp,this.新規か);
            var 結果=await this.管理操作.EditUserProfileAsync(要求,this.寿命.Token);if(世代!=this.読込世代){return;}
            var 位置=Array.FindIndex(this.一覧,項目=>string.Equals(項目.User.Name,結果.User.Name,StringComparison.OrdinalIgnoreCase));
            if(位置>=0){this.一覧[位置]=結果;}else if(this.一覧.Length<100){this.一覧=[..this.一覧,結果];}
            this.原本=結果;this.新規か=false;this.入力を復元();await this.編集変更時Async();this.案内=表示文言.取得("利用者を保存しました。");
        }catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){
        }catch(Exception 例外){if(世代==this.読込世代){this.エラーか=true;this.案内=表示文言.取得("保存に失敗しました。入力は保持しています。")+例外.Message;}}
        finally{if(世代==this.読込世代){this.保存中か=false;this.StateHasChanged();}}
    }
    private sealed class 権限行{public string DB="";public ManagementPermission 許可;}
    public void Dispose(){this.読込世代++;this.寿命.Cancel();this.寿命.Dispose();}
}
