namespace Benchmark.Infrastructure;

/// <summary>両アプリの測定が重ならないようにする。履歴の閲覧・書き込みは妨げない。</summary>
public sealed class MeasurementLease:IDisposable{
    private readonly FileStream file;
    private MeasurementLease(FileStream file){this.file=file;}
    public static MeasurementLease Acquire(CancellationToken cancellationToken=default){
        var folder=Path.Combine(Path.GetTempPath(),"Seamless");Directory.CreateDirectory(folder);
        while(true){
            cancellationToken.ThrowIfCancellationRequested();
            try{return new(new FileStream(Path.Combine(folder,"benchmark-measurement.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));}
            catch(IOException exception) when((exception.HResult&0xffff) is 32 or 33){
                if(cancellationToken.WaitHandle.WaitOne(100)){cancellationToken.ThrowIfCancellationRequested();}
            }
        }
    }
    public void Dispose()=>this.file.Dispose();
}
