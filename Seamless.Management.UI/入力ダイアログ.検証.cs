using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Seamless.Management.UI;

internal sealed partial class 入力ダイアログ{
    /// <summary>表示・OS入力を使わず、製品の決定ボタンと同じ検証経路を通す。</summary>
    internal void パスワード検証を試験する(int パスワード位置,int 確認位置,string 画像ファイル){
        var 秘密=(PasswordBox)this.入力欄[パスワード位置];var 確認=(PasswordBox)this.入力欄[確認位置];
        foreach(var 長さ in new[]{0,1,11}){
            秘密.Password=new string('a',長さ);確認.Password=秘密.Password;var 元=this.値;
            this.決定.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(this.DialogResult is not null||this.エラー.Visibility!=Visibility.Visible||!this.エラー.Text.Contains("12文字",StringComparison.Ordinal)||!元.SequenceEqual(this.値)){throw new InvalidOperationException("短いパスワードを拒否し、入力を保持できませんでした。");}
        }
        var 要素=(FrameworkElement)this.Content;要素.Measure(new Size(620,double.PositiveInfinity));要素.Arrange(new Rect(new Size(620,要素.DesiredSize.Height)));要素.UpdateLayout();
        var 画像=new RenderTargetBitmap(620,(int)Math.Ceiling(要素.ActualHeight),96,96,PixelFormats.Pbgra32);画像.Render(要素);var 保存=new PngBitmapEncoder();保存.Frames.Add(BitmapFrame.Create(画像));using(var ファイル=File.Create(画像ファイル)){保存.Save(ファイル);}
        秘密.Password=new string('a',12);確認.Password=new string('b',12);this.決定.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(this.DialogResult is not null||!this.エラー.Text.Contains("一致",StringComparison.Ordinal)||確認.BorderBrush!=Brushes.Firebrick){throw new InvalidOperationException("確認欄の不一致を表示できませんでした。");}
        確認.Password=秘密.Password;if(!this.入力が有効か()||this.エラー.Visibility!=Visibility.Collapsed){throw new InvalidOperationException("12文字に修正した入力を受け付けられませんでした。");}
        秘密.Password=new string('a',13);確認.Password=秘密.Password;if(!this.入力が有効か()){throw new InvalidOperationException("13文字の入力を受け付けられませんでした。");}
        this.Close();
    }
}
