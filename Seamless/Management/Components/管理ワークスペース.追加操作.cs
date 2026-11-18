namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private async Task 表追加編集Async(管理対象 対象,bool 列追加か){
        if(列追加か){await this.ツリー列追加開始Async(対象);return;}
        var 既存=this.対象文書一覧.Values.FirstOrDefault(文書=>文書.DBID==対象.ID&&文書.対象?.Kind=="Table"&&文書.対象.Id==対象.表);
        if(既存 is not null&&this.対象画面.TryGetValue(既存.ID,out var 画面)&&!await 画面.同期Async()){this.作業文書を選ぶ(既存.ID);return;}
        if(this.表入力中か&&this.編集前の表?.Path!=対象.表||this.スキーマ入力中か||this.問い合わせ入力 is not null){throw new ManagementException("Busy",表示文言.取得("入力中の定義を下書きへ反映してから、別の追加操作を開始してください。"));}
        if(!this.表入力中か){this.表編集開始時(this.対象文書(対象)!,対象.表!);}
    }
    private ロール管理ビュー? ロール画面;
    private 利用者管理ビュー? 利用者画面;
    private 監査ビュー? 監査画面;
    private bool システム展開か;
    private bool 監査条件追加待ちか;
    private コマンド登録<string>? システム追加登録;
    private コマンド登録<string> システム追加=>this.システム追加登録??=new([
        new("role","新しいロール",対象=>対象 is "system" or "roles"&&this.ロールを利用できるか,_=>!this.操作中か&&this.ロール画面?.新規開始できるか is not false,_=>this.システム追加開始Async("roles"),["role","権限"],アイコン:"ロール"),
        new("user","利用者を作成",対象=>対象 is "system" or "users"&&this.利用者管理を利用できるか,_=>!this.操作中か&&this.利用者画面?.新規開始できるか is not false,_=>this.システム追加開始Async("users"),["user","account"],アイコン:"利用者"),
        new("settings","追加できる設定を表示",対象=>対象 is "system" or "settings"&&this.設定を利用できるか,_=>!this.操作中か,_=>{this.設定を開く();return Task.CompletedTask;},["settings","接続","期限"],アイコン:"設定"),
        new("filter","検索条件を追加",対象=>対象 is "system" or "audit"&&this.監査を利用できるか,_=>!this.操作中か,_=>this.システム追加開始Async("audit"),["audit","検索","filter"],アイコン:"検索")
    ]);
    private async Task システム追加開始Async(string 種類){
        this.システム展開か=true;
        if(種類=="roles"&&this.ロール画面 is{} ロール){await ロール.追加開始Async();this.ロールを開く();return;}
        if(種類=="users"&&this.利用者画面 is{} 利用者){await 利用者.追加開始Async();this.利用者管理を開く();return;}
        if(種類=="audit"){this.監査を開く();this.監査条件追加待ちか=true;}
    }
    private async Task システム追加を反映Async(){
        if(this.監査条件追加待ちか&&this.監査画面 is{} 監査){this.監査条件追加待ちか=false;await 監査.検索条件追加Async();}
    }
}
