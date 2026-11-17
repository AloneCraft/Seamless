using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
namespace Seamless.Execution;
/// <summary>一時フォルダ(scope-* / store-*)を、所有者ロックを持ったまま削除する。</summary>
internal static class OwnedDirectory{
    private const int TimeoutMilliseconds=2000;
    /// <summary>
    /// 所有者ロックを持ったまま中身を消し、owner.json、最後にロック自身を消してから閉じ、空のフォルダを消す。
    /// ロックを先に手放すと、その間に <see cref="TemporaryResourceRecovery"/> が owner.json を見て放置されたものとしてロックを取り、
    /// 回収側がロックを持ったまま止まると(全件実行の負荷下など)、こちらの削除が共有違反のまま期限を過ぎて失敗していた。
    /// ロックは <see cref="FileShare.Delete"/> で開いておくので、持ったまま消せる。回収側は読み書きで開こうとするので、持っている間は取れない。
    /// owner.json は他の中身の後に消すので、途中で異常終了しても、owner.json が残っていれば次の回収で消える。
    /// </summary>
    internal static void Delete(string directory,FileStream ownerLock){
        var lockPath=ownerLock.Name;
        var metadata=Path.Combine(directory,"owner.json");
        try{
            foreach(var child in Directory.EnumerateDirectories(directory)){Directory.Delete(child,true);}
            foreach(var file in Directory.EnumerateFiles(directory)){
                if(PathEquals(file,lockPath)||PathEquals(file,metadata)){continue;}
                File.Delete(file);
            }
            File.Delete(metadata);
            File.Delete(lockPath);
        }catch(Exception error) when(error is IOException or UnauthorizedAccessException){
            // ロックを持っている間に消せなかったもの(外部のプログラムが一時的に開いているなど)は、下の再試行で消す。
        }finally{
            ownerLock.Dispose();
        }
        Delete(directory);
    }
    /// <summary>
    /// フォルダを削除する。外部のプログラムが一時的にファイルを開いていて共有違反(IOException)になったときは、期限まで削除し直す。
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
    private static bool PathEquals(string left,string right)=>string.Equals(Path.GetFullPath(left),Path.GetFullPath(right),StringComparison.OrdinalIgnoreCase);
}
