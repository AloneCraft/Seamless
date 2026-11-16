using MemoryPack;
using Seamless.Collections;
using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests.Relational;

// 関係代入(Document/refactoring-designs/20261117-関係代入.md)の試験。
internal static partial class DatabaseGenerator;
[MemoryPackable]public readonly partial record struct Item(int Key,string Name,bool Active):IRecord<int>;
[MemoryPackable]public readonly partial record struct Tag(string Name);
public sealed partial class ShopDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public partial Table<int,Item> Items{get;set;}
    public partial Table<Tag> Tags{get;set;}
}

/// <summary>書き込みの回数を数える、試験用の表。差分が最小かを外から確かめる。</summary>
internal sealed class RecordingTable:Table<int,Item>{
    private readonly Dictionary<int,Item> rows=[];
    public int Adds,Removes,Sets,Scans;
    public RecordingTable(params Item[] items){foreach(var item in items){this.rows.Add(item.Key,item);}}
    public void ResetCounts()=>this.Adds=this.Removes=this.Sets=this.Scans=0;
    public IReadOnlyCollection<Item> Snapshot=>this.rows.Values.OrderBy(static row=>row.Key).ToArray();
    public override bool TryGetValue(int key,out Item row)=>this.rows.TryGetValue(key,out row);
    public override Item this[int key]{get=>this.rows[key];set{this.Sets++;this.rows[key]=value;}}
    public override bool Add(Item row){this.Adds++;return this.rows.TryAdd(row.Key,row);}
    public override bool Remove(int key){this.Removes++;return this.rows.Remove(key);}
    public override IEnumerator<Item> GetEnumerator(){this.Scans++;return this.rows.Values.ToList().GetEnumerator();}
    protected override int KeyOf(Item row)=>row.Key;
}

public sealed class RelationalAssignmentDifferenceTests{
    private static readonly Item A=new(1,"a",true),B=new(2,"b",false),C=new(3,"c",true);

    [Fact]
    public void UnionAddsOnlyMissingRowsWithoutScanningTheTable(){
        var table=new RecordingTable(A,B);
        Table<int,Item> target=table;
        target.Assign(target.Union([A,C]));
        Assert.Equal(new[]{A,B,C},table.Snapshot);
        Assert.Equal((1,0,0,0),(table.Adds,table.Removes,table.Sets,table.Scans));
    }

    [Fact]
    public void UnionOfExistingRowsWritesNothing(){
        var table=new RecordingTable(A,B);
        table.Assign(table.Union(new List<Item>{A,B,A}));
        Assert.Equal((0,0,0,0),(table.Adds,table.Removes,table.Sets,table.Scans));
    }

    [Fact]
    public void UnionThatLeavesTwoRowsWithTheSameKeyIsAPrimaryKeyViolationAndWritesNothing(){
        var table=new RecordingTable(A,B);
        var error=Assert.Throws<PrimaryKeyViolationException>(()=>table.Assign(table.Union([C,A with{Name="changed"}])));
        Assert.Equal(1,error.Key);
        Assert.Equal(new[]{A,B},table.Snapshot);
        Assert.Equal(0,table.Adds+table.Removes+table.Sets);
        // 右辺の中で同じキーの違う行が重なっても違反。
        Assert.Throws<PrimaryKeyViolationException>(()=>table.Assign(table.Union([C,C with{Name="other"}])));
        Assert.Equal(0,table.Adds+table.Removes+table.Sets);
    }

    [Fact]
    public void ExceptRemovesOnlyRowsThatAreEqualInEveryColumnWithoutScanningTheTable(){
        var table=new RecordingTable(A,B,C);
        table.Assign(table.Except([A,B with{Name="different"}]));
        Assert.Equal(new[]{B,C},table.Snapshot);
        Assert.Equal((0,1,0,0),(table.Adds,table.Removes,table.Sets,table.Scans));
    }

    [Fact]
    public void IntersectAndWhereRemoveRejectedRowsInOneScan(){
        var table=new RecordingTable(A,B,C);
        table.Assign(table.Intersect([A,B,new Item(9,"x",true)]));
        Assert.Equal(new[]{A,B},table.Snapshot);
        Assert.Equal((0,1,0,1),(table.Adds,table.Removes,table.Sets,table.Scans));
        table.ResetCounts();
        table.Assign(table.Where(static row=>row.Active).Except([new Item(7,"none",true)]));
        Assert.Equal(new[]{A},table.Snapshot);
        Assert.Equal((0,1,0,1),(table.Adds,table.Removes,table.Sets,table.Scans));
    }

    [Fact]
    public void ReplacementWritesOnlyTheDifferenceAndOverwritesChangedRows(){
        var table=new RecordingTable(A,B,C);
        var changedB=B with{Name="b2"};var d=new Item(4,"d",false);
        // 並べ替えは結果に影響しない。
        table.Assign(new(new[]{d,changedB,A}.OrderBy(static row=>row.Name)));
        Assert.Equal(new[]{A,changedB,d},table.Snapshot);
        Assert.Equal((1,1,1),(table.Adds,table.Removes,table.Sets));
    }

    [Fact]
    public void ReplacementWithTwoDifferentRowsForOneKeyIsAViolationAndWritesNothing(){
        var table=new RecordingTable(A,B);
        Assert.Throws<PrimaryKeyViolationException>(()=>table.Assign([C,C with{Active=false}]));
        Assert.Equal(new[]{A,B},table.Snapshot);
        Assert.Equal(0,table.Adds+table.Removes+table.Sets);
    }

    [Fact]
    public void AssigningTheSameTableOrEquivalentRowsWritesNothing(){
        var table=new RecordingTable(A,B);
        table.Assign(table);
        table.Assign(table.Union([]));
        table.Assign([B,A,A]);
        Assert.Equal(0,table.Adds+table.Removes+table.Sets);
    }

    [Fact]
    public void ShortFormsAllMeanReplacement(){
        var table=new RecordingTable(A);
        Table<int,Item> target=table;
        target=new List<Item>{B};Assert.True(target.IsExpression);
        table.Assign(target);Assert.Equal(new[]{B},table.Snapshot);
        target=new[]{C};table.Assign(target);Assert.Equal(new[]{C},table.Snapshot);
        IEnumerable<Item> query=new[]{A,B}.Where(static row=>row.Key>0);
        target=[..query];table.Assign(target);Assert.Equal(new[]{A,B},table.Snapshot);
        table.Assign([A]);Assert.Equal(new[]{A},table.Snapshot);
    }

    [Fact]
    public void AnExpressionCannotBeWrittenAndCannotBeSearchedByKey(){
        var table=new RecordingTable(A);
        var expression=table.Union([B]);
        Assert.Throws<InvalidOperationException>(()=>expression.Add(C));
        Assert.Throws<InvalidOperationException>(()=>expression.Remove(1));
        Assert.Throws<InvalidOperationException>(()=>expression[1]=A);
        Assert.Throws<InvalidOperationException>(()=>expression.Assign(table));
        Assert.Equal(2,expression.Count);
        Assert.True(expression.Contains(B));
        Assert.Equal(0,table.Adds+table.Removes+table.Sets);
    }
}

public sealed class RelationalAssignmentDatabaseTests{
    private static readonly Item A=new(1,"a",true),B=new(2,"b",false),C=new(3,"c",true);

    private static DatabaseContainerOptions Options(bool versioned){
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        if(!versioned){return options;}
        return options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,MemorySize=1<<16,SegmentSize=1<<16,IndexSize=16,
            TransactionMemoryLimit=1024,ManagedMemoryLimitBytes=64<<20,TemporaryStorageLimitBytes=32<<20,
            TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"relational-work"),
            TransactionHistory=new(){MaxRetainedMemoryBytes=64<<10,MaxRetainedStorageBytes=1<<20,MaxMetadataBytes=512<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}};
    }

    private static async Task<Item[]> ReadItems(ShopDatabase db){
        await using var read=await db.BeginReadOnlyTransactionAsync();
        var rows=read.Items.OrderBy(static row=>row.Key).ToArray();
        await read.CommitAsync();
        return rows;
    }

    private static async Task<string[]> ReadTags(ShopDatabase db){
        await using var read=await db.BeginReadOnlyTransactionAsync();
        var rows=read.Tags.Select(static tag=>tag.Name).Order(StringComparer.Ordinal).ToArray();
        await read.CommitAsync();
        return rows;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionTablesAcceptRelationalAssignmentInsideTheTransaction(bool versioned){
        using var db=new ShopDatabase(Options(versioned));
        await using(var tx=await db.BeginTransactionAsync()){
            tx.Items=tx.Items.Union([A,B,C]);
            tx.Items=tx.Items.Where(static row=>row.Active);
            Assert.Equal(2,tx.Items.Count);
            await tx.CommitAsync();
        }
        Assert.Equal(new[]{A,C},await ReadItems(db));
        await using(var tx=await db.BeginTransactionAsync()){
            tx.Items=tx.Items.Except([C]).Union([B]);
            // 確定しないで抜けると、関係代入の差分も取り消される。
        }
        Assert.Equal(new[]{A,C},await ReadItems(db));
        await using(var tx=await db.BeginTransactionAsync()){
            tx.Items=new(new[]{A with{Name="a2"},B});
            await tx.CommitAsync();
        }
        Assert.Equal(new[]{A with{Name="a2"},B},await ReadItems(db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseTablesCommitEachAssignmentAndEachWriteInAnImplicitTransaction(bool versioned){
        using var db=new ShopDatabase(Options(versioned));
        Assert.True(db.Items.Add(A));
        db.Items=db.Items.Union([B,C]);
        Assert.Equal(new[]{A,B,C},await ReadItems(db));
        db.Items=db.Items.Where(static row=>row.Active);
        Assert.Equal(new[]{A,C},await ReadItems(db));
        db.Items[1]=A with{Name="renamed"};
        Assert.True(db.Items.Remove(3));
        Assert.Equal(new[]{A with{Name="renamed"}},await ReadItems(db));
        // 主キーの違反では1行も書かない。
        Assert.Throws<PrimaryKeyViolationException>(()=>db.Items=db.Items.Union([C,A]));
        Assert.Equal(new[]{A with{Name="renamed"}},await ReadItems(db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseTablesCannotBeUsedWhileATransactionIsOpenOnTheFlow(bool versioned){
        using var db=new ShopDatabase(Options(versioned));
        db.Items=[A];
        await using(var tx=await db.BeginTransactionAsync()){
            Assert.Throws<InvalidOperationException>(()=>db.Items=db.Items.Union([B]));
            Assert.Throws<InvalidOperationException>(()=>db.Items.Add(B));
            Assert.Throws<InvalidOperationException>(()=>tx.Items=tx.Items.Union(db.Items));
            await tx.CommitAsync();
        }
        Assert.Equal(new[]{A},await ReadItems(db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeylessTablesUseTheRowAsTheKey(bool versioned){
        using var db=new ShopDatabase(Options(versioned));
        db.Tags=db.Tags.Union([new("x"),new("y"),new("z")]);
        Assert.Equal(new[]{"x","y","z"},await ReadTags(db));
        db.Tags=db.Tags.Except([new("y")]).Where(static tag=>tag.Name!="z");
        Assert.Equal(new[]{"x"},await ReadTags(db));
        await using(var tx=await db.BeginTransactionAsync()){
            tx.Tags=tx.Tags.Intersect([new("x"),new("w")]).Union([new("v")]);
            await tx.CommitAsync();
        }
        Assert.Equal(new[]{"v","x"},await ReadTags(db));
        db.Tags=[new Tag("q")];
        Assert.Equal(new[]{"q"},await ReadTags(db));
    }
}

// FLinq が関係代入の文を書き換える経路と、実行時に式を読む経路。同じ書き方で同じ結果になることを比べる。
[global::Seamless.FLinq.LinqIntercept(true)]
public sealed class RewrittenWriter{
    public int KeyCalls;
    public void AddOrdered(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=tx.Items.Union(rows.OrderBy(row=>{this.KeyCalls++;return row.Key;}));}
    public void ReplaceOrdered(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=new(rows.OrderBy(row=>{this.KeyCalls++;return row.Name;}).Distinct());}
    public void KeepActive(ShopDatabase.TypedTransaction tx){tx.Items=tx.Items.Where(static row=>row.Active);}
    public void RemoveListed(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=tx.Items.Except(rows);}
    public void AddOrderedToDatabase(ShopDatabase db,Item[] rows){db.Items=db.Items.Union(rows.OrderBy(row=>{this.KeyCalls++;return row.Key;}));}
}
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class RuntimeWriter{
    public int KeyCalls;
    public void AddOrdered(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=tx.Items.Union(rows.OrderBy(row=>{this.KeyCalls++;return row.Key;}));}
    public void ReplaceOrdered(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=new(rows.OrderBy(row=>{this.KeyCalls++;return row.Name;}).Distinct());}
    public void KeepActive(ShopDatabase.TypedTransaction tx){tx.Items=tx.Items.Where(static row=>row.Active);}
    public void RemoveListed(ShopDatabase.TypedTransaction tx,Item[] rows){tx.Items=tx.Items.Except(rows);}
    public void AddOrderedToDatabase(ShopDatabase db,Item[] rows){db.Items=db.Items.Union(rows.OrderBy(row=>{this.KeyCalls++;return row.Key;}));}
}

public sealed class RelationalAssignmentRewriteTests{
    private static readonly Item A=new(1,"a",true),B=new(2,"b",false),C=new(3,"c",true),D=new(4,"d",true);

    private static async Task<Item[]> Run(ShopDatabase db,Action<ShopDatabase.TypedTransaction> step){
        await using(var tx=await db.BeginTransactionAsync()){step(tx);await tx.CommitAsync();}
        await using var read=await db.BeginReadOnlyTransactionAsync();
        var rows=read.Items.OrderBy(static row=>row.Key).ToArray();
        await read.CommitAsync();
        return rows;
    }

    private static DatabaseContainerOptions Options()=>DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};

    [Fact]
    public async Task RewrittenAndRuntimeAssignmentsLeaveTheSameRowsAndTheRewriteDropsOrdering(){
        var rewritten=new RewrittenWriter();var runtime=new RuntimeWriter();
        using var first=new ShopDatabase(Options());using var second=new ShopDatabase(Options());
        async Task Both(Action<ShopDatabase.TypedTransaction> left,Action<ShopDatabase.TypedTransaction> right,Item[] expected){
            Assert.Equal(expected,await Run(first,left));
            Assert.Equal(expected,await Run(second,right));
        }
        await Both(tx=>rewritten.AddOrdered(tx,[C,A,B]),tx=>runtime.AddOrdered(tx,[C,A,B]),new[]{A,B,C});
        await Both(tx=>rewritten.KeepActive(tx),tx=>runtime.KeepActive(tx),new[]{A,C});
        await Both(tx=>rewritten.RemoveListed(tx,[C,A with{Name="other"}]),tx=>runtime.RemoveListed(tx,[C,A with{Name="other"}]),new[]{A});
        await Both(tx=>rewritten.ReplaceOrdered(tx,[D,A with{Name="a2"},D]),tx=>runtime.ReplaceOrdered(tx,[D,A with{Name="a2"},D]),new[]{A with{Name="a2"},D});
        rewritten.AddOrderedToDatabase(first,[B]);runtime.AddOrderedToDatabase(second,[B]);
        Assert.Equal(await Run(first,static _=>{}),await Run(second,static _=>{}));
        // 代入先は集合なので、書き換えた経路は右辺の並べ替えを取り除く(鍵を求める関数を呼ばない)。実行時の経路は並べ替えを実行する。
        Assert.Equal(0,rewritten.KeyCalls);
        Assert.True(runtime.KeyCalls>0);
    }

    [Fact]
    public async Task RewrittenUnionStillRejectsAConflictingRowAndWritesNothing(){
        var rewritten=new RewrittenWriter();
        using var db=new ShopDatabase(Options());
        await Run(db,tx=>rewritten.AddOrdered(tx,[A,B]));
        await using(var tx=await db.BeginTransactionAsync()){
            Assert.Throws<PrimaryKeyViolationException>(()=>rewritten.AddOrdered(tx,[C,A with{Name="conflict"}]));
            await tx.CommitAsync();
        }
        Assert.Equal(new[]{A,B},await Run(db,static _=>{}));
    }
}