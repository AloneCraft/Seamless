namespace Benchmark.Infrastructure.Tests;

public sealed class BenchmarkRepository{
    [Fact]
    public void PublishProfileRequiresIndependentImprovement(){
        var folder=Path.Combine(Path.GetTempPath(),"profile-evidence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try{
            var repository=new Infrastructure.BenchmarkRepository(new(Path.Combine(folder,"history.db")));
            var environment=EnvironmentSnapshot.Capture("scalable-content-v2");
            var minimum=Seamless.Execution.ProfileResolver.Minimum("Tsavorite");
            var candidate=minimum with{IndexBytes=4096};
            var growth=System.Text.Json.JsonSerializer.Serialize(new Seamless.Execution.Growth.Descriptor("doubling",1,"{}"));
            var application="Scalable";var implementation="Native";var workload="FilterAggregate";
            Measurement Save(string id,string input,Seamless.Execution.ProfileSettings settings,double time,Seamless.Execution.ProfileSettings? initial=null){
                repository.SaveRun(new(id,application,"Passed","{}",System.Text.Json.JsonSerializer.Serialize(environment),"build",DateTimeOffset.UtcNow));
                var conditions=new Conditions(workload,input,4096,"System.Int32|System.Int32","Tsavorite","Temporary",implementation,"Native",System.Text.Json.JsonSerializer.Serialize(new ProfileParameters(null,settings,Initial:initial,Evidence:Seamless.Execution.ProfileEvidence.Current)),growth,environment.Key,"scalable-content-v2");
                var measurement=Measurement.Create(id,id,conditions,"digest-"+input,Enumerable.Repeat(new Sample(time,1,0),7).ToArray());repository.SaveMeasurement(measurement);return measurement;
            }
            var before=Save("control","training-input",minimum,100);var training=Save("candidate","training-input",candidate,80);
            var validation=Save("validation","validation-input",candidate,80);var beforeValidation=Save("validation-control","validation-input",minimum,100);
            Assert.Throws<InvalidOperationException>(()=>repository.PublishProfile("not-yet",training.ID,validation.ID));
            repository.Compare(before.ID,training.ID,ComparisonMode.Experiment);repository.Compare(beforeValidation.ID,validation.ID,ComparisonMode.Experiment);
            var profile=repository.PublishProfile("validated",training.ID,validation.ID);
            Assert.Equal(4,profile.EvidenceRunIDs.Length);Assert.Equal(4096,profile.Settings.IndexBytes);Assert.Equal("doubling",profile.Growth!.ID);
            Assert.NotNull(repository.GetProfiles().Find(new("Tsavorite",Seamless.Execution.StoreKind.Temporary,"System.Int32|System.Int32",4096,"FilterAggregate",environment.ProfileEnvironment)));
            application="Dynamic";implementation="FLinq";workload="query-hash";
            var grown=minimum with{IndexBytes=16384};var faster=grown with{IndexBytes=32768};
            var dynamicBefore=Save("dynamic-before","dynamic-training",grown,100,minimum);
            var dynamicCandidate=Save("dynamic-candidate","dynamic-training",faster,80,candidate);
            var dynamicValidationBefore=Save("dynamic-before-validation","dynamic-validation",grown,100,minimum);
            var dynamicValidation=Save("dynamic-validation","dynamic-validation",faster,80,candidate);
            repository.Compare(dynamicBefore.ID,dynamicCandidate.ID,ComparisonMode.Experiment);repository.Compare(dynamicValidationBefore.ID,dynamicValidation.ID,ComparisonMode.Experiment);
            var dynamicProfile=repository.PublishProfile("dynamic",dynamicCandidate.ID,dynamicValidation.ID);
            Assert.Equal("query-hash",dynamicProfile.Conditions.Workload);Assert.Equal(4096,dynamicProfile.Settings.IndexBytes);
            growth=System.Text.Json.JsonSerializer.Serialize(new Seamless.Execution.Growth.Descriptor("fixed",1,"{}"));
            application="Scalable";implementation="Native";workload="FilterAggregate";
            var fixedControl=Save("fixed-control","fixed-training",minimum,100);
            var fixedCandidate=Save("fixed-candidate","fixed-training",candidate,80);
            var fixedValidationControl=Save("fixed-control-validation","fixed-validation",minimum,100);
            var fixedValidation=Save("fixed-validation","fixed-validation",candidate,80);
            repository.Compare(fixedControl.ID,fixedCandidate.ID,ComparisonMode.Experiment);repository.Compare(fixedValidationControl.ID,fixedValidation.ID,ComparisonMode.Experiment);
            Assert.Throws<InvalidOperationException>(()=>repository.PublishProfile("fixed-is-not-the-default",fixedCandidate.ID,fixedValidation.ID));
        }finally{Directory.Delete(folder,true);}
    }
    [Fact]
    public void SaveCompareAndPinBaseline(){
        var folder=Path.Combine(Path.GetTempPath(),"seamless-common-history",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try{
            var database=new Infrastructure.BenchmarkDatabase(Path.Combine(folder,"history.db"));
            var repository=new Infrastructure.BenchmarkRepository(database);
            Assert.Equal(4,database.ReadVersion());
            var at=DateTimeOffset.UtcNow;
            repository.SaveRun(new("one","Dynamic","Passed","{}","{}","revision1",at));
            repository.SaveRun(new("two","Scalable","Passed","{}","{}","revision2",at));
            var conditions=new Conditions("lookup","same-input",100,"Int32","Tsavorite","Temporary","FLinq","All","{}","{}","same-environment");
            var first=Measurement.Create("a","one",conditions,"same-result",Enumerable.Repeat(new Sample(100,1,4),7).ToArray());
            var second=first with{ID="b",RunID="two",Samples=Enumerable.Repeat(new Sample(90,1,0),7).ToArray()};
            repository.SaveMeasurement(first);repository.SaveMeasurement(second);
            repository.SetBaseline("a");
            Assert.Equal("a",repository.GetBaseline(conditions));
            Assert.Equal(ComparisonStatus.Passed,repository.Compare("a","b").Status);
            Assert.Single(repository.GetComparisons("two"));
            Assert.Equal(2,repository.GetRuns().Count);
            Assert.Equal(7,repository.GetMeasurement("a")!.Samples.Length);
            Assert.Equal("a",repository.GetBaseline(conditions));
            repository.SaveRun(repository.GetRun("two")! with{Status="Canceled"});
            var stopped=repository.Compare("a","b");Assert.Equal(ComparisonStatus.Incomplete,stopped.Status);Assert.Null(stopped.TimeRatio);
            Assert.Throws<InvalidOperationException>(()=>repository.SetBaseline("b"));
        }finally{Directory.Delete(folder,true);}
    }
}
