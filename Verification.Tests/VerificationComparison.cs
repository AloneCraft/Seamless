using Verification.Core;
namespace Verification.Tests;
public sealed class VerificationComparison{
    [Fact]public void Compare(){
        var conditions=new ComparisonConditions("case-v1","input","binary-v1","codec-v1","measurement-v1","machine","cpu","Release");
        var baseline=new CaseResult("case",CaseStatus.Passed,conditions,[100,100,100,100,100],0,"digest",null);
        var candidate=baseline with{ElapsedNanoseconds=[120,120,120,120,120]};
        Assert.Equal(ComparisonStatus.Regressed,Verification.Core.VerificationComparison.Compare(baseline,candidate).Status);
        Assert.Equal(ComparisonStatus.Incompatible,Verification.Core.VerificationComparison.Compare(baseline,candidate with{Conditions=conditions with{Semantics="dotnet"}}).Status);
        Assert.Equal(ComparisonStatus.Incomplete,Verification.Core.VerificationComparison.Compare(baseline,candidate with{Status=CaseStatus.Cancelled}).Status);
        Assert.Equal(ComparisonStatus.Inconclusive,Verification.Core.VerificationComparison.Compare(baseline,candidate with{ElapsedNanoseconds=[100]}).Status);
        Assert.Equal(ComparisonStatus.Inconclusive,Verification.Core.VerificationComparison.Compare(baseline,candidate with{ElapsedNanoseconds=[double.NaN,100,100,100,100]}).Status);
        Assert.Equal(ComparisonStatus.CorrectnessFailure,Verification.Core.VerificationComparison.Compare(baseline,candidate with{ResultDigest="different"}).Status);
        Assert.Equal(ComparisonStatus.Passed,Verification.Core.VerificationComparison.Compare(baseline,baseline).Status);
    }
    [Fact]public void CapacitySweep_UsesMaximumCountAndFailureKind(){
        var conditions=new ComparisonConditions("case-v1","input","capacity","codec","capacity-sweep-v1","machine","cpu","Release");
        var baseline=new CaseResult("case",CaseStatus.Passed,conditions,[],null,null,null,Metrics:new Dictionary<string,double>{{"maximum-count",100},{"failure-kind",1}});
        Assert.Equal(ComparisonStatus.Passed,Verification.Core.VerificationComparison.Compare(baseline,baseline with{Metrics=new Dictionary<string,double>{{"maximum-count",110},{"failure-kind",1}}}).Status);
        Assert.Equal(ComparisonStatus.Regressed,Verification.Core.VerificationComparison.Compare(baseline,baseline with{Metrics=new Dictionary<string,double>{{"maximum-count",90},{"failure-kind",1}}}).Status);
        Assert.Equal(ComparisonStatus.Inconclusive,Verification.Core.VerificationComparison.Compare(baseline,baseline with{Metrics=new Dictionary<string,double>{{"maximum-count",100},{"failure-kind",2}}}).Status);
        Assert.Equal(ComparisonStatus.Incomplete,Verification.Core.VerificationComparison.Compare(baseline,baseline with{Metrics=null}).Status);
    }
}
