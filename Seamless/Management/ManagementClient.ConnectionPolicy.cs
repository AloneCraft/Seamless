namespace Seamless.Management;

public sealed partial class ManagementClient{
    private bool disposed;

    // 接続は操作ごとに開いて閉じる。接続を保つ・使わないとき解放するといった切替は持たない(Managerはブラウザーからだけ使う)。
    private Task<Stream> OpenForRequestAsync(CancellationToken cancellationToken){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed),this);
        cancellationToken.ThrowIfCancellationRequested();
        return this.OpenAsync(cancellationToken);
    }

    public void Dispose()=>Volatile.Write(ref this.disposed,true);
}
