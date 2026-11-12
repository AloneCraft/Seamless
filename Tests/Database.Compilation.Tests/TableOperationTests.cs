using Seamless.Database;
using Seamless.Linq;

namespace Test.Database.Compilation;

public sealed class TableOperationTests {
    private readonly record struct ItemKey(int Id);
    private readonly record struct ItemRecord(ItemKey Key, int Value, string Name);

    private sealed class TestDatabase(DatabaseContainerOptions options) : DatabaseContainer(options) {
        public Table<ItemKey, ItemRecord> Items => this.Table<ItemKey, ItemRecord>(static row => row.Key);
        protected override void EnsureTransactionSchema() => _ = this.Items;
    }

    private static TestDatabase CreateDatabase() =>
        new(DatabaseContainerOptions.InMemory with {
            RecordSerialization = DatabaseRecordSerialization.RecordGraphV1
        });

    [Fact]
    public void Update_KeyUnchanged_UpdatesRecordInPlace() {
        using var db = CreateDatabase();
        var key = new ItemKey(1);
        using(var tx = db.BeginTransaction()) {
            db.Items[key] = new(key, 10, "initial");
            tx.Commit();
        }

        using(var tx = db.BeginTransaction()) {
            db.Items.Update(key, static current => current with { Value = 20, Name = "updated" });
            tx.Commit();
        }

        Assert.Equal(20, db.Items[key].Value);
        Assert.Equal("updated", db.Items[key].Name);
    }

    [Fact]
    public void Update_KeyNotFound_ThrowsKeyNotFoundException() {
        using var db = CreateDatabase();
        var key = new ItemKey(999);

        using var tx = db.BeginTransaction();
        Assert.Throws<KeyNotFoundException>(() =>
            db.Items.Update(key, static current => current with { Value = 1 }));
    }

    [Fact]
    public void Update_KeyChanged_ReplacesKeyAtomically() {
        using var db = CreateDatabase();
        var oldKey = new ItemKey(1);
        var newKey = new ItemKey(2);
        using(var tx = db.BeginTransaction()) {
            db.Items[oldKey] = new(oldKey, 100, "moving");
            tx.Commit();
        }

        using(var tx = db.BeginTransaction()) {
            db.Items.Update(oldKey, current => current with { Key = newKey, Value = 200 });
            tx.Commit();
        }

        Assert.False(db.Items.ContainsKey(oldKey));
        Assert.True(db.Items.ContainsKey(newKey));
        Assert.Equal(200, db.Items[newKey].Value);
        Assert.Equal(1, db.Items.Count);
    }

    [Fact]
    public void Update_KeyConflict_ThrowsAndPreservesOriginal() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        var key2 = new ItemKey(2);
        using(var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "first");
            db.Items[key2] = new(key2, 20, "second");
            tx.Commit();
        }

        using(var tx = db.BeginTransaction()) {
            Assert.Throws<InvalidOperationException>(() =>
                db.Items.Update(key1, current => current with { Key = key2 }));
            // コミットせずに抜ける（ロールバック）
        }

        Assert.True(db.Items.ContainsKey(key1));
        Assert.True(db.Items.ContainsKey(key2));
        Assert.Equal("first", db.Items[key1].Name);
        Assert.Equal("second", db.Items[key2].Name);
        Assert.Equal(2, db.Items.Count);
    }

    [Fact]
    public void Merge_NonExistingKey_CreatesWithAddedResult() {
        using var db = CreateDatabase();
        var key = new ItemKey(1);

        MergeResult result;
        using(var tx = db.BeginTransaction()) {
            result = db.Items.Merge(key,
                static k => new(k, 10, "created"),
                static cur => cur with { Value = cur.Value + 1 });
            tx.Commit();
        }

        Assert.Equal(MergeResult.Added, result);
        Assert.True(db.Items.ContainsKey(key));
        Assert.Equal(10, db.Items[key].Value);
        Assert.Equal("created", db.Items[key].Name);
    }

    [Fact]
    public void Merge_ExistingKey_UpdatesWithUpdatedResult() {
        using var db = CreateDatabase();
        var key = new ItemKey(1);
        using(var tx = db.BeginTransaction()) {
            db.Items[key] = new(key, 10, "initial");
            tx.Commit();
        }

        MergeResult result;
        using(var tx = db.BeginTransaction()) {
            result = db.Items.Merge(key,
                static k => new(k, 0, "fallback"),
                static cur => cur with { Value = cur.Value + 5, Name = "merged" });
            tx.Commit();
        }

        Assert.Equal(MergeResult.Updated, result);
        Assert.Equal(15, db.Items[key].Value);
        Assert.Equal("merged", db.Items[key].Name);
    }

    [Fact]
    public void ContainsAndRemove_FullRecordEquality_DistinguishesDifferences() {
        using var db = CreateDatabase();
        var key = new ItemKey(1);
        var exact = new ItemRecord(key, 10, "alpha");
        var different = new ItemRecord(key, 99, "beta");
        using(var tx = db.BeginTransaction()) {
            db.Items[key] = exact;
            tx.Commit();
        }

        // Contains: key is present, but values differ
        Assert.True(db.Items.ContainsKey(key));
        Assert.True(db.Items.Contains(exact));
        Assert.False(db.Items.Contains(different));

        // Remove(record): must match all columns
        using(var tx = db.BeginTransaction()) {
            Assert.False(db.Items.Remove(different));
            Assert.True(db.Items.ContainsKey(key));

            Assert.True(db.Items.Remove(exact));
            Assert.False(db.Items.ContainsKey(key));
            tx.Commit();
        }

        Assert.Empty(db.Items);
    }

    private readonly record struct InputPayload(int MatchId, int NewVal, string Label);

    [Fact]
    public void MergeWith_BasicAddAndUpdate_ReturnsExpectedCounts() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "item1");
            tx.Commit();
        }

        var inputs = new[] {
            new InputPayload(1, 15, "item1_mod"),
            new InputPayload(2, 20, "item2_new")
        };

        (long updated, long changed, long added) counts;
        using (var tx = db.BeginTransaction()) {
            counts = db.Items.MergeWith(
                inputs,
                static r => r.Key.Id,
                static i => i.MatchId,
                static (matchId, inp) => new(new ItemKey(matchId), inp.NewVal, inp.Label),
                static (cur, inp) => cur with { Value = inp.NewVal, Name = inp.Label });
            tx.Commit();
        }

        Assert.Equal((1L, 1L, 1L), counts);
        Assert.Equal(2, db.Items.Count);
        Assert.Equal(new ItemRecord(key1, 15, "item1_mod"), db.Items[key1]);
        Assert.Equal(new ItemRecord(new ItemKey(2), 20, "item2_new"), db.Items[new ItemKey(2)]);
    }

    [Fact]
    public void MergeWith_UnchangedUpdate_NotCountedAsChanged() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "item1");
            tx.Commit();
        }

        var inputs = new[] {
            new InputPayload(1, 10, "item1") // no changes
        };

        (long updated, long changed, long added) counts;
        using (var tx = db.BeginTransaction()) {
            counts = db.Items.MergeWith(
                inputs,
                static r => r.Key.Id,
                static i => i.MatchId,
                static (matchId, inp) => new(new ItemKey(matchId), inp.NewVal, inp.Label),
                static (cur, inp) => cur with { Value = inp.NewVal, Name = inp.Label });
            tx.Commit();
        }

        // Updated is 1, but Changed is 0
        Assert.Equal((1L, 0L, 0L), counts);
        Assert.Equal(new ItemRecord(key1, 10, "item1"), db.Items[key1]);
    }

    [Fact]
    public void MergeWith_DuplicateIdenticalInputs_DeduplicatedSuccessfully() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "item1");
            tx.Commit();
        }

        // 2 identical items for key 1
        var inputs = new[] {
            new InputPayload(1, 15, "item1_mod"),
            new InputPayload(1, 15, "item1_mod")
        };

        (long updated, long changed, long added) counts;
        using (var tx = db.BeginTransaction()) {
            counts = db.Items.MergeWith(
                inputs,
                static r => r.Key.Id,
                static i => i.MatchId,
                static (matchId, inp) => new(new ItemKey(matchId), inp.NewVal, inp.Label),
                static (cur, inp) => cur with { Value = inp.NewVal, Name = inp.Label });
            tx.Commit();
        }

        Assert.Equal((1L, 1L, 0L), counts);
        Assert.Equal(15, db.Items[key1].Value);
    }

    [Fact]
    public void MergeWith_DuplicateConflictingInputs_ThrowsInvalidOperationException() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "item1");
            tx.Commit();
        }

        // 2 conflicting items for matchId 1
        var inputs = new[] {
            new InputPayload(1, 15, "versionA"),
            new InputPayload(1, 20, "versionB")
        };

        using (var tx = db.BeginTransaction()) {
            Assert.Throws<InvalidOperationException>(() => {
                db.Items.MergeWith(
                    inputs,
                    static r => r.Key.Id,
                    static i => i.MatchId,
                    static (matchId, inp) => new(new ItemKey(matchId), inp.NewVal, inp.Label),
                    static (cur, inp) => cur with { Value = inp.NewVal, Name = inp.Label });
            });
        }
    }

    [Fact]
    public void MergeWith_CycleKeySwap_SucceedsAtomically() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        var key2 = new ItemKey(2);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 100, "first");
            db.Items[key2] = new(key2, 200, "second");
            tx.Commit();
        }

        // Swap primary keys: 1 -> 2, 2 -> 1
        var inputs = new[] {
            new InputPayload(1, 100, "swapped1to2"),
            new InputPayload(2, 200, "swapped2to1")
        };

        (long updated, long changed, long added) counts;
        using (var tx = db.BeginTransaction()) {
            counts = db.Items.MergeWith(
                inputs,
                static r => r.Key.Id,
                static i => i.MatchId,
                static (matchId, inp) => new(new ItemKey(matchId), inp.NewVal, inp.Label),
                static (cur, inp) => {
                    var newKey = cur.Key.Id == 1 ? new ItemKey(2) : new ItemKey(1);
                    return cur with { Key = newKey, Name = inp.Label };
                });
            tx.Commit();
        }

        Assert.Equal((2L, 2L, 0L), counts);
        Assert.Equal(2, db.Items.Count);
        // Key 1 now has record from original key 2
        Assert.Equal("swapped2to1", db.Items[key1].Name);
        Assert.Equal(200, db.Items[key1].Value);
        // Key 2 now has record from original key 1
        Assert.Equal("swapped1to2", db.Items[key2].Name);
        Assert.Equal(100, db.Items[key2].Value);
    }

    [Fact]
    public void MergeWith_CollisionOnAdd_ThrowsAndRollsBack() {
        using var db = CreateDatabase();
        var key1 = new ItemKey(1);
        using (var tx = db.BeginTransaction()) {
            db.Items[key1] = new(key1, 10, "item1");
            tx.Commit();
        }

        // Input 2 attempts to create a record with key 1, colliding with existing untouched key 1
        var inputs = new[] {
            new InputPayload(2, 20, "item2_colliding")
        };

        using (var tx = db.BeginTransaction()) {
            Assert.Throws<InvalidOperationException>(() => {
                db.Items.MergeWith(
                    inputs,
                    static r => r.Key.Id,
                    static i => i.MatchId,
                    static (matchId, inp) => new(new ItemKey(1), inp.NewVal, inp.Label), // deliberate key collision
                    static (cur, inp) => cur);
            });
            // rollback without commit
        }

        // Verify state is untouched
        Assert.Single(db.Items);
        Assert.Equal(new ItemRecord(key1, 10, "item1"), db.Items[key1]);
    }
}
