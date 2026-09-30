using System.Diagnostics;
namespace Seamless.Execution;
/// <summary>所有者ロックを手放したあとに、自分のフォルダを削除する。</summary>
internal static class OwnedDirectory{
    private const int TimeoutMilliseconds=2000;
    /// <summary>
    /// 所有者ロックを手放してから削除するので、その間に <see cref="TemporaryResourceRecovery"/> が放置されたものとして同じフォルダのロックを取り、
    /// 中身を確かめたり削除したりすることがある。回収側が数ミリ秒ロックや owner.json を開いている間は、削除が共有違反(IOException)になる。
    /// 回収側は削除中の所有者に会うと何も消さずに引き下がるので、待つだけでは消えない。回収側が手放したあとに削除し直す。
    /// 期限を過ぎても消せなければ、最後の例外を投げる。フォルダが既に消えていれば(回収側が消した)成功とする。
    /// </summary>
    internal static void Delete(string directory){
        var watch=Stopwatch.StartNew();
        while(true){
            try{
                Directory.Delete(directory,true);
                return;
            }catch(DirectoryNotFoundException){
                return;
            }catch(Exception error) when((error is IOException or UnauthorizedAccessException)&&watch.ElapsedMilliseconds<TimeoutMilliseconds){
                if(!Directory.Exists(directory)){return;}
                Thread.Sleep(5);
            }
        }
    }
}
