using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Seamless.Management.UI;

internal sealed record 入力項目(string 名前,string 初期値="",bool 秘密か=false,bool 複数行か=false,bool 読取専用か=false);
internal sealed record 入力エラー(string 説明,int 項目);
internal sealed partial class 入力ダイアログ:Window{
    private readonly List<Control> 入力欄=[];
    private readonly Button 決定=new(){Content="決定",IsDefault=true,Padding=new Thickness(20,7,20,7),Margin=new Thickness(4)};
    private readonly TextBlock エラー=new(){Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};
    internal Func<string[],入力エラー?>? 入力検証{get;set;}
    internal string 決定ラベル{set=>this.決定.Content=value;}
    internal string[] 値=>this.入力欄.Select(欄=>{if(欄 is PasswordBox 秘密){return 秘密.Password;}return ((TextBox)欄).Text;}).ToArray();
    internal 入力ダイアログ(Window 親,string 題名,params 入力項目[] 項目){
        this.Owner=親;this.Title=題名;this.Width=650;this.SizeToContent=SizeToContent.Height;this.MaxHeight=800;this.WindowStartupLocation=WindowStartupLocation.CenterOwner;this.ShowInTaskbar=false;
        var 全体=new StackPanel{Margin=new Thickness(18)};
        foreach(var 指定 in 項目){全体.Children.Add(new TextBlock{Text=指定.名前,Margin=new Thickness(0,8,0,4)});
            Control 欄;if(指定.秘密か){欄=new PasswordBox{Padding=new Thickness(6)};}else{var テキスト=new TextBox{Text=指定.初期値,Padding=new Thickness(6),IsReadOnly=指定.読取専用か};if(指定.複数行か){テキスト.AcceptsReturn=true;テキスト.Height=180;テキスト.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;テキスト.FontFamily=new System.Windows.Media.FontFamily("Consolas");}欄=テキスト;}
            this.入力欄.Add(欄);全体.Children.Add(欄);
        }
        var ボタン列=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};
        AutomationProperties.SetAutomationId(this.エラー,"入力エラー");AutomationProperties.SetLiveSetting(this.エラー,AutomationLiveSetting.Assertive);全体.Children.Add(this.エラー);
        this.決定.Click+=(_,_)=>{if(this.入力が有効か()){this.DialogResult=true;}};
        var 取消=new Button{Content="取消",IsCancel=true,Padding=new Thickness(20,7,20,7),Margin=new Thickness(4)};ボタン列.Children.Add(this.決定);ボタン列.Children.Add(取消);全体.Children.Add(ボタン列);
        this.Content=new ScrollViewer{Content=全体,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    internal bool 入力が有効か(){
        var 問題=this.入力検証?.Invoke(this.値);foreach(var 欄 in this.入力欄){欄.ClearValue(Control.BorderBrushProperty);欄.ClearValue(ToolTipProperty);}
        if(問題 is null){this.エラー.Text="";this.エラー.Visibility=Visibility.Collapsed;return true;}
        this.エラー.Text=問題.説明;this.エラー.Visibility=Visibility.Visible;
        if(問題.項目>=0&&問題.項目<this.入力欄.Count){var 欄=this.入力欄[問題.項目];欄.BorderBrush=Brushes.Firebrick;欄.ToolTip=問題.説明;欄.Focus();}return false;
    }
    internal static 入力エラー? パスワードを検証(string[] 値,int パスワード位置,int? 確認位置=null,bool 空なら変更なしか=false){
        if(空なら変更なしか&&値[パスワード位置].Length==0){return null;}
        if(値[パスワード位置].Length<12){return new("パスワードは12文字以上で入力してください。",パスワード位置);}
        if(確認位置 is{} 位置&&値[パスワード位置]!=値[位置]){return new("確認用のパスワードが一致しません。",位置);}return null;
    }
}
