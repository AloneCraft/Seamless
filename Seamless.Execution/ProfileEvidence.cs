namespace Seamless.Execution;

/// <summary>測定DBを必要としない、候補の証拠と利用側の互換条件。</summary>
public sealed record ProfileEvidence(string LibraryVersion,string CodecVersion,string MeasurementVersion,string ComparisonPolicyVersion){
    public static ProfileEvidence Current{get;}=new("execution-budget-v2:"+typeof(ProfileEvidence).Assembly.ManifestModule.ModuleVersionId,"1","scalable-content-v2","noise-v1");
}
