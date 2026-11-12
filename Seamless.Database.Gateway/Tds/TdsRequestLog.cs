using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tds;

// SSMS等が実際に送る要求を集めるための受信記録。GatewayServerOptions.RequestLogPathを指定したときだけ、1要求1行のJSONを追記する。
// 引数の値は実データを含み得るため名前と型だけを残し、SQL本文のPASSWORD/SECRET指定は伏せる。ログイン要求は記録しない。
public sealed partial class TdsRequestLog:IDisposable{
    private readonly object gate=new();
    private readonly StreamWriter writer;
    private long sequence;
    private bool disposed;
    public TdsRequestLog(string path){
        var full=global::System.IO.Path.GetFullPath(path);Directory.CreateDirectory(global::System.IO.Path.GetDirectoryName(full)!);
        this.writer=new StreamWriter(new global::System.IO.FileStream(full,FileMode.Append,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){AutoFlush=true};
        this.FilePath=full;
    }
    public string FilePath{get;}
    internal void Write(TdsRequestRecord record){
        var line=JsonSerializer.Serialize(new{
            utc=record.Utc.ToString("O",CultureInfo.InvariantCulture),
            seq=Interlocked.Increment(ref this.sequence),
            session=record.Session,
            database=record.Database,
            kind=record.Kind,
            procedure=record.Procedure,
            sql=Mask(record.Sql),
            declarations=record.Declarations,
            parameters=record.Parameters.Select(p=>new{name=p.Name,type=TypeText(p.Type),output=p.Output}),
            resultSets=record.ResultSets,
            ok=record.Error is null,
            error=record.Error is null?null:new{type=record.Error.GetType().Name,number=(record.Error as SqlQueryException)?.Number,message=Mask(record.Error.Message)},
            ms=Math.Round(record.Elapsed.TotalMilliseconds,3)
        });
        lock(this.gate){if(!this.disposed){this.writer.WriteLine(line);}}
    }
    public void Dispose(){lock(this.gate){if(this.disposed){return;}this.disposed=true;this.writer.Dispose();}}
    public static string? Mask(string? sql)=>sql is null?null:Secret().Replace(sql,"$1'***'");
    private static string TypeText(SqlType type){
        var name=type.Name;
        if(type.Precision is not null){return name+"("+type.Precision.Value.ToString(CultureInfo.InvariantCulture)+","+(type.Scale??0).ToString(CultureInfo.InvariantCulture)+")";}
        if(type.Length is not null){return name+"("+(type.Length.Value<0?"max":type.Length.Value.ToString(CultureInfo.InvariantCulture))+")";}
        return name;
    }
    [GeneratedRegex(@"(\b(?:PASSWORD|SECRET)\s*=\s*)N?'(?:[^']|'')*'",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex Secret();
}

internal sealed record TdsRequestRecord(DateTime Utc,long Session,string Database,string Kind,string? Procedure,string? Sql,string? Declarations,IReadOnlyList<TdsRpcParameter> Parameters,int ResultSets,Exception? Error,TimeSpan Elapsed);
