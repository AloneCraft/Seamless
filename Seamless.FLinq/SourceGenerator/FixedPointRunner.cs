namespace Seamless.FLinq;

internal enum OptimizationTermination{
    FixedPoint,
    CycleDetected,
    IterationLimit,
    RebindFailed,
}

internal readonly record struct FixedPointRoundResult<T>(T State,bool Changed,bool FailedToRebind){
    public static FixedPointRoundResult<T> Unchanged(T state)=>new(state,false,false);
    public static FixedPointRoundResult<T> ChangedState(T state)=>new(state,true,false);
    public static FixedPointRoundResult<T> RebindFailure(T state)=>new(state,false,true);
}

internal readonly record struct FixedPointResult<T>(
    T State,
    int RoundCount,
    OptimizationTermination Termination);

internal static class FixedPointRunner{
    public const int MaximumRounds=128;

    public static FixedPointResult<T> Run<T>(
        T initialState,
        Func<T,int,FixedPointRoundResult<T>> runRound,
        Func<T,string> fingerprint,
        int maximumRounds=MaximumRounds){
        if(maximumRounds<=0){
            throw new ArgumentOutOfRangeException(nameof(maximumRounds));
        }

        var state=initialState;
        var fingerprints=new HashSet<string>(StringComparer.Ordinal){
            fingerprint(state),
        };
        for(var round=1;round<=maximumRounds;round++){
            var result=runRound(state,round);
            if(result.FailedToRebind){
                return new(result.State,round,OptimizationTermination.RebindFailed);
            }

            state=result.State;
            if(!result.Changed){
                return new(state,round,OptimizationTermination.FixedPoint);
            }

            if(!fingerprints.Add(fingerprint(state))){
                return new(state,round,OptimizationTermination.CycleDetected);
            }
        }

        return new(state,maximumRounds,OptimizationTermination.IterationLimit);
    }
}
