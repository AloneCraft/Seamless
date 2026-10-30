using System.Text;
using System.Text.Json;
namespace Benchmark.TpcC.Core;
/// <summary>失敗の種類。終了コードは <see cref="TpcCFailure.ExitCode"/>。</summary>
public enum TpcCFailureKind{
    /// <summary>取引の出力が組み合わせ同士で食い違った(L1)。</summary>
    Output,
    /// <summary>取引が書いた中身が食い違った(L2)。</summary>
    WriteSet,
    /// <summary>書いた行を読み直すと違った(L3)。</summary>
    ReadBack,
    /// <summary>表や索引のダイジェスト、一貫性の条件、索引と表の一致が崩れた(L4)。</summary>
    State,
    /// <summary>保存→開き直しで状態が変わった。</summary>
    Reopen,
    /// <summary>強制終了から開き直した状態が期待と違った。</summary>
    Crash,
    /// <summary>取引やハーネスの想定外の例外。</summary>
    Exception,
    /// <summary>資源の上限(メモリ・ディスク・ログ満杯)。</summary>
    Resource,
    /// <summary>子プロセスの応答なし。</summary>
    Hang,
}
/// <summary>その場で止めるための失敗。再現に必要な情報を持つ。</summary>
public sealed class TpcCFailure(TpcCFailureKind kind,string message,Exception? inner=null):Exception(message,inner){
    public TpcCFailureKind Kind{get;}=kind;
    public long TxnIndex{get;init;}
    public TpcCTxnKind? Transaction{get;init;}
    public string? Engine{get;init;}
    public object? Request{get;init;}
    /// <summary>組み合わせの名前→出力(または出力の要約)。</summary>
    public IReadOnlyDictionary<string,object?> Outputs{get;init;}=new Dictionary<string,object?>();
    public IReadOnlyList<string> Details{get;init;}=[];
    public int ExitCode{
        get{
            switch(this.Kind){
                case TpcCFailureKind.Exception:
                    return 2;
                case TpcCFailureKind.Resource:
                    return 4;
                case TpcCFailureKind.Hang:
                    return 5;
                default:
                    return 1;
            }
        }
    }
    /// <summary>失敗の記録を書く(failure.json と failure.txt)。</summary>
    public void Write(string directory,TpcCRunOptions options,long lastAgreedTxn){
        Directory.CreateDirectory(directory);
        var replay=options with{StopAt=this.TxnIndex+1,TraceFrom=Math.Max(1,this.TxnIndex-20)};
        var report=new{
            Kind=this.Kind.ToString(),
            this.Message,
            this.TxnIndex,
            Transaction=this.Transaction?.ToString(),
            this.Engine,
            LastAgreedTxn=lastAgreedTxn,
            Options=options,
            Reproduce=replay.ToCommandLine(),
            Request=Describe(this.Request),
            Outputs=this.Outputs.ToDictionary(static pair=>pair.Key,static pair=>Describe(pair.Value)),
            this.Details,
            Exception=this.InnerException?.ToString(),
        };
        File.WriteAllText(Path.Combine(directory,"failure.json"),JsonSerializer.Serialize(report,TpcCReportJson.Options));
        var text=new StringBuilder();
        text.AppendLine($"失敗: {this.Kind}");
        text.AppendLine(this.Message);
        text.AppendLine($"取引の通し番号: {this.TxnIndex} ({this.Transaction})");
        if(this.Engine is not null){
            text.AppendLine($"組み合わせ: {this.Engine}");
        }
        text.AppendLine($"最後に一致した取引の通し番号: {lastAgreedTxn}");
        foreach(var detail in this.Details){
            text.AppendLine(detail);
        }
        if(this.InnerException is not null){
            text.AppendLine(this.InnerException.ToString());
        }
        text.AppendLine("再現コマンド:");
        text.AppendLine(replay.ToCommandLine());
        File.WriteAllText(Path.Combine(directory,"failure.txt"),text.ToString());
    }
    public static string Describe(object? value){
        if(value is null){
            return "null";
        }
        try{
            return JsonSerializer.Serialize(value,value.GetType(),TpcCReportJson.Options);
        }catch(Exception error){
            return value+" (JSON: "+error.Message+")";
        }
    }
}
internal static class TpcCReportJson{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
}
