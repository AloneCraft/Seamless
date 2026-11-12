namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    public QueryPlanResult QueryPlans(UserAccount user,QueryPlanRequest request){
        if(string.IsNullOrWhiteSpace(request.DefinitionId)||string.IsNullOrWhiteSpace(request.ObjectId)){
            throw new ManagementException("InvalidRequest","DB定義と問い合わせを指定してください。");
        }
        DefinitionVersion version;
        if(request.HistoricalVersion is{} historical){
            if(historical<1||request.DraftRevision is not null||request.BundleHash is not null){throw new ManagementException("InvalidRequest","過去の公開版と下書きは同時に指定できません。");}
            version=this.Version(user,request.DefinitionId,historical);
        }else{version=this.GetDefinition(user,request.DefinitionId);}
        if(request.ExpectedVersion is{} expected&&version.Version!=expected){throw new ManagementException("SchemaMismatch","公開版が変わりました。対象を開き直してください。");}
        var definition=version.Definition;
        BuildResult build;
        if(request.DraftRevision is{} revision){
            var draft=this.GetDraft(user,request.DefinitionId);
            if(draft.Revision!=revision){throw new ManagementException("Conflict","下書きが変わりました。保存状態を確認してください。");}
            definition=draft.Definition;
            build=this.Build(user,new(request.DefinitionId,revision){BundleHash=request.BundleHash});
        }else{
            build=this.runtime.PublishedPlans(version);
        }
        var selection=DatabaseDesigner.SelectQueryPlans(definition,request.ObjectId,build.Plans);
        if(!request.IncludeGeneratedCode){selection=selection with{Plans=selection.Plans.Select(plan=>plan with{GeneratedCode=null,ProfiledGeneratedCode=null}).ToArray()};}
        SourceSnapshot? historicalSource=null;
        if(request.HistoricalVersion is not null){historicalSource=definition.Source;}
        return new(definition.Id,version.Version,build.ArtifactHash,build.SourceHash,request.DraftRevision is not null,selection,request.DraftRevision){HistoricalSource=historicalSource};
    }
}
