using System.Collections;
using Seamless.Collections;
using Seamless.Stores;

namespace Test.Seamless.Collections;

public partial class ManagedDictionary {
	[Fact, Trait("Category","Unit")]
	public void IsReadOnly() {
		using var store = Factory.Create().Create<string,string>();
		using var subject = store.Create();

		Assert.False(subject.IsReadOnly);
	}
	[Fact, Trait("Category","Unit")]
	public void Add_item() {
		{
			//KeyValuePair から追加できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			subject.Add(new KeyValuePair<string,string>("k1","v1"));

			Assert.True(subject.TryGetValue("k1",out var value));
			Assert.Equal("v1",value);
		}
		{
			//KeyValuePair の重複キーで例外を送出することのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add(new KeyValuePair<string,string>("k1","v1"));

			Assert.Throws<ArgumentException>(() => subject.Add(new KeyValuePair<string,string>("k1","v2")));
		}
	}
	[Fact, Trait("Category","Unit")]
	public void ICollectionT_Contains() {
		{
			//キーと値が一致するときのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			ICollection<KeyValuePair<string,string>> collection = subject;
			subject.Add("k1","v1");

			var contains = collection.Contains(new KeyValuePair<string,string>("k1","v1"));

			Assert.True(contains);
		}
		{
			//値が一致しないときのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			ICollection<KeyValuePair<string,string>> collection = subject;
			subject.Add("k1","v1");

			var contains = collection.Contains(new KeyValuePair<string,string>("k1","v2"));

			Assert.False(contains);
		}
		{
			//キーが存在しないときのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			ICollection<KeyValuePair<string,string>> collection = subject;

			var contains = collection.Contains(new KeyValuePair<string,string>("missing","v1"));

			Assert.False(contains);
		}
	}
	[Fact, Trait("Category","Unit")]
	public void CopyTo() {
		{
			//配列へコピーできることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");
			var array = new KeyValuePair<string,string>[3];

			subject.CopyTo(array,1);

			var copied = array.Skip(1).Where(static pair => pair.Key is not null).ToDictionary(static pair => pair.Key,static pair => pair.Value);
			Assert.Equal(2,copied.Count);
			Assert.Equal("v1",copied["k1"]);
			Assert.Equal("v2",copied["k2"]);
		}
		{
			//null 配列のテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			Assert.Throws<ArgumentNullException>(() => subject.CopyTo(null!,0));
		}
		{
			//負の index のテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			Assert.Throws<ArgumentOutOfRangeException>(() => subject.CopyTo(new KeyValuePair<string,string>[1],-1));
		}
		{
			//配列が小さすぎるときのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");

			Assert.Throws<ArgumentException>(() => subject.CopyTo(new KeyValuePair<string,string>[1],0));
		}
	}
	[Fact, Trait("Category","Unit")]
	public void IEnumerable_GetEnumerator() {
		{
			//非ジェネリック IEnumerable 経由で列挙できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");
			IEnumerable enumerable = subject;

			var enumerator = enumerable.GetEnumerator();
			var actual = new Dictionary<string,string>();
			while(enumerator.MoveNext()) {
				var pair = (KeyValuePair<string,string>)enumerator.Current!;
				actual.Add(pair.Key,pair.Value);
			}

			Assert.Equal(2,actual.Count);
			Assert.Equal("v1",actual["k1"]);
			Assert.Equal("v2",actual["k2"]);
		}
		{
			//空のときに列挙要素がないことのテスト
			using var store = Factory.Create().Create<string,string>();
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
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");
			IEnumerable<KeyValuePair<string,string>> enumerable = subject;

			using var enumerator = enumerable.GetEnumerator();
			var actual = new Dictionary<string,string>();
			while(enumerator.MoveNext()) {
				actual.Add(enumerator.Current.Key,enumerator.Current.Value);
			}

			Assert.Equal(2,actual.Count);
			Assert.Equal("v1",actual["k1"]);
			Assert.Equal("v2",actual["k2"]);
		}
		{
			//空のときに列挙要素がないことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			IEnumerable<KeyValuePair<string,string>> enumerable = subject;

			using var enumerator = enumerable.GetEnumerator();

			Assert.False(enumerator.MoveNext());
		}
	}
	[Fact, Trait("Category","Unit")]
	public void ToList() {
		{
			//全エントリを List 化できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");

			var list = subject.ToList();
			var actual = list.ToDictionary(static pair => pair.Key,static pair => pair.Value);

			Assert.Equal(2,actual.Count);
			Assert.Equal("v1",actual["k1"]);
			Assert.Equal("v2",actual["k2"]);
		}
		{
			//空のときは空 List を返すことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			var list = subject.ToList();

			Assert.Empty(list);
		}
	}
	[Fact, Trait("Category","Unit")]
	public void Keys() {
		{
			//Keys でキー一覧を取得できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");

			var keys = subject.Keys;

			Assert.Equal(2,keys.Count);
			Assert.Contains("k1",keys);
			Assert.Contains("k2",keys);
		}
	}

	[Fact, Trait("Category","Unit")]
	public void Values() {
		{
			//Values で値一覧を取得できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");

			var values = subject.Values;

			Assert.Equal(2,values.Count);
			Assert.Contains("v1",values);
			Assert.Contains("v2",values);
		}
	}
	[Fact, Trait("Category","Unit")]
	public void Keys_IReadOnlyDictionary() {
		//IReadOnlyDictionary 経由でキー一覧を取得できることのテスト
		using var store = Factory.Create().Create<string,string>();
		using var subject = store.Create();
		subject.Add("k1","v1");
		subject.Add("k2","v2");
		IReadOnlyDictionary<string,string> dictionary = subject;

		var keys = dictionary.Keys;

		Assert.Equal(2,keys.Count());
		Assert.Contains("k1",keys);
		Assert.Contains("k2",keys);
	}
	[Fact, Trait("Category","Unit")]
	public void Values_IReadOnlyDictionary() {
		//IReadOnlyDictionary 経由で値一覧を取得できることのテスト
		using var store = Factory.Create().Create<string,string>();
		using var subject = store.Create();
		subject.Add("k1","v1");
		subject.Add("k2","v2");
		IReadOnlyDictionary<string,string> dictionary = subject;

		var values = dictionary.Values;

		Assert.Equal(2,values.Count());
		Assert.Contains("v1",values);
		Assert.Contains("v2",values);
	}
	[Fact, Trait("Category","Unit")]
	public void IsDisposed() {
		using var store = Factory.Create().Create<string,string>();
		var subject = store.Create();

		Assert.False(subject.IsDisposed);

		subject.Dispose();

		Assert.True(subject.IsDisposed);
	}

	[Fact, Trait("Category","Unit")]
	public void Dispose() {
		{
			//Dispose すると破棄済み状態になることのテスト
			using var store = Factory.Create().Create<string,string>();
			var subject = store.Create();

			subject.Dispose();

			Assert.True(subject.IsDisposed);
		}
		{
			//複数回 Dispose しても安全なことのテスト
			using var store = Factory.Create().Create<string,string>();
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
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");

			var removed = subject.Remove(new KeyValuePair<string,string>("k1","v1"));

			Assert.True(removed);
			Assert.False(subject.TryGetValue("k1",out _));
		}
		{
			//値が一致しないときに削除しないことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");

			var removed = subject.Remove(new KeyValuePair<string,string>("k1","v2"));

			Assert.False(removed);
			Assert.Equal("v1",subject["k1"]);
		}
		{
			//キーが存在しないときに false を返すことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			var removed = subject.Remove(new KeyValuePair<string,string>("missing","v1"));

			Assert.False(removed);
		}
	}

	[Fact, Trait("Category","Unit")]
	public void Remove_key_value() {
		{
			//キーが存在するときに値を返して削除することのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");

			var removed = subject.Remove("k1",out var value);

			Assert.True(removed);
			Assert.Equal("v1",value);
			Assert.False(subject.TryGetValue("k1",out _));
		}
		{
			//キーが存在しないときに false と default を返すことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			var removed = subject.Remove("missing",out var value);

			Assert.False(removed);
			Assert.Null(value);
		}
	}

	[Fact, Trait("Category","Unit")]
	public void GetEnumerator() {
		{
			//public GetEnumerator で列挙できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");
			subject.Add("k2","v2");

			using var enumerator = subject.GetEnumerator();
			var actual = new Dictionary<string,string>();
			while(enumerator.MoveNext()) {
				actual.Add(enumerator.Current.Key,enumerator.Current.Value);
			}

			Assert.Equal(2,actual.Count);
			Assert.Equal("v1",actual["k1"]);
			Assert.Equal("v2",actual["k2"]);
		}
	}

	[Fact, Trait("Category","Unit")]
	public void get_Item() {
		{
			//キーが存在するときに値を返すことのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");

			var value = subject["k1"];

			Assert.Equal("v1",value);
		}
		{
			//キーが存在しないときに例外を送出することのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			Assert.Throws<KeyNotFoundException>(() => subject["missing"]);
		}
	}

	[Fact, Trait("Category","Unit")]
	public void set_Item() {
		{
			//新規キーへ値を設定できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();

			subject["k1"] = "v1";

			Assert.True(subject.TryGetValue("k1",out var value));
			Assert.Equal("v1",value);
		}
		{
			//既存キーの値を更新できることのテスト
			using var store = Factory.Create().Create<string,string>();
			using var subject = store.Create();
			subject.Add("k1","v1");

			subject["k1"] = "v2";

			Assert.Equal("v2",subject["k1"]);
		}
		{
			//LogFull で OutOfMemoryException を送出することのテスト
			using var store = Factory.Create(MemorySize:global::Seamless.Const.LogMemorySize,MutableFraction:0.5).Create<string,string>();
			using var subject = store.Create();

			Assert.Throws<OutOfMemoryException>((Action)(() => {
				for(var i = 0;;i++) {
					subject[new string('k',128) + i] = new string('v',1024);
				}
			}));
		}
	}
}

