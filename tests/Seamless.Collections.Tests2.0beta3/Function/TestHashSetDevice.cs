using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// デバイスを型引数で切り替え可能なHashSetテストの基底クラス
/// </summary>
[Collection("Sequential")]
public abstract class TestHashSetDevice<T>(ITestOutputHelper output):Test<T,Collections.Empty>(output) where T : IEquatable<T> {
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
	private HashSetStore<T> CreateStore() {
		var folder = Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
		var LogDevice = new LocalStorageDevice(folder);
		var ObjectLogDevice = new LocalStorageDevice(folder+"_obj");
		return new HashSetStore<T>(LogDevice,ObjectLogDevice,MutableFraction: 0);
	}
	[Fact]
	public void Add_大量データでストレージスピルをテスト() {
		using var store = this.CreateStore();
		using var set = store.Create();
		//メモリサイズを超えるデータを追加してストレージにスピルさせる
		var count = 100000;
		for(var a = 0; a<count; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		Assert.Equal(count,set.GetLongCount());
		//データの整合性確認
		for(var a = 0; a<count; a++) {
			Assert.Contains(this.CreateKey(a),set);
		}
	}
	[Fact]
	public void Add_スピル後の削除と再追加() {
		using var store = this.CreateStore();
		using var set = store.Create();
		//大量データを追加
		var count = 50000;
		for(var a = 0; a<count; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		//半分削除
		for(var a = 0; a<count/2; a++) {
			Assert.True(set.Remove(this.CreateKey(a)));
		}
		Assert.Equal(count/2,set.GetLongCount());
		//削除したキーを再追加
		for(var a = 0; a<count/2; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		Assert.Equal(count,set.GetLongCount());
		//データの整合性確認
		for(var a = 0; a<count; a++) {
			Assert.Contains(this.CreateKey(a),set);
		}
	}
	[Fact]
	public void Add_スピル後のClear() {
		using var store = this.CreateStore();
		using var set = store.Create();
		//大量データを追加
		var count = 50000;
		for(var a = 0; a<count; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		Assert.Equal(count,set.GetLongCount());
		//全削除
		set.Clear();
		Assert.Equal(0,set.GetLongCount());
		//削除後に再追加
		for(var a = 0; a<10; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		Assert.Equal(10,set.GetLongCount());
	}
	[Fact]
	public void Add_スピル後のCompaction() {
		using var store = this.CreateStore();
		using var set = store.Create();
		//大量データを追加
		var count = 50000;
		for(var a = 0; a<count; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		//コンパクション実行
		store.CompactWithLookup();
		//データの整合性確認
		Assert.Equal(count,set.GetLongCount());
		for(var a = 0; a<count; a++) {
			Assert.Contains(this.CreateKey(a),set);
		}
	}
	[Fact]
	public void Add_スピル後のExceptWith() {
		using var store = this.CreateStore();
		using var set = store.Create();
		//大量データを追加
		var count = 50000;
		for(var a = 0; a<count; a++) {
			Assert.True(set.Add(this.CreateKey(a)));
		}
		//別のHashSetを作成
		using var other = store.Create();
		for(var a = 0; a<count/2; a++) {
			other.Add(this.CreateKey(a));
		}
		//ExceptWithで差集合
		set.ExceptWith(other);
		Assert.Equal(count/2,set.GetLongCount());
		//データの整合性確認
		for(var a = 0; a<count/2; a++) {
			Assert.DoesNotContain(this.CreateKey(a),set);
		}
		for(var a = count/2; a<count; a++) {
			Assert.Contains(this.CreateKey(a),set);
		}
	}
}
