using System.Diagnostics;
namespace Benchmark.Tpc.Server;
/// <summary>子プロセス。標準出力と標準エラーをログファイルに書き、行を後から待てるようにする。</summary>
public sealed class ChildProcess:IDisposable{
    private readonly Process process;
    private readonly StreamWriter log;
    private readonly object gate=new();
    private readonly List<string> lines=[];
    public ChildProcess(string file,IEnumerable<string> arguments,string logPath,string? workingDirectory=null){
        var start=new ProcessStartInfo(file){
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            CreateNoWindow=true,
            StandardOutputEncoding=System.Text.Encoding.UTF8,
            StandardErrorEncoding=System.Text.Encoding.UTF8,
        };
        if(workingDirectory is not null){
            start.WorkingDirectory=workingDirectory;
        }
        foreach(var argument in arguments){
            start.ArgumentList.Add(argument);
        }
        this.log=new StreamWriter(logPath,false,new System.Text.UTF8Encoding(false)){AutoFlush=true};
        this.process=new Process{StartInfo=start};
        this.process.OutputDataReceived+=(_,data)=>this.OnLine(data.Data);
        this.process.ErrorDataReceived+=(_,data)=>this.OnLine(data.Data);
        this.process.Start();
        this.process.BeginOutputReadLine();
        this.process.BeginErrorReadLine();
    }
    public bool HasExited=>this.process.HasExited;
    public int ExitCode=>this.process.ExitCode;
    public int Id=>this.process.Id;
    /// <summary>このプロセスの物理メモリ使用量(バイト)。取れなければ 0。</summary>
    public long WorkingSetBytes{
        get{
            try{
                this.process.Refresh();
                return this.process.WorkingSet64;
            }catch(InvalidOperationException){
                return 0;
            }
        }
    }
    private void OnLine(string? text){
        if(text is null){
            return;
        }
        lock(this.gate){
            this.lines.Add(text);
            this.log.WriteLine(text);
        }
    }
    /// <summary>これまでに受けた行のうち、<paramref name="marker"/> を含むものをすべて。</summary>
    public IReadOnlyList<string> FindLines(string marker){
        var found=new List<string>();
        lock(this.gate){
            foreach(var line in this.lines){
                if(line.Contains(marker,StringComparison.Ordinal)){
                    found.Add(line);
                }
            }
        }
        return found;
    }
    /// <summary>これまでに受けた行のうち、<paramref name="marker"/> を含む最後の行。</summary>
    public string? FindLine(string marker){
        lock(this.gate){
            for(var index=this.lines.Count-1;index>=0;index--){
                if(this.lines[index].Contains(marker,StringComparison.Ordinal)){
                    return this.lines[index];
                }
            }
        }
        return null;
    }
    /// <summary><paramref name="marker"/> を含む行が出るか、プロセスが終わるか、時間切れか、取り消されるまで待つ。</summary>
    public string? WaitForLine(string marker,TimeSpan timeout,CancellationToken cancel){
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<timeout&&!cancel.IsCancellationRequested){
            var found=this.FindLine(marker);
            if(found is not null){
                return found;
            }
            if(this.process.HasExited){
                return this.FindLine(marker);
            }
            Thread.Sleep(100);
        }
        return null;
    }
    public bool WaitForExit(TimeSpan timeout)=>this.process.WaitForExit(timeout);
    public void Kill(){
        try{
            if(!this.process.HasExited){
                this.process.Kill(true);
                this.process.WaitForExit(10_000);
            }
        }catch(InvalidOperationException){
        }
    }
    public void Dispose(){
        this.Kill();
        this.process.Dispose();
        lock(this.gate){
            this.log.Dispose();
        }
    }
}
