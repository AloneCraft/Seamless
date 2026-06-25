using Seamless.Tsavorite.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Tsavorite.core;
using Seamless.Collections;
namespace Tsavorite.Collection.Shared;
/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> ç¹§E¤ç¹ï½³ç¹§E¿ç¹ï½¼ç¹è¼”ã‰ç¹ï½¼ç¹§E¹é‚¨æªç½°ç¸ºE®ç¹ãEã›ç¹äº¥æ¸•è è¼”ã‘ç¹ï½©ç¹§E¹ç¸²ãƒ»/// <see cref="DictionaryTestBase{TStrategy,TKey,TValue}"/> ç¸ºE¯ç¸ºè–™ãEç¹§E¯ç¹ï½©ç¹§E¹ç¹§è²èŒœé€•ï½¨ç¸ºåŠ±Â€ãƒ»/// <see cref="global::Seamless.Tsavorite.Collections.IDictionary{TKey,TValue}"/> è—ï½ºè­›å³¨ãƒ»ç¹ãEã›ç¹åŒ»E’éœ‘E½èœ‰ï£°ç¸ºå¶E‹ç¸²ãƒ»/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [BenchFact,Benchmark]
    public void IsReadOnly() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        {
            // éš±E­ç¸ºE¿èœ¿æ‚¶EŠèŸ†ã‚‰ç•‘ç¸ºE§ç¸ºE¯ç¸ºEªç¸ºãƒ»EE¸ºE¨
            Assert.False(dict.IsReadOnly);
        }
    }
    [BenchFact]
    public void Add() {
        {
            // éœ‘ï½½èœ‰ï£°ç¸ºE§ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // é©¥å´ï½¤ãƒ»ãç¹ï½¼ç¸ºE§ç¸ºE¯è“å¥E¤æ‚¶â†“ç¸ºEªç¹§ä¹ï¼E¸ºE¨
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
        }
        {
            // ç¹ï½­ç¹§E°ç¸ºæ¢§EºÂ€è­šï½¯ç¸ºE«ç¸ºEªç¹§ä¹âEè“å¥E¤æ‚¶â†“ç¸ºEªç¹§ä¹ï¼E¸ºE¨
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [BenchFact,Benchmark]
    public void Add_KeyValuePair() {
        {
            // KeyValuePair ç¸ºE§éœ‘ï½½èœ‰ï£°ç¸ºE§ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(5),TStrategy.V(5)));
            Assert.True(dict.ContainsKey(TStrategy.K(5)));
        }
    }
    [BenchFact,Benchmark]
    public void Contains() {
        {
            // ç¹§E­ç¹ï½¼ç¸ºE¨è›Ÿï½¤ç¸ºå¾¡E¸Â€é–¾E´ç¸ºå¶E‹éš•âˆ«E´E°ç¸ºE¯ true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        }
        {
            // è³å ºE¸Â€é–¾E´éš•âˆ«E´E°ç¸ºE¯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
        }
    }
    [BenchFact]
    public void CopyTo() {
        {
            // èœˆï½¨éš•âˆ«E´E°ç¹§å¸ãEè›»åŠ±â†“ç¹§E³ç¹æ–ãƒ»ç¸ºE§ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(10),TStrategy.V(10));
            var array = new KeyValuePair<TKey,TValue>[2];
            dict.CopyTo(array,0);
            Assert.Equal(2,array.Length);
        }
        {
            // null é©Ÿæ¦ŠãEç¸ºE¯è“å¥E¤ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
        }
        {
            // é›‹ï£°ç¸ºE®ç¹§E¤ç¹ï½³ç¹ãEãƒ£ç¹§E¯ç¹§E¹ç¸ºE¯è“å¥E¤ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[2],-1));
        }
        {
            // é©Ÿæ¦ŠãEç¸ºæ‚Ÿï½°ä¸Šï¼E¸ºå¶â„E¹§ä¹âEè“å¥E¤ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(2),TStrategy.V(2));
            Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[1],0));
        }
    }
    [BenchFact,Benchmark]
    public void ToList() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        var list = dict.ToList();
        {
            // èœˆï½¨ç¹§E¨ç¹ï½³ç¹åŒ»Îœç¹§å®šï½¿æ–âEç¸ºè–™âE
            Assert.Equal(2,list.Count);
        }
    }
    [BenchFact]
    public void Keys() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // ç¹§E­ç¹ï½¼è³Â€éš•ï½§ç¹§è²å™è •åŠ±ã€’ç¸ºé˜ªE‹ç¸ºè–™âE
            Assert.True(new global::System.Collections.Generic.HashSet<TKey>(new[] { TStrategy.K(1),TStrategy.K(2) }).SetEquals(dict.Keys));
        }
        {
            // ç¹§E­ç¹ï½¼è³Â€éš•ï½§ç¸ºE¯èœˆï½¨è‰ï½¶ç¹§E³ç¹æ–ãƒ»è²‚åŒ»âˆ©ç¸ºE®è®“å‘Eºè±Šistç¸ºE§ç¸ºE¯ç¸ºEªç¸ºå‰°EªE­ç¸ºE¿èœ¿æ‚¶EŠèŸ†ã‚‰ç•‘viewç¸ºE§ç¸ºã‚E½‹ç¸ºè–™âE
            Assert.True(dict.Keys.IsReadOnly);
            Assert.IsNotType<List<TKey>>(dict.Keys);
            Assert.Throws<NotSupportedException>(() => dict.Keys.Add(TStrategy.K(3)));
        }
    }
    [BenchFact]
    public void Values() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // è›Ÿï½¤è³Â€éš•ï½§ç¹§è²å™è •åŠ±ã€’ç¸ºé˜ªE‹ç¸ºè–™âE
            Assert.True(new global::System.Collections.Generic.HashSet<TValue>(new[] { TStrategy.V(1),TStrategy.V(2) }).SetEquals(dict.Values));
        }
        {
            // è›Ÿï½¤è³Â€éš•ï½§ç¸ºE¯èœˆï½¨è‰ï½¶ç¹§E³ç¹æ–ãƒ»è²‚åŒ»âˆ©ç¸ºE®è®“å‘Eºè±Šistç¸ºE§ç¸ºE¯ç¸ºEªç¸ºå‰°EªE­ç¸ºE¿èœ¿æ‚¶EŠèŸ†ã‚‰ç•‘viewç¸ºE§ç¸ºã‚E½‹ç¸ºè–™âE
            Assert.True(dict.Values.IsReadOnly);
            Assert.IsNotType<List<TValue>>(dict.Values);
            Assert.Throws<NotSupportedException>(() => dict.Values.Add(TStrategy.V(3)));
        }
    }
    [BenchFact,Benchmark]
    public void ContainsKey() {
        {
            // èŸE¼œæƒ ç¸ºå¶E‹ç¹§E­ç¹ï½¼ç¸ºE¯ true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // èŸE¼œæƒ ç¸ºåŠ±â†‘ç¸ºãƒ»ãç¹ï½¼ç¸ºE¯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }
    [BenchFact,Benchmark]
    public void TryGetValue() {
        {
            // ç¹§E­ç¹ï½¼ç¸ºæ‚Ÿï½­ä¼œæƒ ç¸ºå¶E‹è­ã‚…ãƒ»ç¹ãEã›ç¹ãE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(3),TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
            Assert.Equal(TStrategy.V(3),val);
        }
        {
            // ç¹§E­ç¹ï½¼ç¸ºæ‚Ÿï½­ä¼œæƒ ç¸ºåŠ±â†‘ç¸ºãƒ»å‡¾ç¸ºE®ç¹ãEã›ç¹ãE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
            Assert.Equal(default,val);
        }
        {
            // éœ‘ï½½èœ‰ï£°é€¶E´è •å¾Œã€’ç¹§ã‚Šï½ªE­ç¸ºE¿èœE½ºç¸ºå¸™ï½‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.TryGetValue(TStrategy.K(1),out var v));
            Assert.Equal(TStrategy.V(1),v);
        }
        {
            // FlushAndEvict è •å¾Œï½‚ç¹§E³ç¹ï½¼ç¹ï½«ç¹å³¨ãƒ§ç¹ï½¼ç¹§E¿ç¹§å®šï½ªE­ç¹§âˆšï½‹ç¸ºè–™âE
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.True(dict.TryGetValue(TStrategy.K(0),out var v));
            Assert.Equal(TStrategy.V(0),v);
        }
    }
    [BenchFact]
    public void Item() {
        {
            // setter ç¸ºE§è›Ÿï½¤ç¹§å®šï½¨E­è³å£¹ã€’ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict[TStrategy.K(10)]=TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
            Assert.Equal(TStrategy.V(10),v);
        }
        {
            // getter ç¸ºE§è³æ¦Šï½­ä¼œæƒ ç¹§E­ç¹ï½¼èœ¿ã‚‰ãEè­ã‚…ãƒ»è“å¥E¤ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<KeyNotFoundException>(() => {
                var _ = dict[TStrategy.K(999)];
            });
        }
        {
            // ç¹ï½­ç¹§E°è²E€è­šï½¯è­ã‚…ãƒ» setter ç¸ºE¯è“å¥E¤ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict[TStrategy.K(i)]=TStrategy.V(i);
            }));
        }
    }
    [BenchFact,Benchmark]
    public void Remove() {
        {
            // èŸE¼œæƒ ç¹§E­ç¹ï½¼ç¸ºE®èœ‘ä¼å‹ç¸ºE«è¬Œä»™ç²¥ç¸ºå¶E‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // èŸE¼œæƒ ç¸ºåŠ±â†‘ç¸ºãƒ»ãç¹ï½¼ç¸ºE¯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
        {
            // Write ç¹ä»£ã›ç¸ºE®èœ‘ä¼å‹ç¸ºE«è¬Œä»™ç²¥ç¸ºå¶E‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove_KeyValuePair() {
        {
            // ç¹§E­ç¹ï½¼ç¸ºE¨è›Ÿï½¤ç¸ºå¾¡E¸Â€é–¾E´ç¸ºå¶E‹ç¸ºE¨èœ‘ä¼å‹ç¸ºè¼”ï½Œç¹§ãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
            foreach(var _ in dict) { }
            Assert.True(removed);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // è›Ÿï½¤ç¸ºå¾¡E¸å ºE¸Â€é–¾E´ç¸ºEªç¹§ç‰™ç‚é«¯E¤ç¸ºè¼”ï½Œç¸ºEªç¸ºãƒ»
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong()));
            Assert.False(removed);
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // ç¹§E­ç¹ï½¼ç¸ºæ‚Ÿï½­ä¼œæƒ ç¸ºåŠ±â†‘ç¸ºä»£EŒç¸ºE° false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong()));
            Assert.False(removed);
        }
    }
    [BenchFact,Benchmark]
    public void Clear() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // ç¹§E¯ç¹ï½ªç¹§E¢è •å¾ŒãEè‰ï½¶è¬¨E°ç¸ºãƒ»0 ç¸ºE«ç¸ºEªç¹§ä¹ï¼E¸ºE¨
            dict.Clear();
            Assert.Empty(dict);
        }
    }
    [BenchFact,Benchmark]
    public void Count() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // éœ‘ï½½èœ‰ï£°è¬¨E°ç¸ºE¨è³Â€é–¾E´ç¸ºå¶E‹ç¸ºè–™âE
            Assert.Equal(2,dict.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // IEnumerable<T> é‚¨æªç½°ç¸ºE§è›»ç²å«Œç¸ºE§ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable<KeyValuePair<TKey,TValue>> e = dict;
            Assert.Single(e);
        }
        {
            // IEnumerable é‚¨æªç½°ç¸ºE§è›»ç²å«Œç¸ºE§ç¸ºé˜ªE‹ç¸ºè–™âE
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            global::System.Collections.IEnumerable e = dict;
            var count = 0;
            var en = e.GetEnumerator();
            while(en.MoveNext()) count++;
            Assert.Equal(1,count);
        }
    }
    [BenchFact]
    [SuppressMessage("Interoperability","CA1416:ç¹åŠ±Î›ç¹ãEãƒ¨ç¹è¼”ã‹ç¹ï½¼ç¹ï£°ç¸ºE®è å‘ˆé‹¤è«¤E§",Justification = "<è«æ™‰è•—è³E­>")]
    public void WouldOverflowMutable() {
        {
            // mutableFraction=0 ç¸ºE§ç¹§ã‚ˆï£°E¼é‚é˜ªã€’ç¸ºé˜ªE‹ç¸ºè–™âE
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice,memorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,mutableFraction: 0);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TStrategy.V(1),dict[TStrategy.K(1)]);
        }
        {
            // mutable é¬E¼œæ²ºç¹§å‰E½½E¿ç¸ºãƒ»ãƒ»ç¹§ä¹âE OutOfMemoryException
            using var sh = TStrategy.CreateMemoryStore(memorySize: global::Seamless.Tsavorite.Const.LogMemorySize,mutableFraction: 0.5);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [BenchFact,Benchmark]
    [SuppressMessage("Interoperability","CA1416:ç¹åŠ±Î›ç¹ãEãƒ¨ç¹è¼”ã‹ç¹ï½¼ç¹ï£°ç¸ºE®è å‘ˆé‹¤è«¤E§",Justification = "<è«æ™‰è•—è³E­>")]
    public void SaveLoad() {
        var folder = CreateDiskFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir);
            using var rawSet = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> set = rawSet;
            set[TStrategy.K(1)]=TStrategy.V(1);
            set[TStrategy.K(2)]=TStrategy.V(2);
            set[TStrategy.K(3)]=TStrategy.V(3);
            Assert.Equal(3,set.Count);
            Assert.True(set.ContainsKey(TStrategy.K(1)));
            token=sh.Save();
            Assert.True(set.ContainsKey(TStrategy.K(1)));
        }
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir,recoverDevice: true);
            sh.Load(token);
            var (begin2,head2,ro2,tail2)=sh.Address;
            using var rawSet = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> set = rawSet;
            Assert.True(set.ContainsKey(TStrategy.K(1)),$"ContainsKey(K(1)) failed. Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.ContainsKey(TStrategy.K(2)));
            Assert.True(set.ContainsKey(TStrategy.K(3)));
            Assert.False(set.ContainsKey(TStrategy.K(99)));
        }
    }
}
