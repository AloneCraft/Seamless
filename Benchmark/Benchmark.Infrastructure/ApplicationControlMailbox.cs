using System.Diagnostics;
using System.Text.Json;

namespace Benchmark.Infrastructure;

public sealed class アプリ操作メールボックス:Iアプリ操作通信{
    public static readonly JsonSerializerOptions 通信JSON=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true};
    private readonly string メールボックス;
    private readonly TimeSpan 応答制限;
    public ExternalTarget 接続先{get;}

    public アプリ操作メールボックス(string メールボックス,ExternalTarget 接続先,TimeSpan? 応答制限=null){
        if(!Path.IsPathFullyQualified(メールボックス)){throw new ArgumentException("登録済みメールボックスの絶対パスが必要です。",nameof(メールボックス));}
        this.メールボックス=Path.GetFullPath(メールボックス);
        this.接続先=接続先;
        this.応答制限=応答制限??TimeSpan.FromSeconds(3);
        if(this.応答制限<=TimeSpan.Zero||this.応答制限>TimeSpan.FromSeconds(5)){throw new ArgumentOutOfRangeException(nameof(応答制限));}
    }

    public static アプリ操作メールボックス? 環境から作成(){
        var メールボックス=Environment.GetEnvironmentVariable("SEAMLESS_APP_CONTROL_MAILBOX");
        if(string.IsNullOrWhiteSpace(メールボックス)){return null;}
        var 共有状態=Environment.GetEnvironmentVariable("BGO_STATE");
        if(string.IsNullOrWhiteSpace(共有状態)||!Path.IsPathFullyQualified(共有状態)){
            throw new InvalidDataException("アプリ内部操作には BGO_STATE の絶対パスを明示してください。");
        }
        using var 自身=Process.GetCurrentProcess();
        return new(メールボックス,new(Guid.NewGuid().ToString("N"),自身.Id,自身.StartTime.ToUniversalTime().Ticks));
    }

    public async Task<ExternalReply> 送信Async(ExternalRequest 要求,CancellationToken 取消){
        if(要求.External!=this.接続先){throw new InvalidDataException("異なるアプリinstanceの要求です。");}
        var 呼出ID=Guid.NewGuid().ToString("N");
        var 要求パス=Path.Combine(this.メールボックス,"requests",呼出ID+".request.json");
        var 応答パス=Path.Combine(this.メールボックス,"responses",呼出ID+".response.json");
        var データ=JsonSerializer.SerializeToUtf8Bytes(new ExternalFileRequest(this.接続先.Pid,this.接続先.Start,要求),通信JSON);
        if(データ.Length>1048576){throw new InvalidDataException("要求が1MiBを超えています。");}
        永続的に公開(要求パス,データ);
        using var 期限=CancellationTokenSource.CreateLinkedTokenSource(取消);
        期限.CancelAfter(this.応答制限);
        try{
            while(!File.Exists(応答パス)){await Task.Delay(25,期限.Token).ConfigureAwait(false);}
            if(new FileInfo(応答パス).Length>1048576){throw new InvalidDataException("応答が1MiBを超えています。");}
            var 応答=JsonSerializer.Deserialize<ExternalReply>(await File.ReadAllBytesAsync(応答パス,期限.Token).ConfigureAwait(false),通信JSON)??throw new InvalidDataException("BGOの応答が空です。");
            File.Delete(応答パス);
            return 応答;
        }catch(OperationCanceledException) when(!取消.IsCancellationRequested){
            throw new TimeoutException("BGOメールボックスの応答期限を超えました。同一操作を再実行しません。");
        }
    }

    public Task 終端を保存Async(ExternalTaskRecord 操作,ExternalTerminalReceipt 終端){
        if(!Path.IsPathFullyQualified(操作.ArtifactDirectory)||終端.External!=this.接続先||操作.Id!=終端.TaskId){throw new InvalidDataException("終端receiptの束縛が不正です。");}
        var データ=JsonSerializer.SerializeToUtf8Bytes(終端,通信JSON);
        if(データ.Length>65536){throw new InvalidDataException("終端receiptが64KiBを超えています。");}
        var パス=Path.Combine(操作.ArtifactDirectory,"external-terminal.json");
        if(File.Exists(パス)){
            if(new FileInfo(パス).Length<=65536&&File.ReadAllBytes(パス).AsSpan().SequenceEqual(データ)){return Task.CompletedTask;}
            throw new InvalidDataException("既存receiptの内容が異なります。上書きしません。");
        }
        永続的に公開(パス,データ);
        return Task.CompletedTask;
    }

    private static void 永続的に公開(string 最終パス,byte[] データ){
        var 一時パス=最終パス+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var ファイル=new FileStream(一時パス,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                ファイル.Write(データ);
                ファイル.Flush(true);
            }
            File.Move(一時パス,最終パス);
        }finally{
            if(File.Exists(一時パス)){File.Delete(一時パス);}
        }
    }
}
