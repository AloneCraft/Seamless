namespace Test.Seamless.Database.Spec;

public sealed class DatabaseTransactionTests {
    [Fact,Trait("Category","Integration")]
    public async Task ReadSession_BlocksWriterAndRejectsPromotion() {
        {
            using var database=new SpecDatabase();
            database.Items.Add(new Records.Item(1,"Before"));
            using var read=database.BeginReadSession();
            Assert.Equal("Before",database.Items[new Keys.Item(1)].Name);
            Assert.Throws<InvalidOperationException>(()=>database.Items.Add(new Records.Item(2,"Rejected")));
            Assert.Throws<InvalidOperationException>(()=>database.BeginTransaction());

            Task writer;
            using(global::System.Threading.ExecutionContext.SuppressFlow()) {
                writer=Task.Run(()=>{
                    using var transaction=database.BeginTransaction();
                    database.Items[new Keys.Item(1)]=new Records.Item(1,"After");
                    transaction.Commit();
                });
            }
            await Task.Delay(100);
            Assert.False(writer.IsCompleted);
            read.Dispose();
            await writer.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("After",database.Items[new Keys.Item(1)].Name);
        }
        {
            using var database=new SpecDatabase();
            using var read=database.BeginReadSession();
            using var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            Task writer;
            using(global::System.Threading.ExecutionContext.SuppressFlow()) {
                writer=Task.Run(()=>Assert.Throws<OperationCanceledException>(()=>database.BeginTransaction(cancellation.Token)));
            }
            await writer.WaitAsync(TimeSpan.FromSeconds(5));
        }
        {
            using var database=new SpecDatabase();
            database.Items.Add(new Records.Item(1,"One"));
            using var iterator=database.Items.GetEnumerator();
            Assert.True(iterator.MoveNext());
            Task<bool> writer;
            using(global::System.Threading.ExecutionContext.SuppressFlow()) {
                writer=Task.Run(()=>database.Items.Add(new Records.Item(2,"Two")));
            }
            await Task.Delay(100);
            Assert.False(writer.IsCompleted);
            iterator.Dispose();
            Assert.True(await writer.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Commit_AppliesStagedRows() {
        using var database=new SpecDatabase();
        using(var transaction=database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(1,"Apple")));
            Assert.Equal("Apple",database.Items[new Keys.Item(1)].Name);
            transaction.Commit();
        }

        Assert.Equal("Apple",database.Items[new Keys.Item(1)].Name);
    }

    [Fact,Trait("Category","Unit")]
    public void DisposeWithoutCommit_RollsBack() {
        using var database=new SpecDatabase();
        using(database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(1,"Apple")));
            Assert.True(database.Items.ContainsKey(new(1)));
        }

        Assert.False(database.Items.ContainsKey(new(1)));
    }

    [Fact,Trait("Category","Unit")]
    public void InnerRollback_AbortsWholeTransaction() {
        using var database=new SpecDatabase();
        using(var outer=database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(1,"Outer")));
            using(var inner=database.BeginTransaction()) {
                Assert.True(database.Items.Add(new Records.Item(2,"Inner")));
                inner.Rollback();
            }
            outer.Commit();
        }

        Assert.Empty(database.Items);
    }

    [Fact,Trait("Category","Unit")]
    public void MemoryLimit_SpillsChangesToTsavoriteAndCommits() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            TransactionMemoryLimit=1,
        };
        using var database=new SpecDatabase(options);
        using(var transaction=database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(1,"One")));
            Assert.True(database.Items.Add(new Records.Item(2,"Two")));
            Assert.Equal(2,database.Items.Count);
            transaction.Commit();
        }

        Assert.Equal("One",database.Items[new Keys.Item(1)].Name);
        Assert.Equal("Two",database.Items[new Keys.Item(2)].Name);
    }

    [Fact,Trait("Category","Unit")]
    public void SharedResourceBudget_ReportsSpillAndReleasesTransactionResources() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            ManagedMemoryLimitBytes=16L<<20,
            TemporaryStorageLimitBytes=1L<<20,
            TransactionMemoryLimit=1,
        };
        using var database=new SpecDatabase(options);
        _=database.Items;
        var storeMemory=database.ResourceUsage.MemoryBytes;
        Assert.True(storeMemory>0);
        using(var transaction=database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(1,"One")));
            Assert.True(database.Items.Add(new Records.Item(2,"Two")));
            var active=database.ResourceUsage;
            Assert.Equal(options.ManagedMemoryLimitBytes,active.MemoryLimitBytes);
            Assert.Equal(options.TemporaryStorageLimitBytes,active.TemporaryStorageLimitBytes);
            Assert.True(active.PeakMemoryBytes>0);
            Assert.True(active.TemporaryStorageBytes>0);
            transaction.Commit();
        }

        var released=database.ResourceUsage;
        Assert.Equal(storeMemory,released.MemoryBytes);
        Assert.Equal(0,released.TemporaryStorageBytes);
        Assert.True(released.PeakTemporaryStorageBytes>0);
    }

    [Fact,Trait("Category","Unit")]
    public void TemporaryStorageLimit_ReportsItsOwnResourceKind() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            ManagedMemoryLimitBytes=16L<<20,
            TemporaryStorageLimitBytes=0,
            TransactionMemoryLimit=1,
        };
        using var database=new SpecDatabase(options);
        using var transaction=database.BeginTransaction();
        var error=Assert.Throws<global::Seamless.Execution.ResourceLimitException>(
            ()=>database.Items.Add(new Records.Item(1,"One")));
        Assert.Equal(global::Seamless.Execution.ResourceLimitKind.TemporaryStorage,error.Kind);
    }

    [Fact,Trait("Category","Unit")]
    public void TableStores_ShareOneDatabaseMemoryBudget() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            IndexSize=1L<<8,
            MemorySize=1L<<20,
            ManagedMemoryLimitBytes=6L<<20,
            TransactionMemoryLimit=1L<<20,
        };
        using var database=new TwoTableDatabase(options);

        _=database.First;
        var usage=database.ResourceUsage;
        Assert.InRange(usage.MemoryBytes,1,usage.MemoryLimitBytes);
        var error=Assert.Throws<global::Seamless.Execution.ResourceLimitException>(()=>_=database.Second);
        Assert.Equal(global::Seamless.Execution.ResourceLimitKind.Memory,error.Kind);
        Assert.Equal(usage.MemoryBytes,database.ResourceUsage.MemoryBytes);
    }

    [Fact,Trait("Category","Unit")]
    public void TableSpecificStorage_AvoidsRepeatingTheLargestStoreAllocation() {
        var small=new global::Seamless.Database.DatabaseTableStorageOptions {
            IndexSize=1L<<6,
            PageSize=1L<<12,
            SegmentSize=1L<<12,
            MemorySize=1L<<13,
        };
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            IndexSize=1L<<20,
            MemorySize=1L<<26,
            ManagedMemoryLimitBytes=9L<<20,
            TransactionMemoryLimit=1L<<20,
            TableStorage=new Dictionary<string,global::Seamless.Database.DatabaseTableStorageOptions>(StringComparer.Ordinal) {
                ["First"]=small,
                ["Second"]=small,
            },
        };
        using var database=new TwoTableDatabase(options);

        _=database.First;
        _=database.Second;

        Assert.InRange(database.ResourceUsage.MemoryBytes,1,options.ManagedMemoryLimitBytes);
    }

    [Fact,Trait("Category","Integration")]
    public async Task UnkeyedTable_UsesRollbackAndHoldsReadSessionForEnumeration() {
        using var database=new UnkeyedDatabase();
        Assert.True(database.Items.Add(new Records.Item(1,"One")));
        using(database.BeginTransaction()) {
            Assert.True(database.Items.Add(new Records.Item(2,"Rolled back")));
        }
        Assert.DoesNotContain(new Records.Item(2,"Rolled back"),database.Items);

        using var iterator=database.Items.GetEnumerator();
        Assert.True(iterator.MoveNext());
        Task<bool> writer;
        using(global::System.Threading.ExecutionContext.SuppressFlow()) {
            writer=Task.Run(()=>database.Items.Add(new Records.Item(3,"Three")));
        }
        await Task.Delay(100);
        Assert.False(writer.IsCompleted);
        iterator.Dispose();
        Assert.True(await writer.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact,Trait("Category","Integration")]
    public void UnkeyedTable_ClearCommitsAndRollsBackWithoutSnapshottingRows() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<8,
                MemorySize=1L<<20,
            };
            using(var database=new UnkeyedDatabase(options)) {
                Assert.True(database.Items.Add(new Records.Item(1,"One")));
                Assert.True(database.Items.Add(new Records.Item(2,"Two")));
                using(database.BeginTransaction()) {
                    database.Items.Clear();
                    Assert.True(database.Items.Add(new Records.Item(3,"Rolled back")));
                    Assert.Single(database.Items);
                }
                Assert.Equal(2,database.Items.LongCount);
                Assert.Contains(new Records.Item(1,"One"),database.Items);
                Assert.Contains(new Records.Item(2,"Two"),database.Items);

                using(var transaction=database.BeginTransaction()) {
                    database.Items.Clear();
                    Assert.True(database.Items.Add(new Records.Item(4,"Committed")));
                    Assert.Single(database.Items);
                    transaction.Commit();
                }
                Assert.Equal([new Records.Item(4,"Committed")],database.Items);
            }

            using var reopened=new UnkeyedDatabase(options);
            Assert.Equal([new Records.Item(4,"Committed")],reopened.Items);
        } finally {
            if(Directory.Exists(directory)){Directory.Delete(directory,recursive:true);}
        }
    }

    [Fact,Trait("Category","Unit")]
    public void UnkeyedSetOperations_AsFirstTransactionMutation_CommitAndRollbackThroughOverlay() {
        using var database=new UnkeyedDatabase();
        var one=new Records.Item(1,"One");
        var two=new Records.Item(2,"Two");
        var three=new Records.Item(3,"Three");
        var four=new Records.Item(4,"Four");
        Assert.True(database.Items.Add(one));
        Assert.True(database.Items.Add(two));
        Assert.True(database.Items.Add(three));

        using(database.BeginTransaction()) {
            database.Items.IntersectWith([two,three]);
            Assert.Equal(2,database.Items.LongCount);
            Assert.DoesNotContain(one,database.Items);
        }

        Assert.Equal(3,database.Items.LongCount);
        Assert.Contains(one,database.Items);
        using(var transaction=database.BeginTransaction()) {
            database.Items.SymmetricExceptWith([three,four,four]);
            Assert.Equal(3,database.Items.LongCount);
            Assert.Contains(one,database.Items);
            Assert.Contains(two,database.Items);
            Assert.Contains(four,database.Items);
            Assert.DoesNotContain(three,database.Items);
            transaction.Commit();
        }

        Assert.Equal(3,database.Items.LongCount);
        Assert.Contains(one,database.Items);
        Assert.Contains(two,database.Items);
        Assert.Contains(four,database.Items);
        Assert.DoesNotContain(three,database.Items);
    }

    [Fact,Trait("Category","Unit")]
    public void UnkeyedSetOperations_UseSharedTemporaryBudgetAndReleaseIt() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            IndexSize=1L<<8,
            MemorySize=1L<<20,
            ManagedMemoryLimitBytes=8L<<20,
            TemporaryStorageLimitBytes=32L<<20,
            TransactionMemoryLimit=1,
        };
        using var database=new UnkeyedDatabase(options);
        for(var index=0;index<1500;index++) {
            Assert.True(database.Items.Add(new Records.Item(index,new string((char)('A'+index%20),128))));
        }
        IEnumerable<Records.Item> Retained() {
            for(var index=0;index<1500;index+=2) {
                yield return new Records.Item(index,new string((char)('A'+index%20),128));
            }
        }

        using(var transaction=database.BeginTransaction()) {
            database.Items.IntersectWith(Retained());
            Assert.Equal(750,database.Items.LongCount);
            Assert.True(database.ResourceUsage.PeakTemporaryStorageBytes>0);
            Assert.InRange(database.ResourceUsage.MemoryBytes,1,options.ManagedMemoryLimitBytes);
            Assert.True(database.ResourceUsage.TemporaryStorageBytes>0);
            transaction.Commit();
        }

        Assert.Equal(750,database.Items.LongCount);
        Assert.Equal(0,database.ResourceUsage.TemporaryStorageBytes);
    }

    [Fact,Trait("Category","Integration")]
    public void CommittedWal_IsArchivedAfterCheckpointAndReopenIsIdempotent() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<8,
                MemorySize=1L<<20,
            };
            using(var database=new SpecDatabase(options)) {
                using var transaction=database.BeginTransaction();
                Assert.True(database.Items.Add(new Records.Item(7,"Durable")));
                transaction.Commit();
            }

            var wal=Path.Combine(directory,"database.wal");
            Assert.True(File.Exists(wal));
            Assert.Equal(0,new FileInfo(wal).Length);
            var archives=Directory.GetFiles(wal+".archive","*.wal");
            Assert.Single(archives);
            Assert.True(new FileInfo(archives[0]).Length>0);

            using var recovered=new SpecDatabase(options);
            Assert.Equal("Durable",recovered.Items[new Keys.Item(7)].Name);
            Assert.Single(recovered.Items);
        } finally {
            if(Directory.Exists(directory)) {
                Directory.Delete(directory,recursive:true);
            }
        }
    }

    [Fact,Trait("Category","Integration")]
    public void CommittedWal_WithoutCheckpointIsReplayedThenArchived() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(directory);
            var temporary=Path.Combine(directory,"temporary");
            using(var resources=new global::Seamless.Execution.ExecutionScope(new global::Seamless.Execution.ExecutionOptions {
                TemporaryDirectory=temporary,
            })) {
                var operation=global::Seamless.Database.DatabaseWalOperationFactory.Create(
                    resources,
                    "Items",
                    false,
                    new Keys.Item(11),
                    new Records.Item(11,"From WAL"));
                new global::Seamless.Database.DatabaseWriteAheadLog(Path.Combine(directory,"database.wal")).Append([operation]);
            }
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<8,
                MemorySize=1L<<20,
            };

            using(var recovered=new SpecDatabase(options)) {
                Assert.Equal("From WAL",recovered.Items[new Keys.Item(11)].Name);
                Assert.Single(recovered.Items);
            }

            var wal=Path.Combine(directory,"database.wal");
            Assert.Equal(0,new FileInfo(wal).Length);
            Assert.Single(Directory.GetFiles(wal+".archive","*.wal"));
            using var reopened=new SpecDatabase(options);
            Assert.Equal("From WAL",reopened.Items[new Keys.Item(11)].Name);
        } finally {
            if(Directory.Exists(directory)){Directory.Delete(directory,recursive:true);}
        }
    }

    [Fact,Trait("Category","Integration")]
    public void WalArchive_RejectsTamperedExistingArchiveAndKeepsActiveWal() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(directory);
            var wal=Path.Combine(directory,"database.wal");
            using(var resources=new global::Seamless.Execution.ExecutionScope()) {
                var operation=global::Seamless.Database.DatabaseWalOperationFactory.Create(
                    resources,
                    "Items",
                    false,
                    new Keys.Item(17),
                    new Records.Item(17,"Protected"));
                new global::Seamless.Database.DatabaseWriteAheadLog(wal).Append([operation]);
            }
            string hash;
            using(var stream=File.OpenRead(wal)) {
                hash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(stream));
            }
            var archiveDirectory=wal+".archive";
            Directory.CreateDirectory(archiveDirectory);
            File.WriteAllText(Path.Combine(archiveDirectory,hash+".wal"),"tampered");
            var original=File.ReadAllBytes(wal);

            var error=Assert.Throws<IOException>(
                ()=>new global::Seamless.Database.DatabaseWriteAheadLog(wal).ArchiveAfterCheckpoint());
            Assert.Contains("does not match",error.Message,StringComparison.Ordinal);
            Assert.Equal(original,File.ReadAllBytes(wal));
        } finally {
            if(Directory.Exists(directory)){Directory.Delete(directory,recursive:true);}
        }
    }

    [Fact,Trait("Category","Integration")]
    public void WalForUnopenedTable_IsKeptUntilThatTableIsRecoveredAndCheckpointed() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(directory);
            var wal=Path.Combine(directory,"database.wal");
            using(var resources=new global::Seamless.Execution.ExecutionScope()) {
                var operation=global::Seamless.Database.DatabaseWalOperationFactory.Create(
                    resources,
                    "Second",
                    false,
                    new Keys.Item(23),
                    new Records.Item(23,"Deferred"));
                new global::Seamless.Database.DatabaseWriteAheadLog(wal).Append([operation]);
            }
            var originalLength=new FileInfo(wal).Length;
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<8,
                MemorySize=1L<<20,
            };

            using(var firstOpen=new TwoTableDatabase(options)) {
                Assert.Empty(firstOpen.First);
            }
            Assert.Equal(originalLength,new FileInfo(wal).Length);
            Assert.False(Directory.Exists(wal+".archive"));

            using(var secondOpen=new TwoTableDatabase(options)) {
                Assert.Equal("Deferred",secondOpen.Second[new Keys.Item(23)].Name);
            }
            Assert.Equal(0,new FileInfo(wal).Length);
            Assert.Single(Directory.GetFiles(wal+".archive","*.wal"));
        } finally {
            if(Directory.Exists(directory)){Directory.Delete(directory,recursive:true);}
        }
    }

    [Fact,Trait("Category","Integration")]
    public void WalRecovery_IgnoresUncommittedAndTruncatedTailAcrossRepeatedRecovery() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.Database.Transaction",Guid.NewGuid().ToString("N"));
        try {
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<8,
                MemorySize=1L<<20,
            };
            using(var database=new SpecDatabase(options)) {
                using var transaction=database.BeginTransaction();
                Assert.True(database.Items.Add(new Records.Item(7,"Committed")));
                transaction.Commit();
            }

            var wal=Path.Combine(directory,"database.wal");
            var transactionId=Guid.NewGuid();
            var key=Convert.ToBase64String(global::MemoryPack.MemoryPackSerializer.Serialize(new Keys.Item(8)));
            var row=Convert.ToBase64String(global::MemoryPack.MemoryPackSerializer.Serialize(new Records.Item(8,"Uncommitted")));
            var begin=global::System.Text.Json.JsonSerializer.Serialize(new {
                Version=2,
                Kind="begin",
                TransactionId=transactionId,
            });
            var operation=global::System.Text.Json.JsonSerializer.Serialize(new {
                Version=2,
                Kind="operation",
                TransactionId=transactionId,
                TableName="Items",
                Deleted=false,
                KeyData=key,
                RecordData=row,
            });
            File.AppendAllText(wal,begin+Environment.NewLine+operation+Environment.NewLine+"{"+Convert.ToChar(34)+"Version"+Convert.ToChar(34)+":1");

            for(var attempt=0;attempt<2;attempt++) {
                using var recovered=new SpecDatabase(options);
                Assert.Equal("Committed",recovered.Items[new Keys.Item(7)].Name);
                Assert.False(recovered.Items.ContainsKey(new Keys.Item(8)));
                Assert.Single(recovered.Items);
            }
        } finally {
            if(Directory.Exists(directory)) {
                Directory.Delete(directory,recursive:true);
            }
        }
    }

    private sealed class TwoTableDatabase(global::Seamless.Database.DatabaseContainerOptions options)
        :global::Seamless.Database.DatabaseContainer(options) {
        public global::Seamless.Database.Table<Keys.Item,Records.Item> First=>
            this.Table<Keys.Item,Records.Item>(static item=>new(item.Id));
        public global::Seamless.Database.Table<Keys.Item,Records.Item> Second=>
            this.Table<Keys.Item,Records.Item>(static item=>new(item.Id));
    }

    private sealed class UnkeyedDatabase:global::Seamless.Database.DatabaseContainer {
        public UnkeyedDatabase() { }
        public UnkeyedDatabase(global::Seamless.Database.DatabaseContainerOptions options):base(options) { }
        public global::Seamless.Database.Table<Records.Item> Items=>this.Table<Records.Item>();
    }
}
