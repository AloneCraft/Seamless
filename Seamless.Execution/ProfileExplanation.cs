namespace Seamless.Execution;

public sealed record ProfileExplanation(Profile Requested,Profile Applied,string? ProfileID,int? ProfileVersion,
    ProfileSettings Settings,Growth.Descriptor Growth,IReadOnlyList<string> EvidenceRunIDs,string Reason,
    IReadOnlyList<string> UnresolvedConditions,ProfileEvidence RequiredEvidence){
    public ProfileRequest? Conditions{get;init;}
    public IReadOnlyList<string> MissingConditions{get;init;}=Array.Empty<string>();
    public static ProfileExplanation Create(ProfileResolution resolution,Growth.Policy? explicitGrowth=null,ProfileEvidence? requiredEvidence=null)=>new(
        resolution.Requested,resolution.Applied,resolution.ProfileID,resolution.ProfileVersion,resolution.Settings,
        explicitGrowth?.Descriptor??resolution.Growth??new Growth.Policy<Growth.Doubling>().Descriptor,
        Array.AsReadOnly((string[])resolution.EvidenceRunIDs.Clone()),resolution.Reason,
        Array.AsReadOnly(new[]{"Growth decisions depend on runtime usage.","Spill and join/sort paths depend on runtime values and available shared budget."}),
        requiredEvidence??ProfileEvidence.Current);
}
