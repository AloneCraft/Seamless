using System.Collections;
using Seamless.Collections;
using Seamless.Stores;

namespace Test.Seamless.Collections;

public class UnmanagedDictionary {
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly() {
        using var store = Factory.Create().Create<int,int>();
        using var subject = store.Create();

        Assert.False(subject.IsReadOnly);
    }
    [Fact, Trait("Category","Unit")]
    public void Add_item() {
        {
            //KeyValuePair から追加できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            subject.Add(new KeyValuePair<int,int>(1,10));

            Assert.True(subject.TryGetValue(1,out var value));
            Assert.Equal(10,value);
        }
        {
            //KeyValuePair の重複キーで例外を送出することのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(new KeyValuePair<int,int>(1,10));

            Assert.Throws<ArgumentException>(() => subject.Add(new KeyValuePair<int,int>(1,20)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ICollectionT_Contains() {
        {
            //キーと値が一致するときのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            ICollection<KeyValuePair<int,int>> collection = subject;
            subject.Add(1,10);

            var contains = collection.Contains(new KeyValuePair<int,int>(1,10));

            Assert.True(contains);
        }
        {
            //値が一致しないときのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            ICollection<KeyValuePair<int,int>> collection = subject;
            subject.Add(1,10);

            var contains = collection.Contains(new KeyValuePair<int,int>(1,20));

            Assert.False(contains);
        }
        {
            //キーが存在しないときのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            ICollection<KeyValuePair<int,int>> collection = subject;

            var contains = collection.Contains(new KeyValuePair<int,int>(99,10));

            Assert.False(contains);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo() {
        {
            //配列へコピーできることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);
            var array = new KeyValuePair<int,int>[3];

            subject.CopyTo(array,1);

            var copied = array.Skip(1).Where(static pair => pair.Key!=0).ToDictionary(static pair => pair.Key,static pair => pair.Value);
            Assert.Equal(2,copied.Count);
            Assert.Equal(10,copied[1]);
            Assert.Equal(20,copied[2]);
        }
        {
            //null 配列のテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            Assert.Throws<ArgumentNullException>(() => subject.CopyTo(null!,0));
        }
        {
            //負の index のテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => subject.CopyTo(new KeyValuePair<int,int>[1],-1));
        }
        {
            //配列が小さすぎるときのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);

            Assert.Throws<ArgumentException>(() => subject.CopyTo(new KeyValuePair<int,int>[1],0));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IEnumerable_GetEnumerator() {
        {
            //非ジェネリック IEnumerable 経由で列挙できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);
            IEnumerable enumerable = subject;

            var enumerator = enumerable.GetEnumerator();
            var actual = new Dictionary<int,int>();
            while(enumerator.MoveNext()) {
                var pair = (KeyValuePair<int,int>)enumerator.Current!;
                actual.Add(pair.Key,pair.Value);
            }

            Assert.Equal(2,actual.Count);
            Assert.Equal(10,actual[1]);
            Assert.Equal(20,actual[2]);
        }
        {
            //空のときに列挙要素がないことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            IEnumerable enumerable = subject;

            var enumerator = enumerable.GetEnumerator();

            Assert.False(enumerator.MoveNext());
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IEnumerableT_GetEnumerator() {
        {
            //generic IEnumerable 経由で列挙できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);
            IEnumerable<KeyValuePair<int,int>> enumerable = subject;

            using var enumerator = enumerable.GetEnumerator();
            var actual = new Dictionary<int,int>();
            while(enumerator.MoveNext()) {
                actual.Add(enumerator.Current.Key,enumerator.Current.Value);
            }

            Assert.Equal(2,actual.Count);
            Assert.Equal(10,actual[1]);
            Assert.Equal(20,actual[2]);
        }
        {
            //空のときに列挙要素がないことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            IEnumerable<KeyValuePair<int,int>> enumerable = subject;

            using var enumerator = enumerable.GetEnumerator();

            Assert.False(enumerator.MoveNext());
        }
    }
	[Fact, Trait("Category","Unit")]
	public void ToList() {
		{
			//全エントリを List 化できることのテスト
			using var store = Factory.Create().Create<int,int>();
			using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);

			var list = subject.ToList();
			var actual = list.ToDictionary(static pair => pair.Key,static pair => pair.Value);

			Assert.Equal(2,actual.Count);
            Assert.Equal(10,actual[1]);
            Assert.Equal(20,actual[2]);
		}
		{
			//空のときは空 List を返すことのテスト
			using var store = Factory.Create().Create<int,int>();
			using var subject = store.Create();

			var list = subject.ToList();

			Assert.Empty(list);
		}
	}
    [Fact, Trait("Category","Unit")]
    public void Keys() {
        {
            //Keys でキー一覧を取得できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);

            var keys = subject.Keys;

            Assert.Equal(2,keys.Count);
            Assert.Contains(1,keys);
            Assert.Contains(2,keys);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Values() {
        {
            //Values で値一覧を取得できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);

            var values = subject.Values;

            Assert.Equal(2,values.Count);
            Assert.Contains(10,values);
            Assert.Contains(20,values);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Keys_IReadOnlyDictionary() {
        //IReadOnlyDictionary 経由でキー一覧を取得できることのテスト
        using var store = Factory.Create().Create<int,int>();
        using var subject = store.Create();
        subject.Add(1,10);
        subject.Add(2,20);
        IReadOnlyDictionary<int,int> dictionary = subject;

        var keys = dictionary.Keys;

        Assert.Equal(2,keys.Count());
        Assert.Contains(1,keys);
        Assert.Contains(2,keys);
    }
    [Fact, Trait("Category","Unit")]
    public void Values_IReadOnlyDictionary() {
        //IReadOnlyDictionary 経由で値一覧を取得できることのテスト
        using var store = Factory.Create().Create<int,int>();
        using var subject = store.Create();
        subject.Add(1,10);
        subject.Add(2,20);
        IReadOnlyDictionary<int,int> dictionary = subject;

        var values = dictionary.Values;

        Assert.Equal(2,values.Count());
        Assert.Contains(10,values);
        Assert.Contains(20,values);
    }
    [Fact, Trait("Category","Unit")]
    public void IsDisposed() {
        using var store = Factory.Create().Create<int,int>();
        var subject = store.Create();

        Assert.False(subject.IsDisposed);

        subject.Dispose();

        Assert.True(subject.IsDisposed);
    }

    [Fact, Trait("Category","Unit")]
    public void Dispose() {
        {
            //Dispose すると破棄済み状態になることのテスト
            using var store = Factory.Create().Create<int,int>();
            var subject = store.Create();

            subject.Dispose();

            Assert.True(subject.IsDisposed);
        }
        {
            //複数回 Dispose しても安全なことのテスト
            using var store = Factory.Create().Create<int,int>();
            var subject = store.Create();

            subject.Dispose();
            subject.Dispose();

            Assert.True(subject.IsDisposed);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_item() {
        {
            //キーと値が一致するときに削除できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);

            var removed = subject.Remove(new KeyValuePair<int,int>(1,10));

            Assert.True(removed);
            Assert.False(subject.TryGetValue(1,out _));
        }
        {
            //値が一致しないときに削除しないことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);

            var removed = subject.Remove(new KeyValuePair<int,int>(1,20));

            Assert.False(removed);
            Assert.Equal(10,subject[1]);
        }
        {
            //キーが存在しないときに false を返すことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            var removed = subject.Remove(new KeyValuePair<int,int>(99,10));

            Assert.False(removed);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_key_value() {
        {
            //キーが存在するときに値を返して削除することのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);

            var removed = subject.Remove(1,out var value);

            Assert.True(removed);
            Assert.Equal(10,value);
            Assert.False(subject.TryGetValue(1,out _));
        }
        {
            //キーが存在しないときに false と default を返すことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            var removed = subject.Remove(99,out var value);

            Assert.False(removed);
            Assert.Equal(default,value);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void GetEnumerator() {
        {
            //public GetEnumerator で列挙できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);
            subject.Add(2,20);

            using var enumerator = subject.GetEnumerator();
            var actual = new Dictionary<int,int>();
            while(enumerator.MoveNext()) {
                actual.Add(enumerator.Current.Key,enumerator.Current.Value);
            }

            Assert.Equal(2,actual.Count);
            Assert.Equal(10,actual[1]);
            Assert.Equal(20,actual[2]);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void get_Item() {
        {
            //キーが存在するときに値を返すことのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);

            var value = subject[1];

            Assert.Equal(10,value);
        }
        {
            //キーが存在しないときに例外を送出することのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            Assert.Throws<KeyNotFoundException>(() => subject[99]);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void set_Item() {
        {
            //新規キーへ値を設定できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();

            subject[1]=10;

            Assert.True(subject.TryGetValue(1,out var value));
            Assert.Equal(10,value);
        }
        {
            //既存キーの値を更新できることのテスト
            using var store = Factory.Create().Create<int,int>();
            using var subject = store.Create();
            subject.Add(1,10);

            subject[1]=20;

            Assert.Equal(20,subject[1]);
        }
        {
            //LogFull で OutOfMemoryException を送出することのテスト
            using var store = Factory.Create(MemorySize: global::Seamless.Const.LogMemorySize,MutableFraction: 0.5).Create<long,long>();
            using var subject = store.Create();

            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(long i = 0; ; i++) {
                    subject[i]=i;
                }
            }));
        }
    }
}
