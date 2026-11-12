namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private 作成入力状態? 新規作成入力;
    private sealed record 作成入力状態(Guid ID,string DBID,string 定義ID,string スキーマ,string 文書ID,string 種類);
    private bool 作成入力表示中か=>this.新規作成入力 is null||this.選択DBID==this.新規作成入力.DBID&&this.現在文書?.定義.Id==this.新規作成入力.定義ID&&this.選択作業文書ID==this.新規作成入力.文書ID;
    private void 別の作成入力を確認(){
        if(this.新規作成入力 is not null){throw new ManagementException("Busy",表示文言.取得("入力中の定義を下書きへ反映してから、別の追加操作を開始してください。"));}
    }
    private bool 作成場所を開く(string DBID,string スキーマ,string 種類){
        if(this.新規作成入力 is{} 入力){
            if(入力.DBID!=DBID||入力.スキーマ!=スキーマ||入力.種類!=種類){this.別の作成入力を確認();}
            this.文書を選ぶ(DBID);this.スキーマを選ぶ(スキーマ);return false;
        }
        this.文書を選ぶ(DBID);this.スキーマを選ぶ(スキーマ);
        var 文書=this.文書一覧[DBID];this.新規作成入力=new(Guid.NewGuid(),DBID,文書.定義.Id,スキーマ,this.選択作業文書ID!,種類);return true;
    }
    private void 作成入力を取消す(){this.新規作成入力=null;this.作成入力を閉じる();}
    private void 問い合わせ入力変更時(Guid? ID,QueryDesign 入力){
        if(this.新規作成入力?.ID!=ID||this.問い合わせ入力 is null){return;}
        this.問い合わせ入力=入力;
    }
    private bool 作成先が一致するか(文書状態 文書)=>this.新規作成入力 is null||this.新規作成入力.DBID==文書.DB.Id&&this.新規作成入力.定義ID==文書.定義.Id&&this.選択作業文書ID==this.新規作成入力.文書ID;
}
