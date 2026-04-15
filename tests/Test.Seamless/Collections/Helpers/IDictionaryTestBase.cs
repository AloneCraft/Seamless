using System.Collections;
namespace Test.Seamless.Collections;

/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> インターフェース経由でテストする汎用ベースクラス。
/// <typeparamref name="TStrategy"/> の静的ポリモーフィズムにより具体型を差し替える。
/// <para>
/// <c>TryAdd</c>・<c>Remove(key, out value)</c>・<c>EntryCount</c> など
/// <c>ISeamlessDictionary</c> 固有メソッドは使用しない。
/// </para>
/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    [Fact, Trait("Category", "Unit")]
    public void IsReadOnly()
    {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey, TValue> dict = sh.NewDict();

        {
            // 読み取り専用ではないこと
            Assert.False(dict.IsReadOnly);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Add()
    {
        {
            // キーと値の追加が成功すること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(1), TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 重複キーの追加で例外が発生すること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(1), TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1), TStrategy.V(2)));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_KeyValuePair()
    {
        {
            // KeyValuePair で追加できること
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(5), TStrategy.V(5)));
            Assert.True(((IDictionary<TKey, TValue>)col).ContainsKey(TStrategy.K(5)));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Contains()
    {
        {
            // キーと値が一致するペアを保持している場合は true
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1)));
            Assert.True(col.Contains(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1))));
        }
        {
            // 存在しないペアは false
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            Assert.False(col.Contains(new KeyValuePair<TKey, TValue>(TStrategy.K(99), TStrategy.VWrong())));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void CopyTo()
    {
        {
            // 配列へのコピーが成功すること
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1)));
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(2), TStrategy.V(2)));
            var array = new KeyValuePair<TKey, TValue>[2];
            col.CopyTo(array, 0);
            Assert.Equal(2, array.Length);
        }
        {
            // null 配列は例外
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            Assert.Throws<ArgumentNullException>(() => col.CopyTo(null!, 0));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void GetEnumerator()
    {
        {
            // ジェネリック列挙で全件走査できること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(1), TStrategy.V(1));
            dict.Add(TStrategy.K(2), TStrategy.V(2));
            var list = new List<KeyValuePair<TKey, TValue>>();
            foreach (var kv in dict)
                list.Add(kv);
            Assert.Equal(2, list.Count);
        }
        {
            // 非ジェネリック列挙で走査できること
            using var sh = TStrategy.CreateMemoryStore();
            IEnumerable dict = sh.NewDict();
            ((IDictionary<TKey, TValue>)dict).Add(TStrategy.K(1), TStrategy.V(1));
            var count = 0;
            foreach (var _ in dict)
                count++;
            Assert.Equal(1, count);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Keys()
    {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey, TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));

        {
            // 追加したキーが列挙されること
            Assert.Contains(TStrategy.K(1), dict.Keys);
            Assert.Contains(TStrategy.K(2), dict.Keys);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Values()
    {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey, TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));

        {
            // 追加した値が列挙されること
            Assert.Contains(TStrategy.V(1), dict.Values);
            Assert.Contains(TStrategy.V(2), dict.Values);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void ContainsKey()
    {
        {
            // 存在キーは true
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(1), TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 不存在キーは false
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void TryGetValue()
    {
        {
            // キーが一致するときのテスト
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(3), TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3), out var val));
            Assert.Equal(TStrategy.V(3), val);
        }
        {
            // キーが一致しないときのテスト
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            Assert.False(dict.TryGetValue(TStrategy.K(0), out var val));
            Assert.Equal(default, val);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Item()
    {
        {
            // setter で値を設定できること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict[TStrategy.K(10)] = TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10), out var v));
            Assert.Equal(TStrategy.V(10), v);
        }
        {
            // 不存在キー getter は例外
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            Assert.Throws<KeyNotFoundException>(() => { var _ = dict[TStrategy.K(999)]; });
        }
        {
            // 既存キーの setter は上書きされること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict[TStrategy.K(1)] = TStrategy.V(1);
            dict[TStrategy.K(1)] = TStrategy.V(2);
            Assert.Equal(TStrategy.V(2), dict[TStrategy.K(1)]);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove()
    {
        {
            // 存在するキーは削除されること
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            dict.Add(TStrategy.K(1), TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 存在しないキーは false を返すこと
            using var sh = TStrategy.CreateMemoryStore();
            IDictionary<TKey, TValue> dict = sh.NewDict();
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_KeyValuePair()
    {
        {
            // キーと値が一致する場合のみ削除されること
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1)));
            Assert.True(col.Remove(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1))));
            Assert.False(((IDictionary<TKey, TValue>)col).ContainsKey(TStrategy.K(1)));
        }
        {
            // 値が不一致なら削除されないこと
            using var sh = TStrategy.CreateMemoryStore();
            ICollection<KeyValuePair<TKey, TValue>> col = sh.NewDict();
            col.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1)));
            Assert.False(col.Remove(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.VWrong())));
            Assert.True(((IDictionary<TKey, TValue>)col).ContainsKey(TStrategy.K(1)));
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Clear()
    {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey, TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));

        {
            // Clear 後に件数が 0 になること
            dict.Clear();
            Assert.Empty(dict);
        }
    }

    [Fact, Trait("Category", "Unit")]
    public void Count()
    {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey, TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));

        {
            // 追加件数を返すこと
            Assert.Equal(2, dict.Count);
        }
    }
}
