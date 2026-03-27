using System.Collections;
using Tsavorite.core;
using Xunit;

namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class RelationAndEnumerable {
    private readonly struct RelationValue(int id,int value):ITuple<int>, IEquatable<RelationValue> {
        private readonly int Value = value;
        public int Key => id;
        public bool Equals(RelationValue other) => this.Key==other.Key&&this.Value==other.Value;
        public override bool Equals(object? obj) => obj is RelationValue other&&this.Equals(other);
        public override int GetHashCode() => HashCode.Combine(this.Key,this.Value);
    }

    [Fact, Trait("Category","Coverage")]
    public void Relation_AddAndEnumerate() {
        using var store = new RelationStore<int,RelationValue>();
        using var relation = store.Create();
        Assert.True(relation.Add(new RelationValue(1,10)));
        Assert.False(relation.Add(new RelationValue(1,10)));
        Assert.True(relation.Add(new RelationValue(2,20)));

        var genericCount = 0;
        foreach(var item in relation) {
            Assert.True(item.Equals(new RelationValue(1,10))||item.Equals(new RelationValue(2,20)));
            genericCount++;
        }
        Assert.Equal(2,genericCount);

        var nonGenericCount = 0;
        foreach(var item in (IEnumerable)relation) {
            Assert.IsType<RelationValue>(item);
            nonGenericCount++;
        }
        Assert.Equal(2,nonGenericCount);
    }

    [Fact, Trait("Category","Coverage")]
    public void InternalEnumerables_GetEnumerator() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");

        var keyEnumerable = new KeyEnumerable<int,string,AddSessionFunctions<int,string>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(dict.Session);
        var valueEnumerable = new ValueEnumerable<int,string,AddSessionFunctions<int,string>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(dict.Session);
        var keyValueEnumerable = new KeyValueEnumerable<int,string,AddSessionFunctions<int,string>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(dict.Session);

        using(var enumerator = keyEnumerable.GetEnumerator()) {
            Assert.True(enumerator.MoveNext());
            Assert.True(enumerator.Current==1||enumerator.Current==2);
            enumerator.Reset();
            Assert.True(enumerator.MoveNext());
        }
        Assert.Equal(2,((IEnumerable<int>)keyEnumerable).Count());
        {
            IEnumerable nonGeneric = keyEnumerable;
            var c = 0;
            foreach(var _ in nonGeneric) c++;
            Assert.Equal(2,c);
        }

        using(var enumerator = valueEnumerable.GetEnumerator()) {
            Assert.True(enumerator.MoveNext());
            Assert.True(enumerator.Current=="a"||enumerator.Current=="b");
        }
        Assert.Equal(2,((IEnumerable<string>)valueEnumerable).Count());
        {
            IEnumerable nonGeneric = valueEnumerable;
            var c = 0;
            foreach(var _ in nonGeneric) c++;
            Assert.Equal(2,c);
        }

        using(var enumerator = keyValueEnumerable.GetEnumerator()) {
            Assert.True(enumerator.MoveNext());
            Assert.True(enumerator.Current.Key==1||enumerator.Current.Key==2);
        }
        Assert.Equal(2,((IEnumerable<KeyValuePair<int,string>>)keyValueEnumerable).Count());
        {
            IEnumerable nonGeneric = keyValueEnumerable;
            var c = 0;
            foreach(var _ in nonGeneric) c++;
            Assert.Equal(2,c);
        }
    }
}