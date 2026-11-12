namespace Seamless.Management.Server;

/// <summary>同一プロセスのStopwatch raw tickでOSの実観測を返す。呼出しは専用collectorから直列化する。</summary>
public interface IResourceHeatProvider:IDisposable{
    ResourceHeatReading Read(CancellationToken cancellationToken=default);
}
