using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private FrameworkElement 削除表示を重ねる(テーブル文書 document,FrameworkElement 内容){
        if(document.削除 is not{} removal){return 内容;}var 面=new Grid();内容.IsEnabled=false;面.Children.Add(内容);
        var 重ね=new Grid{Background=new SolidColorBrush(Color.FromArgb(104,255,230,230))};var カード=new StackPanel{Margin=new Thickness(16),MaxWidth=560};var 見出し="削除予定";var 説明="削除は個人下書きにあります。稼働DBのデータはまだ変更していません。";
        if(removal.確定か){見出し="削除を適用済み";説明="データを戻すには、適用前のバックアップからの復旧が必要です。";}
        カード.Children.Add(new TextBlock{Text=見出し+" · "+document.設計.Path,FontSize=22,FontWeight=FontWeights.Bold,Foreground=Brushes.DarkRed,TextWrapping=TextWrapping.Wrap});カード.Children.Add(new TextBlock{Text=説明,Margin=new Thickness(0,8,0,8),Foreground=Brushes.Black,TextWrapping=TextWrapping.Wrap});
        if(!removal.確定か&&this.対象の権限があるか(document.DB名,ManagementPermission.Edit)){カード.Children.Add(押下("削除を取り消す",()=>this.操作Async(()=>this.表の削除を取り消すAsync(document))));if(this.対象の権限があるか(document.DB名,ManagementPermission.Apply)){カード.Children.Add(押下("このDBをすべて保存…",()=>this.操作Async(()=>this.表のDBをすべて保存Async(document))));}}
        重ね.Children.Add(new Border{Child=カード,Background=Brushes.White,BorderBrush=Brushes.DarkRed,BorderThickness=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});面.Children.Add(重ね);return 面;
    }
    private void 削除の見出しを更新(テーブル文書 document){if(document.削除 is not{} removal){return;}document.アイコン.Text="⌫";document.名前表示.Text="*"+document.設計.Path+"（削除予定）";if(removal.確定か){document.名前表示.Text=document.設計.Path+"（削除済み）";}}
    private async Task 表の削除を下書きにするAsync(テーブル文書 document){
        if(document.削除 is not null||!await this.表の下書きを開くAsync(document)){return;}
        document.パネル?.停止();if(document.パネル is not null&&!document.参照専用か){await document.パネル.保管Async(false);}this.表の接続を確認(document);var 定義=this.現在定義();if(定義.Id!=document.定義ID){throw new ManagementException("Selection","表示しているDBが変わりました。");}
        var path=document.設計.Path;var token=DatabaseDesigner.CaptureRemoval(定義,path);var 変更=DatabaseDesigner.Remove(定義,path);var design=DatabaseDesigner.Read(定義,path);
        var state=new 表作業(document.ID,定義.Id,定義.Database,design,design,this.表の基準宣言(定義,path),true);if(this.表作業一覧.TryGetValue(document.ID,out var existing)){state=existing;}
        document.参照専用か=false;document.設計=design;document.削除=new(token,this.基準版);this.表作業一覧[document.ID]=state with{元設計=design,開いているか=true,削除=document.削除};this.表作業を保存();
        this.表パネルを構築(document,定義);this.削除の見出しを更新(document);await this.表タブを選択Async(document);this.ソースを背景更新(変更,false);this.未保存か=true;await this.保存Async();this.状態.Text="削除を下書きへ記録しました。稼働DBは変更していません。";
    }
    private async Task 表の削除を取り消すAsync(テーブル文書 document){
        if(document.削除 is not{} removal||removal.確定か||!await this.表の下書きを開くAsync(document)){return;}
        var live=await document.接続.CallAsync<DefinitionVersion>("definition",document.DB名,new{id=document.定義ID});this.表の接続を確認(document);if(live.Version!=removal.基準版){throw new ManagementException("Conflict","稼働版が進んでいます。適用済みの削除と区別するため、結果と差分を確認してください。");}
        var current=this.現在定義();if(current.Id!=document.定義ID){throw new ManagementException("Selection","表示しているDBが変わりました。");}
        if(!DatabaseDesigner.Tables(current).Contains(removal.記録.Path)){var restored=DatabaseDesigner.RestoreRemoved(current,removal.記録);this.ソースを背景更新(restored,false);this.未保存か=true;current=(await this.保存Async()).Definition;}
        else{var original=DatabaseDesigner.CaptureRemoval(current,removal.記録.Path);if(original.Declaration!=removal.記録.Declaration||original.RowFingerprint!=removal.記録.RowFingerprint){throw new ManagementException("Conflict","削除対象と同名の変更があるため、両方の内容を保持します。");}}
        this.表の接続を確認(document);document.削除=null;document.設計=DatabaseDesigner.Read(current,removal.記録.Path);if(this.表作業一覧.TryGetValue(document.ID,out var state)){this.表作業一覧[document.ID]=state with{削除=null};this.表作業を保存();}
        this.表パネルを構築(document,current);this.表の差分見出しを更新(document,current);await this.表タブを選択Async(document);this.状態.Text="この表の削除予定だけを取り消しました。";
    }
    private void 削除の公開を確定(Definition definition){
        if(definition.Kind!=DefinitionKind.Database){return;}var tables=DatabaseDesigner.Tables(definition).ToHashSet();var dirty=false;
        foreach(var item in this.表作業一覧.Values.Where(item=>item.定義ID==definition.Id&&item.削除 is{確定か:false}).ToArray()){if(tables.Contains(item.削除!.記録.Path)){continue;}var removal=item.削除 with{確定か=true};this.表作業一覧[item.ID]=item with{削除=removal};if(this.表文書.TryGetValue(item.ID,out var document)){document.削除=removal;this.削除の見出しを更新(document);if(this.ソース文書タブ.SelectedItem==document.タブ){this.表領域.Content=this.表の表示面(document);}}dirty=true;}
        if(dirty){this.表作業を保存();}
    }
    private async Task 表のDBをすべて保存Async(テーブル文書 document){await this.表のDBの作業をまとめるAsync(document);await this.適用を実行Async(this.適用を確認);}
    private async Task 表のDBの作業をまとめるAsync(テーブル文書 document){
        if(!await this.表の下書きを開くAsync(document)){throw new OperationCanceledException();}await this.DBの作業をまとめるAsync(document.定義ID);
    }
    private async Task DBの作業をまとめるAsync(string definitionId){
        var 接続=this.必須接続();
        foreach(var item in this.表文書.Values.Where(item=>item.定義ID==definitionId&&!item.参照専用か&&item.削除 is null).ToArray()){if(item.パネル is not null){await item.パネル.保管Async(false);}}
        var pending=this.表作業一覧.Values.Where(item=>item.定義ID==definitionId&&item.未保管か&&item.削除 is null).Select(item=>item.入力.Path).ToArray();if(pending.Length>0){throw new ManagementException("Validation","未保管の入力を確認してください: "+string.Join(", ",pending));}
        if(!ReferenceEquals(接続,this.クライアント)||this.現在定義().Id!=definitionId){throw new ManagementException("Selection","接続先か表示しているDBが変わりました。");}var saved=await this.保存Async();if(!ReferenceEquals(接続,this.クライアント)||this.現在定義().Id!=definitionId){throw new ManagementException("Selection","接続先か表示しているDBが変わりました。");}var deletes=this.表作業一覧.Values.Where(item=>item.定義ID==definitionId&&item.削除 is{確定か:false}).Select(item=>item.削除!.記録.Path).ToArray();if(deletes.Length>0&&DatabaseDesigner.Tables(saved.Definition).Intersect(deletes).Any()){throw new ManagementException("Conflict","削除予定が作業ソースに反映されていません。対象を確認してください。");}
    }
}
