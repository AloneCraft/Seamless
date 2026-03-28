using System.Diagnostics;
namespace Seamless.Collections;

public abstract class CollectionStore{
	//private protected const int IndexSizeBits = 6; 
	//private protected const int PageSizeBits = 12;
	//private protected const int MemorySizeBits = PageSizeBits+1;
	//private protected const long IndexSize =1L<<IndexSizeBits;
	//private protected const long MemorySize=1L<<MemorySizeBits;
	//private protected const long PageSize  =1L<<PageSizeBits;
	
	[System.Diagnostics.Conditional("DEBUG")]
	private protected static void AssertIsPowerOfTwo(long value,string paramName){
		Debug.Assert(value>0,$"{paramName} must be positive");
		Debug.Assert((value&(value-1))==0,$"{paramName} must be a power of 2, but was {value}");
	}
}
