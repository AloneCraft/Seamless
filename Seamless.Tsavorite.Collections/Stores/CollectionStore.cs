namespace Seamless.Tsavorite.Collections.Stores;

public abstract class CollectionStore{
    /// <summary>2の累乗かどうかを確認する</summary>
    [Conditional("DEBUG")]
    private protected static void AssertIsPowerOfTwo(long value,string paramName){
        Debug.Assert(value>0,$"{paramName} must be positive");
        Debug.Assert((value&(value-1))==0,$"{paramName} must be a power of 2, but was {value}");
    }

    private protected static void ValidatePowerOfTwo(long value,string paramName){
        if(value<=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be positive.");
        if((value&(value-1))!=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be a power of 2, but was {value}.");
    }

    private protected abstract long GetRecoveryEntryCount();
}
