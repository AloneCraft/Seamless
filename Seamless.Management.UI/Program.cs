using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Seamless.Management.UI;

public static class 起動{
    [STAThread]public static int Main(string[] 引数){
        string? 設定ファイル=null;var 設定位置=Array.IndexOf(引数,"--config");if(設定位置>=0&&設定位置+1<引数.Length){設定ファイル=引数[設定位置+1];}
        if(設定ファイル is null&&Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT") is null&&引数.Contains("--workspace-config")){設定ファイル=ManagementLaunchConfiguration.WorkspacePath();}
        var アプリ=new Application();var 画面=new 管理ウィンドウ(設定ファイル);
        if(引数.Length==3&&引数[0]=="--smoke"){
            アプリ.ShutdownMode=ShutdownMode.OnExplicitShutdown;var 終了=0;
            アプリ.Startup+=async(_,_)=>{try{await 画面.画面検証Async(引数[1],引数[2]);}catch(Exception 例外){System.IO.File.WriteAllText(引数[2]+".error.txt",例外.ToString());終了=1;}finally{画面.画面検証を終了();アプリ.Shutdown(終了);}};アプリ.Run();return 終了;
        }
        if(引数.Length==2&&引数[0]=="--render"){
            画像保存(画面,引数[1]);return 0;
        }
        アプリ.Run(画面);return 0;
    }
    internal static void 画像保存(管理ウィンドウ 画面,string 出力){
        var 要素=(FrameworkElement)画面.Content;要素.Measure(new Size(1600,1000));要素.Arrange(new Rect(0,0,1600,1000));要素.UpdateLayout();
        画面.画像配置();要素.UpdateLayout();
        var 画像=new RenderTargetBitmap(1600,1000,96,96,PixelFormats.Pbgra32);画像.Render(要素);var 保存=new PngBitmapEncoder();保存.Frames.Add(BitmapFrame.Create(画像));using var ファイル=System.IO.File.Create(出力);保存.Save(ファイル);
    }
}
