using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management;

/// <summary>
/// 外部 IDE（VS Code 等）のファイル保存を 300ms デバウンス付き FileSystemWatcher で監視し、
/// Manager 自身の書き込み再検知による循環同期ループを SHA-256 ハッシュ照合で原理的に防止する。
/// </summary>
public sealed class SourceWatcher : IDisposable{
    public const int DefaultDebounceMilliseconds = 300;
    private readonly FileSystemWatcher watcher;
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, string> selfWrittenHashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<IReadOnlyList<string>, Task> onChanged;
    private readonly int debounceMilliseconds;
    private readonly TimeProvider timeProvider;
    private readonly HashSet<string> pendingChangedFiles = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? debounceCts;
    private bool disposed;

    public string WatchedDirectory { get; }
    public int DebounceMilliseconds => this.debounceMilliseconds;
    public long IgnoredSelfWriteCount { get; private set; }
    public long ExternalChangeTriggerCount { get; private set; }

    public SourceWatcher(string directoryPath, Func<IReadOnlyList<string>, Task> onChanged, int debounceMilliseconds = DefaultDebounceMilliseconds)
        :this(directoryPath,onChanged,debounceMilliseconds,TimeProvider.System){}
    internal SourceWatcher(string directoryPath,Func<IReadOnlyList<string>,Task> onChanged,int debounceMilliseconds,TimeProvider timeProvider){
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(onChanged);
        this.WatchedDirectory = Path.GetFullPath(directoryPath);
        this.onChanged = onChanged;
        this.debounceMilliseconds = debounceMilliseconds > 0 ? debounceMilliseconds : DefaultDebounceMilliseconds;
        this.timeProvider=timeProvider;

        this.watcher = new FileSystemWatcher(this.WatchedDirectory){
            Filter = "*.cs",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        this.watcher.Changed += this.OnFileSystemEvent;
        this.watcher.Created += this.OnFileSystemEvent;
        this.watcher.Deleted += this.OnFileSystemEvent;
        this.watcher.Renamed += this.OnRenamedEvent;
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>Manager 自前のファイル書き込み内容の SHA-256 を登録し、直後のウォッチャー通知を除外する。</summary>
    public void RecordSelfWritten(string path, string? text){
        if(string.IsNullOrEmpty(path)){return;}
        var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(this.WatchedDirectory, path));
        if(text is null){
            this.selfWrittenHashes[full] = "__DELETED__";
        }else{
            this.selfWrittenHashes[full] = ComputeSha256(text);
        }
    }

    public static string ComputeSha256(string text){
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e){
        this.ScheduleDebounce(e.FullPath);
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e){
        this.ScheduleDebounce(e.OldFullPath);
        this.ScheduleDebounce(e.FullPath);
    }

    private Task ScheduleDebounce(string fullPath){
        lock(this.gate){
            if(this.disposed){return Task.CompletedTask;}
            this.pendingChangedFiles.Add(fullPath);
            this.debounceCts?.Cancel();
            this.debounceCts?.Dispose();
            this.debounceCts = new CancellationTokenSource();
            var token = this.debounceCts.Token;

            return Task.Delay(TimeSpan.FromMilliseconds(this.debounceMilliseconds),this.timeProvider,token).ContinueWith(async t => {
                if(!t.IsCanceled){
                    await this.ProcessPendingFilesAsync(token);
                }
            }, TaskScheduler.Default).Unwrap();
        }
    }

    private async Task ProcessPendingFilesAsync(CancellationToken token){
        List<string> externalFiles = [];
        lock(this.gate){
            // 待機完了後に新しい保存が届いた場合も、古い処理による早い通知を防ぐ。
            if(this.disposed || token.IsCancellationRequested || this.pendingChangedFiles.Count == 0){return;}
            foreach(var full in this.pendingChangedFiles){
                if(this.selfWrittenHashes.TryGetValue(full, out var recordedHash)){
                    string currentHash;
                    if(!File.Exists(full)){
                        currentHash = "__DELETED__";
                    }else{
                        try{
                            currentHash = ComputeSha256(File.ReadAllText(full));
                        }catch{
                            // 読取競合時は次回イベントに委ねる
                            continue;
                        }
                    }
                    if(string.Equals(recordedHash, currentHash, StringComparison.OrdinalIgnoreCase)){
                        // 自前書き込みと一致したため循環同期ループを抑止
                        this.IgnoredSelfWriteCount++;
                        continue;
                    }
                    // ハッシュが異なる場合は自前書き込み後に外部変更が重なったと判定
                    this.selfWrittenHashes.TryRemove(full, out _);
                }
                externalFiles.Add(full);
            }
            this.pendingChangedFiles.Clear();
        }

        if(externalFiles.Count > 0){
            this.ExternalChangeTriggerCount++;
            await this.onChanged(externalFiles);
        }
    }

    public void Dispose(){
        lock(this.gate){
            if(this.disposed){return;}
            this.disposed = true;
            this.debounceCts?.Cancel();
            this.debounceCts?.Dispose();
            this.debounceCts = null;
            this.watcher.EnableRaisingEvents = false;
            this.watcher.Dispose();
            this.pendingChangedFiles.Clear();
            this.selfWrittenHashes.Clear();
        }
    }
}
