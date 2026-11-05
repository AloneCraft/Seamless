using System.IO;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private async Task オブジェクトタブを検証Async(テーブル文書 document,string image){
        var original=this.現在定義();await this.テーブルのタブを開くAsync(original,new("Notes","Sales",[new("Id","int",true)],[]),original.Database,original.Id,false);
        var other=this.表文書.Values.Single(item=>item.定義ID==original.Id&&item.設計.Path=="Sales.Notes");await other.パネル!.保管Async(false);await this.表タブを選択Async(document);
        var design=DatabaseDesigner.Read(this.現在定義(),"Sales.Customers");document.パネル!.入力を更新(design with{Columns=[..design.Columns,new("Phone","int",false,null,"0")]});await document.パネル.保管Async(false);
        if(!DatabaseDesigner.Tables(this.現在定義()).Contains("Sales.Notes")||!DatabaseDesigner.Read(this.現在定義(),"Sales.Customers").Columns.Any(column=>column.Name=="Phone")){throw new InvalidOperationException("別タブのテーブル変更を保持して保管できません。");}
        await this.表の削除を下書きにするAsync(document);if(document.削除 is null||!this.文書表示名(document.タブ).Contains("削除予定")||DatabaseDesigner.Tables(this.現在定義()).Contains("Sales.Customers")){throw new InvalidOperationException("削除予定がタブと下書きへ反映されません。");}起動.画像保存(this,image+".delete-pending.png");
        var live=await this.必須接続().CallAsync<DefinitionVersion>("definition",original.Database,new{id=original.Id});if(!DatabaseDesigner.Tables(live.Definition).Contains("Sales.Customers")){throw new InvalidOperationException("削除予定の登録だけで稼働版を変更しました。");}
        await this.表タブを閉じるAsync(document);await this.テーブルのタブを開くAsync(live.Definition,DatabaseDesigner.Read(live.Definition,"Sales.Customers"),original.Database,original.Id,true,live.Version);document=this.表文書.Values.Single(item=>item.定義ID==original.Id&&item.設計.Path=="Sales.Customers");await this.表の削除を取り消すAsync(document);
        if(document.削除 is not null||!DatabaseDesigner.Tables(this.現在定義()).Contains("Sales.Notes")||!DatabaseDesigner.Read(this.現在定義(),"Sales.Customers").Columns.Any(column=>column.Name=="Phone")){throw new InvalidOperationException("削除の取消が別タブの変更も戻しました。");}
        await this.表のDBの作業をまとめるAsync(document);await this.適用を実行Async((target,preview,_)=>target.Id==original.Id&&preview.CanApply&&!preview.RequiresBackup);
        live=await this.必須接続().CallAsync<DefinitionVersion>("definition",original.Database,new{id=original.Id});if(live.Version!=this.基準版||live.Version!=3||!DatabaseDesigner.Tables(live.Definition).Contains("Sales.Notes")||!DatabaseDesigner.Read(live.Definition,"Sales.Customers").Columns.Any(column=>column.Name=="Phone")){throw new InvalidOperationException("すべて保存で二つの表を一つの公開版へ反映できません。");}
        await this.表タブを選択Async(other);await this.表の削除を下書きにするAsync(other);await this.表のDBの作業をまとめるAsync(other);await this.適用を実行Async((_,preview,_)=>{if(!preview.RequiresBackup){throw new InvalidOperationException("公開済みテーブル削除に事前退避が要求されません。");}return false;});
        var unchanged=await this.必須接続().CallAsync<DefinitionVersion>("definition",original.Database,new{id=original.Id});if(unchanged.Version!=live.Version){throw new InvalidOperationException("削除確認を取り消したのに公開版が変わりました。");}
        await this.適用を実行Async((target,preview,_)=>target.Id==original.Id&&preview.RequiresBackup);var operation=await this.必須接続().CallAsync<OperationInfo>("operation",original.Database,this.適用操作ID!.Value);if(operation.State!=OperationState.Succeeded||operation.PublishedVersion!=live.Version+1||operation.BackupPath is null||!Directory.Exists(operation.BackupPath)||other.削除 is not{確定か:true}){throw new InvalidOperationException("確認済み削除・バックアップ・確定済み表示が一致しません: "+this.診断.Text);}await this.表タブを選択Async(other);起動.画像保存(this,image+".delete-applied.png");await this.表タブを選択Async(document);
        var beforeClose=this.現在定義().Hash();await this.表タブを閉じるAsync(document);if(this.現在定義().Hash()!=beforeClose||this.ソース文書タブ.Items.Contains(document.タブ)){throw new InvalidOperationException("タブを閉じる操作が下書きを変更しました。");}
        await this.テーブルのタブを開くAsync(this.現在定義(),DatabaseDesigner.Read(this.現在定義(),"Sales.Customers"),original.Database,original.Id,false);document=this.表文書.Values.Single(item=>item.定義ID==original.Id&&item.設計.Path=="Sales.Customers");var panel=document.パネル!;panel.自動保管か=false;
        design=panel.入力内容();panel.入力を更新(design with{Columns=[..design.Columns,new("DisplayName","string",false)]});await panel.保管Async(false);if(this.現在定義().Hash()!=beforeClose||panel.結果 is not null){throw new InvalidOperationException("無効な設計で作業ソースが変更されています。");}
        await this.表タブを閉じるAsync(document);this.連携設定を読む();this.表作業を読む();await this.テーブルのタブを開くAsync(this.現在定義(),DatabaseDesigner.Read(this.現在定義(),"Sales.Customers"),original.Database,original.Id,false);document=this.表文書.Values.Single(item=>item.定義ID==original.Id&&item.設計.Path=="Sales.Customers");panel=document.パネル!;panel.自動保管か=false;
        if(panel.入力内容().Columns.Count(column=>column.Name=="DisplayName")!=2){throw new InvalidOperationException("閉じたタブの入力途中の設計を復元できません。");}
        panel.入力を更新(design with{Columns=design.Columns.Select(column=>{if(column.Name=="Phone"){return column with{Type="long"};}return column;}).ToArray()});
        var workspace=this.連携プロジェクト[original.Id];var file=this.現在定義().Source.Files.Single(item=>item.Text.Contains("partial int Phone"));File.WriteAllText(Path.Combine(workspace.DirectoryPath,file.Path),file.Text.Replace("Phone","Contact"));await this.ソースを同期Async();var external=this.現在定義().Hash();await panel.保管Async(false);
        if(this.現在定義().Hash()!=external||!this.現在定義().Source.Files.Any(item=>item.Text.Contains("Contact"))||!panel.入力内容().Columns.Any(column=>column.Name=="Phone"&&column.Type=="long")){throw new InvalidOperationException("同じテーブルへの外部変更とGUI入力を安全に保持できません。");}
        起動.画像保存(this,image+".object-conflict.png");await this.表タブを閉じるAsync(document);if(!File.Exists(this.表作業ファイル)){throw new InvalidOperationException("テーブルの作業記録がありません。");}
        await this.DBを表示Async(this.表示DB.Single(item=>item.DefinitionId==original.Id));await this.テーブル設計Async(true);var unformed=this.表文書.Values.Single(item=>item.定義ID==original.Id&&item.設計.OriginalPath is null);unformed.パネル!.自動保管か=false;unformed.パネル.入力を更新(unformed.パネル.入力内容() with{Name="入力-途中"});await this.表タブを閉じるAsync(unformed);this.連携設定を読む();this.表作業を読む();
        var stored=this.表作業一覧[unformed.ID];if(stored.開いているか||!stored.未保管か||this.表再開ボタン.Visibility!=System.Windows.Visibility.Visible){throw new InvalidOperationException("未作成の表の入力を再開一覧へ保持できません。");}await this.閉じた表を再開Async(stored);if(this.表文書[unformed.ID].パネル?.入力内容().Name!="入力-途中"||DatabaseDesigner.Tables(this.現在定義()).Contains("NewTable")){throw new InvalidOperationException("入力途中の新規表をC#へ混ぜずに再開できません。");}await this.表タブを閉じるAsync(this.表文書[unformed.ID]);
    }
    private async Task 利用者切替を検証Async(画面検証設定 settings){
        if(this.ソース文書タブ.Items.Count==0){throw new InvalidOperationException("切替前のオブジェクトタブがありません。");}
        using var certificate=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(settings.証明書);var viewer=new ManagementClient(settings.ホスト,settings.ポート,certificate);var info=await viewer.InfoAsync();var login=await viewer.LoginAsync("auditreader","ReaderPassword-123");await this.接続を確定Async(viewer,info,login.User,false);
        if(this.ソース文書タブ.Items.Count!=0||this.表領域.Content is not null||this.表作業一覧.Count!=0||this.編集定義 is not null||this.コード.Text.Length!=0){throw new InvalidOperationException("利用者の切替後に以前の利用者の作業内容が残っています。");}
    }
}
