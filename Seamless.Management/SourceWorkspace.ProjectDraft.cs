namespace Seamless.Management;

public sealed partial class SourceWorkspace{
    internal void AlignProjectPublication(DefinitionVersion published){
        using var lease=this.Lock();this.AssertCurrent();
        if(published.Definition.Id!=this.Current.Definition.Id||published.Definition.Database!=this.Current.Definition.Database){
            throw new ManagementException("Conflict","作成元DBと公開定義が一致しません。");
        }
        if(published.Version==this.Current.BaseVersion){return;}
        var saved=this.State.SavedDraftDefinition;
        var latest=published.Definition.Freeze() with{Migration=null};
        if(published.Version!=this.Current.BaseVersion+1||saved is null||saved.Hash()!=this.State.SavedDraftHash||
            (saved with{Migration=null}).Hash()!=latest.Hash()){
            throw new ManagementException("Conflict","公開版が進んでいます。同期基準と公開版を確認してください。");
        }
        if(this.ActiveConflict() is not null||this.State.PendingTablePublication is not null){
            throw new ManagementException("Conflict","競合または公開操作の結果を確認してください。");
        }
        var current=MergeDefinitions(saved,this.Current.Definition,latest);
        var external=latest.Source;
        var undoConflict=this.State.UndoConflict;
        if(this.State.ProjectDraftBaseline is{} baseline&&baseline.DefinitionHash==this.State.SavedDraftHash){
            try{external=Merge(baseline.ExternalSource,latest.Source,this.State.WithoutManager);}
            catch(ManagementException error)when(error.Code=="Conflict"){undoConflict=true;}
        }else if(!Same(this.Current.Definition.Source,saved.Source)){undoConflict=true;}
        this.Commit(this.State with{
            Current=this.Current with{Definition=current,BaseVersion=published.Version},Published=latest.Source,
            SavedDraftDefinition=latest,SavedDraftHash=latest.Hash(),WithoutManager=external,UndoConflict=undoConflict,
            ManagerMigrationTracked=true,WithoutManagerMigration=null,
            ProjectDraftBaseline=new(latest.Hash(),external.Freeze())
        },[]);
    }
}
