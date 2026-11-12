using Seamless.Database.Gateway.Hosting;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(GatewayCommitPhase.BeforeCandidateBuild,false)]
    [InlineData(GatewayCommitPhase.CandidateBuilt,false)]
    [InlineData(GatewayCommitPhase.CandidateCopied,false)]
    [InlineData(GatewayCommitPhase.GenerationSealed,false)]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.PreparationDurable,false)]
    [InlineData(GatewayCommitPhase.WalDurable,true)]
    [InlineData(GatewayCommitPhase.DatabaseApplied,true)]
    [InlineData(GatewayCommitPhase.BeforePublication,true)]
    [InlineData(GatewayCommitPhase.AfterPublication,true)]
    [InlineData(GatewayCommitPhase.AllDatabasesApplied,true)]
    [InlineData(GatewayCommitPhase.DatabaseCheckpointed,true)]
    [InlineData(GatewayCommitPhase.BeforeCommonCheckpointPublication,true)]
    [InlineData(GatewayCommitPhase.AfterCommonCheckpointPublication,true)]
    public Task VersionedProcessKillNeverPublishesOnlyOneDatabaseOrSchema(GatewayCommitPhase phase,bool committed)=>GatewayProcessTermination.VerifyProcessKill(phase,committed,true);
}
