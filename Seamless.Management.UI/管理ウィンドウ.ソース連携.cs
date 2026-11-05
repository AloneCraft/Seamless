using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private readonly DispatcherTimer ソース同期タイマー=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly TabControl ソース文書タブ=new(){Height=34,Visibility=Visibility.Collapsed};
    private readonly Dictionary<string,SourceWorkspace> 連携プロジェクト=[];
    private readonly Dictionary<string,TabItem> ソース文書=[];
    private readonly Dictionary<string,string> 文書表示ハッシュ=[];
    private Guid 接続インスタンスID;
    private bool ソース同期中か,文書選択中か;
    private sealed record ソース文書位置(string 定義ID,string? 表,string ファイル,int 開始,int 長さ);
    private string? 連携設定パス{get{if(this.接続インスタンスID==Guid.Empty||this.ログイン利用者 is null){return null;}return Path.Combine(this.設定ディレクトリ,"sources-"+this.接続インスタンスID.ToString("N")+"-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(this.ログイン利用者.Name)))[..16]+".json");}}
    private static string? ソースリポジトリ(){
        var 指定=Environment.GetEnvironmentVariable("SEAMLESS_SOURCE_REPOSITORY");if(指定 is not null&&File.Exists(Path.Combine(指定,"Seamless.slnx"))){return 指定;}
        var 場所=new DirectoryInfo(AppContext.BaseDirectory);while(場所 is not null){if(File.Exists(Path.Combine(場所.FullName,"Seamless.slnx"))){return 場所.FullName;}場所=場所.Parent;}return null;
    }
    private async Task ソースプロジェクトAsync(){
        await this.DB下書きを開くAsync();if(this.連携プロジェクト.TryGetValue(this.現在定義().Id,out var 既存)){this.状態.Text="C#プロジェクト: "+Path.Combine(既存.DirectoryPath,"Schema.csproj");return;}var 選択=new OpenFolderDialog{Title="C#プロジェクトの保存先（空のフォルダー）"};if(選択.ShowDialog(this)!=true){return;}await this.ソースプロジェクトを作成Async(選択.FolderName);
    }
    private async Task ソースプロジェクトを作成Async(string 場所){
        var 定義=this.現在定義();var リポジトリ=ソースリポジトリ()??throw new ManagementException("Unsupported","この配布には開発用参照がありません。SEAMLESS_SOURCE_REPOSITORYでSeamlessリポジトリを指定してください。");
        var 稼働=await this.必須接続().CallAsync<DefinitionVersion>("definition",定義.Database,new{id=定義.Id});
        var 連携=await Task.Run(()=>SourceWorkspace.Create(場所,new(定義,this.基準版,this.下書き版),稼働.Definition.Source,リポジトリ));this.連携プロジェクト.Add(定義.Id,連携);this.連携設定を保存();
        await this.ソースを同期Async();this.ソースタブを更新(連携);this.状態.Text="C#プロジェクト: "+Path.Combine(場所,"Schema.csproj")+" · VS Codeでフォルダーを開き、Ctrl+Shift+Bでビルドできます。";
    }
    private async Task 作成DBのソースを用意Async(){
        if(ソースリポジトリ() is null){this.ソース案内.Text="DB作成済み · 外部編集には「C#プロジェクト…」から開発用参照を設定してください。";return;}
        var ルート=Environment.GetEnvironmentVariable("SEAMLESS_SOURCE_WORKSPACE_ROOT")??Path.Combine(this.設定ディレクトリ,"Sources");var パス=this.連携設定パス??throw new InvalidOperationException("接続してください。");
        await this.ソースプロジェクトを作成Async(Path.Combine(ルート,Path.GetFileNameWithoutExtension(パス),this.現在定義().Id));
    }
    private void 連携設定を保存(){var パス=this.連携設定パス??throw new InvalidOperationException("接続してください。");Directory.CreateDirectory(this.設定ディレクトリ);File.WriteAllText(パス,JsonSerializer.Serialize(this.連携プロジェクト.Values.Select(連携=>連携.DirectoryPath).ToArray(),ManagementJson.Options));}
    private void 連携設定を読む(){
        this.連携プロジェクト.Clear();this.ソース文書.Clear();this.文書表示ハッシュ.Clear();this.ソース文書タブ.Items.Clear();this.ソース文書タブ.Visibility=Visibility.Collapsed;var パス=this.連携設定パス;if(パス is null||!File.Exists(パス)){return;}
        foreach(var 場所 in JsonSerializer.Deserialize<string[]>(File.ReadAllText(パス),ManagementJson.Options)??[]){try{var 連携=SourceWorkspace.Open(場所);this.連携プロジェクト.Add(連携.Current.Definition.Id,連携);this.ソースタブを更新(連携);}catch(Exception 例外){this.診断.Text="C#プロジェクトの復旧を確認してください: "+場所+"\n"+例外.Message;}}
    }
    private async Task ソースを同期Async(){
        if(this.ソース同期中か||this.クライアント is null||this.操作中数>0){return;}this.ソース同期中か=true;
        try{foreach(var 連携 in this.連携プロジェクト.Values){
            var 現在=連携.Current.Definition;var 表示中か=this.編集定義?.Id==現在.Id&&this.下書き版==連携.Current.DraftRevision;if(表示中か){現在=this.現在定義();}
            var 前ハッシュ=現在.Hash();var 結果=await Task.Run(()=>連携.Synchronize(現在));
            if(表示中か&&this.現在定義().Hash()!=前ハッシュ){continue;}
            if(表示中か&&前ハッシュ!=結果.Definition.Hash()){this.ソースを背景更新(結果.Definition);this.未保存か=true;this.解析タイマー.Stop();this.解析タイマー.Start();}
            if(連携.NeedsDraftSave&&this.対象の権限があるか(現在.Database,ManagementPermission.Edit)){
                var 保存=await this.必須接続().CallAsync<Draft>("saveDraft",現在.Database,new SaveDraftRequest(結果.Definition,連携.Current.DraftRevision,連携.Current.BaseVersion));連携.MarkDraftSaved(保存.Revision,保存.BaseVersion);
                if(表示中か){this.下書き版=保存.Revision;this.保存済みハッシュ=保存.Definition.Hash();this.未保存か=this.現在定義().Hash()!=保存.Definition.Hash();this.ソース保存状態を表示(連携);}
                this.下書き項目を更新(new(new(保存.Definition.Id,保存.Definition.Database,保存.Definition.Kind,保存.Definition.EntryType,保存.Definition.EntryMember,保存.Definition.WritesData),保存.Revision,保存.BaseVersion,保存.SavedAt));
            }this.ソースタブを更新(連携);
        }}catch(Exception 例外){this.状態.Text="ソース同期は保留中 · 既存の内容を保持しています。";this.診断.Text=例外.Message;}finally{this.ソース同期中か=false;}
    }
    private void ソース保存状態を表示(SourceWorkspace 連携){var 適用表示="適用済み";if(連携.HasUnappliedChanges){適用表示="稼働DBへ未適用";}this.ソース案内.Text="個人下書き r"+連携.Current.DraftRevision+" · 基準版 v"+連携.Current.BaseVersion+" · ファイル保存済み · "+適用表示;this.ソース案内.ToolTip=Path.Combine(連携.DirectoryPath,"Schema.csproj");}
    private void ソースを背景更新(Definition 定義){
        this.編集定義=定義;var カーソル=this.コード.CaretOffset;this.反映中か=true;try{this.ファイル一覧.ItemsSource=定義.Source.Files.Select(ファイル=>ファイル.Path).ToArray();var ファイル=定義.Source.Files.FirstOrDefault(ファイル=>ファイル.Path==this.選択ファイル)??定義.Source.Files[0];this.ファイル一覧.SelectedItem=ファイル.Path;this.選択ファイル=ファイル.Path;this.コード.Text=ファイル.Text;this.コード.CaretOffset=Math.Min(カーソル,this.コード.Text.Length);}finally{this.反映中か=false;}this.図の状態.Text="更新前の図 · 外部ソースを解析中";this.未適用表示を更新();
    }
    private void ソースタブを更新(SourceWorkspace 連携){
        var ハッシュ=連携.Current.Definition.Hash()+連携.State.Published.Hash();if(this.文書表示ハッシュ.TryGetValue(連携.Current.Definition.Id,out var 表示済)&&表示済==ハッシュ){return;}this.文書表示ハッシュ[連携.Current.Definition.Id]=ハッシュ;
        var 定義=連携.Current.Definition;var 対象=new List<(string 名前,string? 表,string ファイル,int 開始,int 長さ,bool 未適用か)>();
        try{foreach(var 表 in DatabaseDesigner.Tables(定義)){var 宣言=DatabaseDesigner.TableSource(定義,表);var 未適用か=true;try{var 稼働=DatabaseDesigner.TableSource(定義 with{Source=連携.State.Published},表);未適用か=宣言.Text!=稼働.Text;}catch(ManagementException){}対象.Add((表,表,宣言.Path,宣言.Start,宣言.Length,未適用か));}}catch(Exception 例外)when(例外 is ManagementException or InvalidOperationException){/* 入力途中はファイルタブを残す。 */}
        foreach(var ファイル in 定義.Source.Files){if(対象.Any(項目=>項目.ファイル==ファイル.Path)){continue;}var 稼働=連携.State.Published.Files.SingleOrDefault(項目=>項目.Path==ファイル.Path);対象.Add((ファイル.Path,null,ファイル.Path,0,0,稼働?.Text!=ファイル.Text));}
        this.文書選択中か=true;try{foreach(var 項目 in 対象){var キー=定義.Id+"/"+項目.名前;if(!this.ソース文書.TryGetValue(キー,out var タブ)){タブ=new TabItem();this.ソース文書.Add(キー,タブ);this.ソース文書タブ.Items.Add(タブ);}var 接頭辞="";if(項目.未適用か){接頭辞="*";}タブ.Header=接頭辞+項目.名前;タブ.ToolTip=定義.Database+" · "+項目.ファイル+"\n* = 稼働DBへ未適用（ディスク保存済み・下書き自動保存）";タブ.Tag=new ソース文書位置(定義.Id,項目.表,項目.ファイル,項目.開始,項目.長さ);}this.ソース文書タブ.Visibility=Visibility.Visible;}finally{this.文書選択中か=false;}
    }
    private bool ソースの作業を保持(){
        if(this.編集定義 is null||!this.連携プロジェクト.TryGetValue(this.編集定義.Id,out var 連携)){return false;}
        if(this.下書き版!=連携.Current.DraftRevision){throw new ManagementException("Conflict","表示版と連携中の下書きが異なります。編集内容を確認してください。");}
        var 結果=連携.Synchronize(this.現在定義());this.ソースタブを更新(連携);this.状態.Text="作業をファイルと復旧記録に保持しました。稼働DBには未適用です。";return true;
    }
    private void ソース文書を選択(){
        if(this.文書選択中か||this.ソース文書タブ.SelectedItem is not TabItem{Tag:ソース文書位置 位置}||!this.連携プロジェクト.TryGetValue(位置.定義ID,out var 連携)){return;}
        if(this.編集定義?.Id!=位置.定義ID||this.下書き版!=連携.Current.DraftRevision){if(!this.破棄を確認("下書きのソースを開く")){return;}this.定義を表示(連携.Current.Definition,連携.Current.DraftRevision,連携.Current.BaseVersion);}
        this.ファイル一覧.SelectedItem=位置.ファイル;this.編集タブ.SelectedIndex=0;if(位置.開始+位置.長さ<=this.コード.Text.Length){this.コード.Select(位置.開始,位置.長さ);this.コード.ScrollToLine(this.コード.Document.GetLineByOffset(位置.開始).LineNumber);}
    }
}
