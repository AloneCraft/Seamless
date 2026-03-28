using Common;
using MemoryPack;
using Tsavorite.core;
using Xunit;

namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class TestKeyComparer {
    [Fact, Trait("Category","Coverage")]
    public void GetHashCode64_Equals() {
        var comparer = new KeyComparer<int>();
        var a = MemoryPackSerializer.Serialize(123);
        var b = MemoryPackSerializer.Serialize(123);
        var c = MemoryPackSerializer.Serialize(456);

        Assert.NotEqual(0,comparer.GetHashCode64(a));
        Assert.True(comparer.Equals(a,b));
        Assert.False(comparer.Equals(a,c));
    }

    [Fact, Trait("Category","Coverage")]
    public void IKeyComparer_GetHashCode64_Equals() {
        var comparer = new KeyComparer<int>();
        var asInterface = (IKeyComparer)comparer;
        var k1Bytes = MemoryPackSerializer.Serialize(1);
        var k2Bytes = MemoryPackSerializer.Serialize(2);
        var k1 = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)k1Bytes;
        var k2 = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)k2Bytes;
        Assert.Equal(0,asInterface.GetHashCode64(k1));
        Assert.False(asInterface.Equals(k1,k2));
    }
}

[Collection("Sequential")]
public class TestRelationStore {
    private readonly struct RelationValue(int id,int value):ITuple<int>, IEquatable<RelationValue> {
        private readonly int Value = value;
        public int Key => id;
        public bool Equals(RelationValue other) => this.Key==other.Key&&this.Value==other.Value;
        public override bool Equals(object? obj) => obj is RelationValue other&&this.Equals(other);
        public override int GetHashCode() => HashCode.Combine(this.Key,this.Value);
    }

    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void RelationStore_ctor_Create() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new RelationStore<int,RelationValue>(logDevice,objDevice,MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0.5);
        using var relation = store.Create();

        Assert.True(relation.Add(new RelationValue(1,10)));
        Assert.False(relation.Add(new RelationValue(1,10)));
    }
}
