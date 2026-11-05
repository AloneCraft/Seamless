using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private const int テーブルタブ番号=6;
    private readonly ContentControl 表領域=new();
    private readonly Dictionary<string,テーブル文書> 表文書=[];
    private readonly Dictionary<string,表作業> 表作業一覧=[];
    private readonly Dictionary<string,Definition> DB作業定義=[];
    private readonly Dictionary<string,(long 下書き,long 基準)> DB作業版=[];
    private string? 表作業保存ハッシュ;
    private readonly string 表作業退避ID=Guid.NewGuid().ToString("N");
    private bool 表作業未記録か;
    private readonly Button 表再開ボタン=new(){Content="閉じた下書きを再開…",Margin=new Thickness(5,0,0,0),Padding=new Thickness(8,2,8,2),Visibility=Visibility.Collapsed,VerticalAlignment=VerticalAlignment.Center};
    private sealed record 表作業(string ID,string 定義ID,string DB名,TableDesign 元設計,TableDesign 入力,string? 基準宣言,bool 開いているか,bool 未保管か=false,削除保管? 削除=null);
    private sealed record 削除保管(RemovedTableDefinition 記録,long 基準版,bool 確定か=false);
    private sealed class テーブル文書(string id,string definitionId,string database,TableDesign design,bool readOnly,ManagementClient connection){
        internal readonly string ID=id,定義ID=definitionId,DB名=database;
        internal TableDesign 設計=design;
        internal bool 参照専用か=readOnly;
        internal readonly TextBlock 名前表示=new();
        internal readonly TextBlock アイコン=new(){Margin=new Thickness(0,0,5,0),FontFamily=new FontFamily("Segoe UI Symbol")};
        internal TabItem タブ=new();
        internal テーブル設計? パネル;
        internal Definition? 表示元;
        internal long? 稼働版;
        internal 削除保管? 削除;
        internal readonly ManagementClient 接続=connection;
    }
    private string? 表作業ファイル{get{var path=this.連携設定パス;if(path is null){return null;}return Path.Combine(this.設定ディレクトリ,Path.GetFileName(path).Replace("sources-","objects-",StringComparison.Ordinal));}}
    private static string 作業記録ハッシュ(string text)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    private void 表作業を保存(){
        var path=this.表作業ファイル;if(path is null){return;}this.表作業未記録か=true;Directory.CreateDirectory(this.設定ディレクトリ);var text=JsonSerializer.Serialize(this.表作業一覧.Values.ToArray(),ManagementJson.Options);if(text.Length>16*1024*1024){throw new IOException("テーブル作業記録が大きすぎます。");}
        using var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);string? current=null;if(File.Exists(path)){current=作業記録ハッシュ(File.ReadAllText(path));}var output=path;if(current!=this.表作業保存ハッシュ){output=path+".conflict-"+this.表作業退避ID+".json";}
        var temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){using var writer=new StreamWriter(stream,leaveOpen:true);writer.Write(text);writer.Flush();stream.Flush(flushToDisk:true);}File.Move(temporary,output,overwrite:true);}finally{if(File.Exists(temporary)){File.Delete(temporary);}}
        if(output==path){this.表作業保存ハッシュ=作業記録ハッシュ(text);}else{this.診断.Text="別のManagerの作業記録を保持し、この画面の入力を退避しました: "+output;}this.表作業未記録か=false;this.表再開表示を更新();
    }
    private void 表作業を読む(){
        foreach(var document in this.表文書.Values){document.パネル?.停止();}this.表文書.Clear();this.表作業一覧.Clear();this.DB作業定義.Clear();this.DB作業版.Clear();this.表作業保存ハッシュ=null;this.表作業未記録か=false;var path=this.表作業ファイル;if(path is null||!File.Exists(path)){return;}if(new FileInfo(path).Length>64*1024*1024){throw new InvalidDataException("テーブル作業記録が大きすぎます。");}
        var text=File.ReadAllText(path);this.表作業保存ハッシュ=作業記録ハッシュ(text);foreach(var state in JsonSerializer.Deserialize<表作業[]>(text,ManagementJson.Options)??[]){this.表作業一覧.Add(state.ID,state);if(state.開いているか&&this.対象の権限があるか(state.DB名,ManagementPermission.View|ManagementPermission.Edit)){var document=this.表タブを登録(state.ID,state.定義ID,state.DB名,state.元設計,false);document.削除=state.削除;this.削除の見出しを更新(document);}}this.表再開表示を更新();
    }
    private Definition DB作業を取得(string id){
        if(this.編集定義?.Id==id&&(this.下書き版>0||this.未保存か)){return this.現在定義();}if(this.連携プロジェクト.TryGetValue(id,out var workspace)){return workspace.Current.Definition;}if(this.DB作業定義.TryGetValue(id,out var source)){return source;}throw new ManagementException("Selection","DBの下書きを開いてください。");
    }
    private テーブル文書 表タブを登録(string id,string definitionId,string database,TableDesign design,bool readOnly){
        if(this.表文書.TryGetValue(id,out var existing)){return existing;}existing=this.表文書.Values.FirstOrDefault(item=>item.定義ID==definitionId&&item.設計.Path==design.Path);if(existing is not null){return existing;}var document=new テーブル文書(id,definitionId,database,design,readOnly,this.必須接続());var key=definitionId+"/"+design.Path;
        if(this.ソース文書.TryGetValue(key,out var tab)){document.タブ=tab;}else{this.ソース文書[key]=document.タブ;this.文書選択中か=true;try{this.ソース文書タブ.Items.Add(document.タブ);}finally{this.文書選択中か=false;}}
        var header=new StackPanel{Orientation=Orientation.Horizontal};header.Children.Add(document.アイコン);header.Children.Add(document.名前表示);var close=new Button{Content="×",Margin=new Thickness(7,0,0,0),Padding=new Thickness(3,0,3,0),ToolTip="このタブを閉じる（ファイルと下書きは保持）"};close.Click+=async(_,args)=>{args.Handled=true;await this.操作Async(()=>this.表タブを閉じるAsync(document));};header.Children.Add(close);document.タブ.Header=header;document.タブ.Tag=document;document.名前表示.Text=design.Path;document.アイコン.Text="▦";document.タブ.ToolTip=database+" / "+design.Path;this.表文書.Add(id,document);this.ソース文書タブ.Visibility=Visibility.Visible;return document;
    }
    private async Task<bool> 表の下書きを開くAsync(テーブル文書 document){
        this.表の接続を確認(document);
        if(this.編集定義?.Id==document.定義ID&&(this.下書き版>0||this.未保存か)){return true;}
        if(!this.破棄を確認("テーブルの下書きを開く")){return false;}if(this.連携プロジェクト.TryGetValue(document.定義ID,out var linked)){this.定義を表示(linked.Current.Definition,linked.Current.DraftRevision,linked.Current.BaseVersion);}else if(this.DB作業定義.TryGetValue(document.定義ID,out var source)&&this.DB作業版.TryGetValue(document.定義ID,out var version)){this.定義を表示(source,version.下書き,version.基準);}else{try{var draft=await document.接続.CallAsync<Draft>("draft",document.DB名,new{id=document.定義ID});this.表の接続を確認(document);this.定義を表示(draft.Definition,draft.Revision,draft.BaseVersion);}catch(ManagementException error)when(error.Code=="NotFound"){var live=await document.接続.CallAsync<DefinitionVersion>("definition",document.DB名,new{id=document.定義ID});this.表の接続を確認(document);this.定義を表示(live.Definition,0,live.Version);}}return true;
    }
    private async Task 表タブを選択Async(テーブル文書 document){
        this.表の接続を確認(document);
        if(!document.参照専用か&&!await this.表の下書きを開くAsync(document)){return;}
        if(document.参照専用か&&document.表示元 is{} live&&document.稼働版 is{} version&&(this.編集定義?.Hash()!=live.Hash()||this.下書き版!=0)){if(!this.破棄を確認("稼働版の構造を表示")){return;}this.定義を表示(live,0,version);}
        if(!document.参照専用か&&document.削除 is null&&document.パネル is{未保管か:false}){var latest=this.DB作業を取得(document.定義ID);if(this.表の基準宣言(latest,document.設計.Path)!=this.表の基準宣言(document.表示元,document.設計.Path)){document.設計=DatabaseDesigner.Read(latest,document.設計.Path);this.表パネルを構築(document,latest);}}
        if(document.パネル is null){var definition=this.編集定義;if(definition?.Id!=document.定義ID){return;}this.DB作業定義[definition.Id]=definition;this.表パネルを構築(document,definition);
        }this.表の接続を確認(document);this.表領域.Content=this.表の表示面(document);this.編集タブ.SelectedIndex=テーブルタブ番号;this.ソース文書タブ.SelectedItem=document.タブ;this.権限表示を更新();
    }
    private FrameworkElement 表の表示面(テーブル文書 document){
        var dock=new DockPanel{Background=Brushes.White};var actions=new WrapPanel{Margin=new Thickness(6)};var text="個人下書き · 自動保管 · 稼働DBへは未適用";if(document.参照専用か){text="稼働版の構造 · 参照用";}actions.Children.Add(new TextBlock{Text=document.DB名+" / "+document.設計.Path+" · "+text,Foreground=Brushes.Black,Margin=new Thickness(5)});
        if(document.削除 is null&&document.参照専用か&&document.表示元 is not null&&this.対象の権限があるか(document.DB名,ManagementPermission.Edit|ManagementPermission.View)){actions.Children.Add(押下("このタブで編集",()=>this.操作Async(async()=>{if(!await this.表の下書きを開くAsync(document)){return;}document.参照専用か=false;document.設計=DatabaseDesigner.Read(this.現在定義(),document.設計.Path);this.表パネルを構築(document,this.現在定義());await this.表タブを選択Async(document);})));}
        if(!document.参照専用か&&document.削除 is null&&this.対象の権限があるか(document.DB名,ManagementPermission.Edit)){actions.Children.Add(押下("削除予定にする",()=>this.操作Async(()=>this.表の削除を下書きにするAsync(document))));if(this.対象の権限があるか(document.DB名,ManagementPermission.Apply)){actions.Children.Add(押下("このDBをすべて保存…",()=>this.操作Async(()=>this.表のDBをすべて保存Async(document))));}}
        DockPanel.SetDock(actions,Dock.Top);dock.Children.Add(actions);if(document.パネル?.Parent is Panel parent){parent.Children.Remove(document.パネル);}dock.Children.Add(document.パネル!);return this.削除表示を重ねる(document,dock);
    }
    private void 表パネルを構築(テーブル文書 document,Definition? definition){
        if(!document.参照専用か&&document.削除 is null&&definition is not null&&this.表作業一覧.TryGetValue(document.ID,out var saved)&&!saved.未保管か&&saved.元設計.OriginalPath is{} path){document.設計=DatabaseDesigner.Read(definition,path);}
        document.パネル?.停止();document.表示元=definition;var panel=new テーブル設計(definition,document.設計,document.参照専用か||document.削除 is not null);document.パネル=panel;panel.自動保管か=!document.参照専用か&&document.削除 is null;panel.コード表示要求=()=>this.表のCSharpを表示(document);if(document.削除 is not null){panel.参照案内("削除前の構造 · 参照用");}
        if(!document.参照専用か&&document.削除 is null&&definition is not null){this.DB作業定義[definition.Id]=definition;panel.最新定義=()=>{this.表の接続を確認(document);var source=this.DB作業を取得(document.定義ID);if(this.表作業一覧.TryGetValue(document.ID,out var state)&&state.基準宣言 is not null&&state.基準宣言!=this.表の基準宣言(source,state.元設計.OriginalPath)){throw new ManagementException("Conflict","外部のテーブル変更があります。入力は作業記録へ保持しました。");}return source;};panel.作業変更=design=>{this.表の接続を確認(document);this.表作業一覧[document.ID]=new(document.ID,document.定義ID,document.DB名,document.設計,design,this.表の基準宣言(document.表示元,document.設計.OriginalPath),true,panel.未保管か);this.表作業を保存();if(panel.未保管か){document.名前表示.Text="*"+design.Path;document.アイコン.Text="✎";}};
            panel.反映要求=async(candidate,design,code)=>{
                this.表の接続を確認(document);this.DB作業定義[document.定義ID]=candidate;this.ソース文書[document.定義ID+"/"+design.Path]=document.タブ;var active=this.編集定義?.Id==document.定義ID;
                if(active){this.ソースを背景更新(candidate,false);this.未保存か=true;candidate=(await this.保存Async()).Definition;}else{throw new ManagementException("Conflict","別のDBを表示中です。対象のタブを開いて保管してください。");}
                this.表の接続を確認(document);document.設計=DatabaseDesigner.Read(candidate,design.Path);document.表示元=candidate;this.表作業一覧[document.ID]=new(document.ID,document.定義ID,document.DB名,document.設計,document.設計,this.表の基準宣言(candidate,design.Path),true);this.表作業を保存();document.名前表示.Text="*"+design.Path;document.アイコン.Text="✎";if(candidate.Migration?.NewTables?.Contains(design.Path)==true){document.アイコン.Text="＋";}if(code){this.表のCSharpを表示(document);}return candidate;
            };
            if(this.表作業一覧.TryGetValue(document.ID,out var state)&&state.未保管か){panel.入力を更新(state.入力);}else{panel.作業変更(document.設計);}
        }
    }
    private string? 表の基準宣言(Definition? definition,string? path){if(definition is null||path is null){return null;}try{return DatabaseDesigner.TableSource(definition,path).Text;}catch(ManagementException){return null;}}
    private async Task 表タブを閉じるAsync(テーブル文書 document){
        this.表の接続を確認(document);document.パネル?.停止();if(document.パネル is not null&&!document.参照専用か&&document.削除 is null){await document.パネル.保管Async(false);}this.表の接続を確認(document);if(this.表作業一覧.TryGetValue(document.ID,out var state)){this.表作業一覧[document.ID]=state with{開いているか=false};this.表作業を保存();}
        var 選択中か=this.ソース文書タブ.SelectedItem==document.タブ;this.文書選択中か=true;try{this.ソース文書タブ.Items.Remove(document.タブ);foreach(var key in this.ソース文書.Where(pair=>ReferenceEquals(pair.Value,document.タブ)).Select(pair=>pair.Key).ToArray()){this.ソース文書.Remove(key);}this.表文書.Remove(document.ID);if(選択中か){this.ソース文書タブ.SelectedIndex=-1;}}finally{this.文書選択中か=false;}if(選択中か){this.表領域.Content=null;this.編集タブ.SelectedIndex=3;}this.状態.Text="タブを閉じました。ファイルと個人下書きは保持しています。";
    }
    private string 文書表示名(TabItem tab){if(tab.Tag is テーブル文書 document){return document.名前表示.Text;}return tab.Header?.ToString()??"";}
    private void 表の接続を確認(テーブル文書 document){if(!ReferenceEquals(document.接続,this.クライアント)){throw new ManagementException("Connection","このタブを開いた接続は終了しています。");}}
    private void 表のCSharpを表示(テーブル文書 document){
        this.表の接続を確認(document);if(this.編集定義?.Id!=document.定義ID){return;}this.編集タブ.SelectedIndex=0;
        try{var location=DatabaseDesigner.TableSource(this.現在定義(),document.設計.Path);this.ファイル一覧.SelectedItem=location.Path;if(location.Start+location.Length<=this.コード.Text.Length){this.コード.Select(location.Start,location.Length);this.コード.ScrollToLine(this.コード.Document.GetLineByOffset(location.Start).LineNumber);}}
        catch(ManagementException error){this.ソース案内.Text="宣言位置を確定できません: "+error.Message;}
    }
    private void 作業画面を分離(){
        this.監査接続を消去();this.オブジェクト詳細.Text="";this.属性を表示(false);
        foreach(var document in this.表文書.Values){document.パネル?.停止();}this.表領域.Content=null;this.表文書.Clear();this.表作業一覧.Clear();this.DB作業定義.Clear();this.DB作業版.Clear();this.連携プロジェクト.Clear();this.ソース文書.Clear();this.文書表示ハッシュ.Clear();this.表再開ボタン.Visibility=Visibility.Collapsed;
        this.文書選択中か=true;try{this.ソース文書タブ.Items.Clear();this.ソース文書タブ.Visibility=Visibility.Collapsed;}finally{this.文書選択中か=false;}
    }
    private void 表再開表示を更新(){this.表再開ボタン.Visibility=Visibility.Collapsed;if(this.表作業一覧.Values.Any(item=>!item.開いているか&&this.対象の権限があるか(item.DB名,ManagementPermission.View|ManagementPermission.Edit))){this.表再開ボタン.Visibility=Visibility.Visible;}}
    private sealed record 表再開候補(表作業 作業,string 表示);
    private async Task 閉じた表を選ぶAsync(){
        var list=new ListBox{ItemsSource=this.表作業一覧.Values.Where(item=>!item.開いているか&&this.対象の権限があるか(item.DB名,ManagementPermission.View|ManagementPermission.Edit)).Select(item=>new 表再開候補(item,item.DB名+" / "+item.入力.Path)).ToArray(),DisplayMemberPath=nameof(表再開候補.表示),Margin=new Thickness(8)};
        var window=new Window{Owner=this,Title="閉じたテーブルの下書き",Width=500,Height=400,WindowStartupLocation=WindowStartupLocation.CenterOwner};var layout=new DockPanel();var open=押下("再開",()=>{if(list.SelectedItem is 表再開候補){window.DialogResult=true;}return Task.CompletedTask;});DockPanel.SetDock(open,Dock.Bottom);layout.Children.Add(open);layout.Children.Add(list);window.Content=layout;
        if(window.ShowDialog()==true&&list.SelectedItem is 表再開候補 selected){await this.閉じた表を再開Async(selected.作業);}
    }
    private async Task 閉じた表を再開Async(表作業 state){
        if(!this.対象の権限があるか(state.DB名,ManagementPermission.View|ManagementPermission.Edit)){throw new ManagementException("Permission","下書きを開く権限がありません。");}
        var document=this.表タブを登録(state.ID,state.定義ID,state.DB名,state.元設計,false);document.削除=state.削除;this.削除の見出しを更新(document);await this.表タブを選択Async(document);if(this.表作業一覧.TryGetValue(state.ID,out var updated)){this.表作業一覧[state.ID]=updated with{開いているか=true};this.表作業を保存();}
    }
    private void 表の差分見出しを更新(テーブル文書 document,Definition definition,bool? sourceChanged=null){
        if(document.削除 is not null){this.削除の見出しを更新(document);return;}var path=document.設計.Path;var added=document.設計.OriginalPath is null||definition.Migration?.NewTables?.Contains(path)==true;var changed=added||document.パネル?.未保管か==true||definition.Migration?.Tables.Any(table=>table.Target==path)==true||sourceChanged==true;
        if(!changed&&sourceChanged is null&&this.連携プロジェクト.TryGetValue(definition.Id,out var workspace)){changed=this.表の基準宣言(definition,path)!=this.表の基準宣言(definition with{Source=workspace.State.Published},path);}
        document.名前表示.Text=path;document.アイコン.Text="▦";if(changed){document.名前表示.Text="*"+path;document.アイコン.Text="✎";}if(added){document.アイコン.Text="＋";}
    }
    private async Task テーブルのタブを開くAsync(Definition? definition,TableDesign design,string database,string definitionId,bool readOnly,long? liveVersion=null){
        var id=definitionId+"/"+design.Path;var document=this.表タブを登録(id,definitionId,database,design,readOnly);if(this.表作業一覧.TryGetValue(document.ID,out var state)&&state.削除 is not null){document.削除=state.削除;document.参照専用か=false;document.設計=state.元設計;this.表パネルを構築(document,definition);this.削除の見出しを更新(document);}else if(document.パネル is null||document.参照専用か!=readOnly||readOnly){document.参照専用か=readOnly;document.稼働版=liveVersion;document.設計=design;this.表パネルを構築(document,definition);}await this.表タブを選択Async(document);
    }
}
