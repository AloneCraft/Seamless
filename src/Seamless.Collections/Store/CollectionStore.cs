using System.Diagnostics;
namespace Seamless.Collections;
public abstract class CollectionStore {
	/// <summary>2の累乗かどうかを確認する</summary>
	[System.Diagnostics.Conditional("DEBUG")]
	private protected static void AssertIsPowerOfTwo(long value,string paramName) {
		Debug.Assert(value>0,$"{paramName} must be positive");
		Debug.Assert((value&(value-1))==0,$"{paramName} must be a power of 2, but was {value}");
	}
}
