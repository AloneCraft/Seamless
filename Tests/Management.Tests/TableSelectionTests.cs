using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class TableSelectionTests{
    [Fact]public void NewTablesFollowCrossSchemaCyclesWithoutSelectingIndependentChanges(){
        var live=Designer.Empty("selection","Example");
        var draft=Add(live,"Orders","Sales","Accounts.Customers");
        draft=Add(draft,"Customers","Accounts","Audit.Events");
        draft=Add(draft,"Events","Audit","Sales.Orders");
        draft=Add(draft,"Notes","Sales");

        Assert.Equal(new[]{new TableSelection(null,"Sales.Orders"),new(null,"Accounts.Customers"),new(null,"Audit.Events")},Designer.SelectTables(live,draft,"Sales.Orders"));
    }

    [Fact]public void ACompatibleLiveKeyDoesNotPullInAnUnrelatedColumnEdit(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales") with{Migration=null};
        var customer=Designer.Read(live,"Sales.Customers");
        var draft=Designer.Apply(live,customer with{Columns=[..customer.Columns,new("Label","string",false,null,"\"\"")]});
        draft=Add(draft,"Orders","Sales","Sales.Customers");
        Assert.Equal(new TableSelection(null,"Sales.Orders"),Assert.Single(Designer.SelectTables(live,draft,"Sales.Orders")));
    }

    [Fact]public void AParentRenameRequiresTheChildReferenceChange(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales","Sales.Customers") with{Migration=null};
        var draft=Designer.Apply(live,Designer.Read(live,"Sales.Customers") with{Name="Clients"});
        Assert.Equal(new[]{new TableSelection("Sales.Customers","Sales.Clients"),new("Sales.Orders","Sales.Orders")},Designer.SelectTables(live,draft,"Sales.Clients"));
    }

    [Fact]public void ChangingTheKeyTypeRequiresTheMatchingChangesFromEitherEnd(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales","Sales.Customers") with{Migration=null};
        var draft=ChangeColumnType(live,"Sales.Customers","Id","long");
        draft=ChangeColumnType(draft,"Sales.Orders","ParentId","long?");

        var customers=new TableSelection("Sales.Customers","Sales.Customers");
        var orders=new TableSelection("Sales.Orders","Sales.Orders");
        Assert.Equal(new[]{customers,orders},Designer.SelectTables(live,draft,"Sales.Customers"));
        Assert.Equal(new[]{orders,customers},Designer.SelectTables(live,draft,"Sales.Orders"));
    }

    [Fact]public void DeletingTheParentRequiresRemovingTheDependentReference(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales","Sales.Customers") with{Migration=null};
        var draft=Designer.Remove(live,"Sales.Customers");
        Assert.Equal("ForeignKeyDefinition",Assert.Throws<ManagementException>(()=>Designer.SelectTables(live,draft,"Sales.Customers")).Code);
        draft=Designer.Remove(draft,"Sales.Orders");
        Assert.Equal(new[]{new TableSelection("Sales.Customers",null),new("Sales.Orders",null)},Designer.SelectTables(live,draft,"Sales.Customers"));
    }

    [Fact]public void ANewTableCanReferenceItsOwnKey(){
        var live=Designer.Empty("selection","Example");
        var draft=Add(live,"Nodes","Graph","Graph.Nodes");
        Assert.Equal(new TableSelection(null,"Graph.Nodes"),Assert.Single(Designer.SelectTables(live,draft,"Graph.Nodes")));
    }

    [Fact]public void CandidateAddsOnlyTheSelectedCycleAndKeepsTheFullDraft(){
        var live=Designer.Empty("selection","Example");
        var draft=Add(live,"Orders","Sales","Accounts.Customers");
        draft=Add(draft,"Customers","Accounts","Sales.Orders");
        draft=Add(draft,"Notes","Sales");
        var plan=Designer.PrepareTablePublication(live,draft,"Sales.Orders");

        Assert.Equal(new[]{"Accounts.Customers","Sales.Orders"},Designer.Tables(plan.Candidate).Order());
        Assert.Equal(new[]{"Accounts.Customers","Sales.Orders"},plan.Candidate.Migration!.NewTables!.Order());
        Assert.Equal("Sales.Notes",Assert.Single(plan.RemainingDraft.Migration!.NewTables!));
        Assert.Equal(draft.Source.Hash(),plan.RemainingDraft.Source.Hash());
        AssertBuilds(plan.Candidate);
    }

    [Fact]public void CandidateKeepsUnselectedChangesInTheSameSourceFileOutOfThePublication(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales","Sales.Customers") with{Migration=null};
        var draft=Designer.Apply(live,Designer.Read(live,"Sales.Customers") with{Name="Clients"});
        draft=Designer.Apply(draft,Designer.Read(draft,"Sales.Orders") with{ForeignKeys=[]});
        draft=Add(draft,"Notes","Sales");
        var schema=live.Source.Files.Single(file=>file.Path=="Schemas/Schema_Sales.cs");
        live=live with{Source=live.Source with{Files=live.Source.Files.Select(file=>{
            if(file.Path==schema.Path){return file with{Text=file.Text+"\n// Keep this published comment\n"};}
            return file;
        }).ToArray()}};
        var plan=Designer.PrepareTablePublication(live,draft,"Sales.Clients");

        Assert.Equal(new[]{"Sales.Clients","Sales.Orders"},Designer.Tables(plan.Candidate).Order());
        Assert.Empty(Designer.Read(plan.Candidate,"Sales.Orders").ForeignKeys);
        Assert.Contains("// Keep this published comment",plan.Candidate.Source.Files.Single(file=>file.Path==schema.Path).Text);
        Assert.Equal("Sales.Notes",Assert.Single(plan.RemainingDraft.Migration!.NewTables!));
        AssertBuilds(plan.Candidate);
    }

    [Fact]public void CandidateRetainsUnselectedColumnsAndMigrationRules(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales") with{Migration=null};
        var draft=ChangeColumnType(live,"Sales.Customers","ParentId","long?");
        draft=ChangeColumnType(draft,"Sales.Orders","ParentId","short?");
        var plan=Designer.PrepareTablePublication(live,draft,"Sales.Orders");

        Assert.Equal("int?",Designer.Read(plan.Candidate,"Sales.Customers").Columns.Single(column=>column.Name=="ParentId").Type);
        Assert.Equal("short?",Designer.Read(plan.Candidate,"Sales.Orders").Columns.Single(column=>column.Name=="ParentId").Type);
        Assert.Equal("Sales.Orders",Assert.Single(plan.Candidate.Migration!.Tables).Source);
        Assert.Equal("Sales.Customers",Assert.Single(plan.RemainingDraft.Migration!.Tables).Source);
        AssertBuilds(plan.Candidate);
    }

    [Fact]public void CandidateRejectsAChangedRowTypeUsedByAnUnselectedTable(){
        var live=Add(Designer.Empty("selection","Example"),"Customers","Sales");
        live=Add(live,"Orders","Sales") with{Migration=null};
        var shared=live.Source.Files.Select(file=>{
            if(file.Path=="Schemas/Schema_Sales.cs"){
                return file with{Text=file.Text.Replace("Keys.Sales_Orders","Keys.Sales_Customers").Replace("Records.Sales_Orders","Records.Sales_Customers")};
            }
            return file;
        }).ToArray();
        live=live with{Source=live.Source with{Files=shared}};
        var draft=live with{Source=live.Source with{Files=shared.Select(file=>{
            if(file.Path=="Tables/Sales_Customers.cs"){return file with{Text=file.Text.Replace("int? ParentId","long? ParentId")};}
            return file;
        }).ToArray()}};
        var error=Assert.Throws<ManagementException>(()=>Designer.PrepareTablePublication(live,draft,"Sales.Customers"));
        Assert.Equal("SharedDeclaration",error.Code);
        Assert.Contains("Sales.Orders",error.Message);
    }

    [Fact]public void ApplyPublishesOnlyTheConfirmedTablesAndRebasesTheDraftOnDisk(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        ApplyTableRequest request;
        OperationInfo receipt;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=Admin(service);
            var live=Designer.Empty("selection","Example");
            NativeWorkflow.Publish(service,user,live);
            var changed=Add(live,"Orders","Sales","Accounts.Customers");
            changed=Add(changed,"Customers","Accounts","Sales.Orders");
            changed=Add(changed,"Notes","Sales");
            var draft=service.SaveDraft(user,new(changed,1,1));
            var key=new DraftKey(changed.Id,draft.Revision);
            var preview=service.PreviewTable(user,new(key,"Sales.Orders"));
            Assert.True(preview.Preview.CanApply,string.Join("\n",preview.Preview.Diagnostics.Select(item=>item.Message)));
            Assert.Equal(new[]{"Sales.Orders","Accounts.Customers"},preview.Tables.Select(item=>item.DraftPath));
            Assert.Equal(1,service.GetDefinition(user,changed.Id).Version);
            var apply=new ApplyRequest(changed.Id,draft.Revision,preview.Preview.Token,Guid.NewGuid());
            Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.ApplyTable(user,new(apply,"Sales.Notes"))).Code);
            Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.Apply(user,apply)).Code);
            request=new(apply,"Sales.Orders");
            service.PublicationCheckpoint=(phase,_)=>{
                if(phase=="Prepared"){throw new IOException("Test interruption before publication");}
            };
            var interrupted=service.ApplyTable(user,request with{Apply=apply with{OperationId=Guid.NewGuid()}});
            Assert.Equal(OperationState.Failed,interrupted.State);
            Assert.Equal(1,service.GetDefinition(user,changed.Id).Version);
            Assert.Equal(draft.Definition.Hash(),service.GetDraft(user,changed.Id,draft.Revision).Definition.Hash());
            service.PublicationCheckpoint=null;
            receipt=service.ApplyTable(user,request);
            Assert.True(receipt.State==OperationState.Succeeded,receipt.Error);
            Assert.Equal(2,receipt.PublishedVersion);
        }
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=Admin(service);
            Assert.Equal(receipt,service.ApplyTable(user,request));
            var live=service.GetDefinition(user,request.Apply.Id);
            var draft=service.GetDraft(user,request.Apply.Id);
            Assert.Equal(new[]{"Accounts.Customers","Sales.Orders"},Designer.Tables(live.Definition).Order());
            Assert.Equal(new[]{"Accounts.Customers","Sales.Notes","Sales.Orders"},Designer.Tables(draft.Definition).Order());
            Assert.Equal("Sales.Notes",Assert.Single(draft.Definition.Migration!.NewTables!));
            Assert.Equal(2,draft.BaseVersion);
            Assert.Equal(request.Apply.DraftRevision+1,draft.Revision);
            var next=service.PreviewTable(user,new(new(draft.Definition.Id,draft.Revision),"Sales.Notes"));
            Assert.True(next.Preview.CanApply,string.Join("\n",next.Preview.Diagnostics.Select(item=>item.Message)));
            Assert.Single(next.Tables);
        }
    }

    private static UserAccount Admin(Seamless.Management.Server.ManagementService service)=>
        service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);

    private static void AssertBuilds(Definition definition){
        Assert.DoesNotContain(new RuntimeCompiler().Compile(definition.Source).Result.Diagnostics,item=>item.Severity=="Error");
    }

    private static Definition Add(Definition definition,string name,string schema,string? target=null){
        ForeignKeyDesign[] relations=[];
        if(target is not null){relations=[new("FK_"+schema+"_"+name,target,["ParentId"],["Id"])];}
        return Designer.Apply(definition,new(name,schema,[new("Id","int",true),new("ParentId","int?",false)],relations));
    }

    private static Definition ChangeColumnType(Definition definition,string path,string name,string type){
        var design=Designer.Read(definition,path);
        var columns=design.Columns.Select(column=>{
            if(column.Name==name){return column with{Type=type};}
            return column;
        }).ToArray();
        return Designer.Apply(definition,design with{Columns=columns});
    }
}
