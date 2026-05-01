using BindingFlags = System.Reflection.BindingFlags;
using Seamless.Collections;
using Seamless.Stores;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SeamlessHashSet = Seamless.Linq.HashSet;
using SystemEnumerable = System.Linq.Enumerable;
using Test.Seamless.Collections;

namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod2 {
	[Fact, Trait("Category","Intercept")]
	public static void Select_Select_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
			sample.Select_Select(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_Select_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
			sample.Select_Select_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_Where_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
			sample.Select_Where(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_Where_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
			sample.Select_Where_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_index_Select_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value*10),
			sample.Select_index_Select(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_index_Select_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value+index),
			sample.Select_index_Select_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_index_Where_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value>4),
			sample.Select_index_Where(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_index_Where_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1),
			sample.Select_index_Where_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_Select_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
			sample.Where_Select(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_Select_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
			sample.Where_Select_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_Where_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
			sample.Where_Where(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_Where_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
			sample.Where_Where_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_index_Select_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value*10),
			sample.Where_index_Select(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_index_Select_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value+index),
			sample.Where_index_Select_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_index_Where_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value<4),
			sample.Where_index_Where(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Where_index_Where_index_ToArray() {
		using var sample = new ChainSamples();
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2),
			sample.Where_index_Where_index(),
			typeof(UnmanagedList<int>)
		);
	}

	[Fact, Trait("Category","Intercept")]
	public static void Select_Where_TypeVariants_ToArray() {
		using var sample = new ChainSamples();

		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(LongSource(),static value => value*10L),static value => value>20L),
			sample.Select_Where_Int64(),
			typeof(UnmanagedList<long>)
		);
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1),
			sample.Select_Where_String(),
			typeof(ManagedList<string>)
		);
		AssertChain(
			SystemEnumerable.Where(SystemEnumerable.Select(ManagedStructSource(),static value => CreateManagedStruct(value)),static value => value.Name.Length>2),
			sample.Select_Where_ManagedStruct(),
			typeof(ManagedList<ManagedStruct>)
		);
	}

	private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual,Type expectedStorageType)
		where T : IComparable<T> {
		try {
			AssertStorageType(actual,expectedStorageType);
			Assert.Equal(SystemEnumerable.OrderBy(expected,static value => value),SystemEnumerable.OrderBy(actual,static value => value));
		}
		finally {
			if(actual is IDisposable disposable) {
				disposable.Dispose();
			}
		}
	}

	private static void AssertStorageType<T>(IEnumerable<T> actual,Type expectedStorageType) {
		var field = actual.GetType().GetField("list",BindingFlags.Instance|BindingFlags.NonPublic);
		Assert.NotNull(field);
		var list = field.GetValue(actual);
		Assert.NotNull(list);
		Assert.Equal(expectedStorageType,list.GetType());
	}

	internal sealed class ChainSamples : IDisposable {
		private IHashSet<int>? source;
		private IHashSet<long>? longSource;
		private IHashSet<string>? stringSource;
		private IHashSet<ManagedStruct>? managedStructSource;

		internal IHashSet<int> SourceSet => this.source??=CreateUnmanagedSource(Source());
		internal IHashSet<long> LongSourceSet => this.longSource??=CreateUnmanagedSource(LongSource());
		internal IHashSet<string> StringSourceSet => this.stringSource??=CreateManagedSource(StringSource());
		internal IHashSet<ManagedStruct> ManagedStructSourceSet => this.managedStructSource??=CreateManagedSource(ManagedStructSource());

		public IEnumerable<int> Select_Select() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Select(this.SourceSet,static value => value+1),static value => value*10);

		public IEnumerable<int> Select_Select_index() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Select(this.SourceSet,static value => value+1),static (value,index) => value+index);

		public IEnumerable<int> Select_Where() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.SourceSet,static value => value*10),static value => value>20);

		public IEnumerable<int> Select_Where_index() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.SourceSet,static value => value*10),static (value,index) => value>20&&index>0);

		public IEnumerable<int> Select_index_Select() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Select(this.SourceSet,static (value,index) => value+index),static value => value*10);

		public IEnumerable<int> Select_index_Select_index() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Select(this.SourceSet,static (value,index) => value+index),static (value,index) => value+index);

		public IEnumerable<int> Select_index_Where() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.SourceSet,static (value,index) => value+index),static value => value>4);

		public IEnumerable<int> Select_index_Where_index() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.SourceSet,static (value,index) => value+index),static (value,index) => value>3&&index>1);

		public IEnumerable<int> Where_Select() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Where(this.SourceSet,static value => value>1),static value => value*10);

		public IEnumerable<int> Where_Select_index() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Where(this.SourceSet,static value => value>1),static (value,index) => value+index);

		public IEnumerable<int> Where_Where() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Where(this.SourceSet,static value => value>1),static value => value<4);

		public IEnumerable<int> Where_Where_index() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Where(this.SourceSet,static value => value>1),static (value,index) => value<4&&index<2);

		public IEnumerable<int> Where_index_Select() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Where(this.SourceSet,static (value,index) => value>index),static value => value*10);

		public IEnumerable<int> Where_index_Select_index() =>
			SeamlessEnumerable.Select(SeamlessHashSet.Where(this.SourceSet,static (value,index) => value>index),static (value,index) => value+index);

		public IEnumerable<int> Where_index_Where() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Where(this.SourceSet,static (value,index) => value>index),static value => value<4);

		public IEnumerable<int> Where_index_Where_index() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Where(this.SourceSet,static (value,index) => value>index),static (value,index) => value<4&&index<2);

		public IEnumerable<long> Select_Where_Int64() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.LongSourceSet,static value => value*10L),static value => value>20L);

		public IEnumerable<string> Select_Where_String() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.StringSourceSet,static value => value.ToUpperInvariant()),static value => value.Length>1);

		public IEnumerable<ManagedStruct> Select_Where_ManagedStruct() =>
			SeamlessEnumerable.Where(SeamlessHashSet.Select(this.ManagedStructSourceSet,static value => global::Test.Seamless.Intercept.Seamless.Linq.HashSetSample.HashSetMethod2.CreateManagedStruct(value)),static value => value.Name.Length>2);

		public void Dispose() {
			this.source?.Dispose();
			this.longSource?.Dispose();
			this.stringSource?.Dispose();
			this.managedStructSource?.Dispose();
		}
	}

	internal static HashSet<int> Source() => [1,2,3,4];
	internal static HashSet<long> LongSource() => [1L,2L,3L,4L];
	internal static HashSet<string> StringSource() => ["a","bb","ccc"];
	internal static HashSet<ManagedStruct> ManagedStructSource() => [new() { Name="a" },new() { Name="bb" },new() { Name="ccc" }];
	internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };

	private static IHashSet<T> CreateUnmanagedSource<T>(IEnumerable<T> values)
		where T : unmanaged, IEquatable<T> {
		var store = HashStore.Create().Create<T>();
		var result = store.CreateOwned();
		foreach(var value in values) {
			result.Add(value);
		}
		return result;
	}

	private static IHashSet<T> CreateManagedSource<T>(IEnumerable<T> values)
		where T : IEquatable<T> {
		var store = HashStore.Create().Create<T>(new ManagedTag());
		var result = store.CreateOwned();
		foreach(var value in values) {
			result.Add(value);
		}
		return result;
	}
}
