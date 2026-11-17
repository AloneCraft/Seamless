using Seamless.Transactions;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    // 接続/保存点/世代leaseはGatewayが所有し、WAL受付・耐久化・反映は共通Coordinatorへ委譲する。
    // 行の全件配列を作らず既存prepared journalへstreamし、DDL/Native metadataも同じ確定記録に入る。
    private sealed class CommonCommitChanges(Func<IEnumerable<WalEntry>> rows,Action<long> apply):ICommittedTransactionChanges{
        public IReadOnlyList<WalEntry> Encode()=>throw new NotSupportedException("Gateway commit must stream its prepared rows.");
        public IEnumerable<WalEntry> EnumerateEncoded()=>rows();
        public void Apply()=>throw new InvalidOperationException("Gateway application requires the durable common LSN.");
        public void Apply(long durableLsn){if(durableLsn<=0){throw new InvalidOperationException("A durable common commit is required.");}apply(durableLsn);}
        public void Dispose(){}
    }
}
