namespace Verification.Core;

public sealed class VerificationRunner(IVerificationRepository repository,IEnumerable<IVerificationAdapter> adapters){
    private readonly IVerificationRepository repository=repository;
    private readonly Dictionary<string,IVerificationAdapter> adapters=adapters.ToDictionary(static x=>x.Name,StringComparer.Ordinal);
    public async Task<VerificationRun> RunAsync(VerificationPlan plan,string artifactRoot,IProgress<VerificationProgress>? progress=null,string? parentId=null,CancellationToken cancellationToken=default){
        if(plan.Cases.Length==0){throw new InvalidDataException("計画にケースがありません。");}
        if(plan.BudgetSeconds<0){throw new InvalidDataException("時間枠を負にはできません。");}
        if(plan.Version!=1){throw new InvalidDataException("未対応の計画バージョンです。");}
        if(plan.Cases.Select(static x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=plan.Cases.Length){throw new InvalidDataException("ケースIDが重複しています。");}
        foreach(var item in plan.Cases){
            if(!this.adapters.ContainsKey(item.Adapter)){throw new InvalidDataException("未登録のアダプター: "+item.Adapter);}
        }
        var run=new VerificationRun(Guid.NewGuid().ToString("N"),parentId,DateTimeOffset.UtcNow,CaseStatus.Running,plan);
        var root=Path.Combine(artifactRoot,run.Id);
        Directory.CreateDirectory(root);
        using var lease=new FileStream(Path.Combine(root,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        this.repository.CreateRun(run);
        var overall=CaseStatus.Passed;
        foreach(var item in plan.Cases){
            this.repository.SaveResult(run.Id,new(item.Id,CaseStatus.NotRun,item.Conditions,[],null,null,"未実行"));
        }
        try{
            foreach(var item in plan.Cases){
                cancellationToken.ThrowIfCancellationRequested();
                await this.adapters[item.Adapter].PrepareAsync(item,plan.RepositoryRoot,root,cancellationToken).ConfigureAwait(false);
            }
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if(plan.BudgetSeconds>0){budget.CancelAfter(TimeSpan.FromSeconds(plan.BudgetSeconds));}
            for(var index=0;index<plan.Cases.Length;index++){
                var item=plan.Cases[index];
                if(budget.IsCancellationRequested){overall=CaseStatus.Cancelled;break;}
                var caseRoot=Path.Combine(root,index.ToString("D4",System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(caseRoot);
                this.repository.SaveResult(run.Id,new(item.Id,CaseStatus.Running,item.Conditions,[],null,null,null));
                progress?.Report(new(run.Id,item.Id,CaseStatus.Running,"実行中"));
                CaseResult result;
                try{
                    result=await this.adapters[item.Adapter].ExecuteAsync(item,plan.RepositoryRoot,caseRoot,budget.Token).ConfigureAwait(false);
                }catch(OperationCanceledException) when(budget.IsCancellationRequested){
                    result=new(item.Id,CaseStatus.Cancelled,item.Conditions,[],null,null,"キャンセルまたは時間枠の終了により中断しました。");
                }catch(Exception error){
                    result=new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,error.ToString());
                }
                if(result.CaseId!=item.Id||result.Conditions!=item.Conditions){
                    result=new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"アダプターの返却したケースまたは比較条件が計画と異なります。");
                }
                var artifacts=new List<ArtifactReference>();
                var files=Directory.EnumerateFiles(caseRoot,"*",SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateDirectories(root,"build-*").SelectMany(static directory=>Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories)));
                string? buildFingerprint=null;
                foreach(var path in files.Order(StringComparer.Ordinal)){
                    using var input=File.OpenRead(path);
                    var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
                    artifacts.Add(new(Path.GetRelativePath(artifactRoot,path).Replace('\\','/'),hash,input.Length));
                    if(Path.GetFileName(path)=="binaries.sha256"){buildFingerprint=hash;}
                }
                result=result with{Artifacts=artifacts.ToArray(),BuildFingerprint=buildFingerprint};
                this.repository.SaveResult(run.Id,result);
                progress?.Report(new(run.Id,item.Id,result.Status,result.Diagnostic??result.Status.ToString()));
                if(result.Status==CaseStatus.Cancelled){overall=CaseStatus.Cancelled;break;}
                if(result.Status!=CaseStatus.Passed){overall=CaseStatus.Failed;}
            }
        }catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){
            overall=CaseStatus.Cancelled;
        }catch(Exception error){
            overall=CaseStatus.Failed;
            await File.WriteAllTextAsync(Path.Combine(root,"preparation-error.txt"),error.ToString(),CancellationToken.None).ConfigureAwait(false);
        }finally{
            this.repository.CompleteRun(run.Id,overall);
        }
        return run with{Status=overall};
    }
}
