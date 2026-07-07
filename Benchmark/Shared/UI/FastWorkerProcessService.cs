using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class FastWorkerProcessService:IBenchmarkJobRunner,IAsyncDisposable {
    private readonly SemaphoreSlim sessionGate = new(1,1);
    private FastWorkerSession? session;
    private string runRoot = string.Empty;

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        var current = await this.GetSessionAsync(runRoot,cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeoutSource.Token);
        try {
            return await current.RunAsync(job,logicalCpu,linkedSource.Token);
        } catch(OperationCanceledException) {
            await this.ResetSessionAsync(current);
            if(timeoutSource.IsCancellationRequested&&!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException($"Benchmark job timed out after {timeout}.");
            }

            throw;
        } catch {
            await this.ResetSessionAsync(current);
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await this.sessionGate.WaitAsync();
        try {
            if(this.session is not null) {
                await this.session.DisposeAsync();
                this.session=null;
            }
        } finally {
            this.sessionGate.Release();
            this.sessionGate.Dispose();
        }
    }

    private async Task<FastWorkerSession> GetSessionAsync(string runRoot,CancellationToken cancellationToken) {
        await this.sessionGate.WaitAsync(cancellationToken);
        try {
            if(this.session is not null&&string.Equals(this.runRoot,runRoot,StringComparison.OrdinalIgnoreCase)) {
                return this.session;
            }

            if(this.session is not null) {
                await this.session.DisposeAsync();
                this.session=null;
            }

            this.runRoot=runRoot;
            this.session=await FastWorkerSession.StartAsync(runRoot,cancellationToken);
            return this.session;
        } finally {
            this.sessionGate.Release();
        }
    }

    private async Task ResetSessionAsync(FastWorkerSession current) {
        await this.sessionGate.WaitAsync();
        try {
            if(ReferenceEquals(this.session,current)) {
                this.session=null;
                await current.DisposeAsync();
            }
        } finally {
            this.sessionGate.Release();
        }
    }
}
