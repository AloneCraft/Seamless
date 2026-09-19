namespace Verification.Core;

public static class VerificationComparison{
    public static ComparisonResult Compare(CaseResult baseline,CaseResult candidate,double tolerance=0.05){
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        if(!double.IsFinite(tolerance)){throw new ArgumentOutOfRangeException(nameof(tolerance));}
        if(baseline.CaseId!=candidate.CaseId||baseline.Conditions!=candidate.Conditions){
            return new(candidate.CaseId,ComparisonStatus.Incompatible,"ケース・入力・意味論・Codec・測定方法・環境・CPU・構成のいずれかが一致しません。");
        }
        if(baseline.Status!=CaseStatus.Passed||candidate.Status!=CaseStatus.Passed){
            return new(candidate.CaseId,ComparisonStatus.Incomplete,"基準または候補に未完了の結果があります。");
        }
        if(baseline.ResultDigest!=candidate.ResultDigest){
            return new(candidate.CaseId,ComparisonStatus.CorrectnessFailure,"結果のダイジェストが一致しません。");
        }
        if(baseline.Conditions.MeasurementVersion=="capacity-sweep-v1"){
            if(baseline.Metrics is null||candidate.Metrics is null
                ||!baseline.Metrics.TryGetValue("maximum-count",out var baselineCount)
                ||!candidate.Metrics.TryGetValue("maximum-count",out var candidateCount)
                ||!baseline.Metrics.TryGetValue("failure-kind",out var baselineFailure)
                ||!candidate.Metrics.TryGetValue("failure-kind",out var candidateFailure)){
                return new(candidate.CaseId,ComparisonStatus.Incomplete,"容量スイープの正本指標が不足しています。");
            }
            if(baselineFailure!=candidateFailure){
                return new(candidate.CaseId,ComparisonStatus.Inconclusive,"容量スイープの失敗種別が変化しました。",null,null);
            }
            if(candidateCount<baselineCount){
                double? decreaseRatio=null;
                if(baselineCount>0){decreaseRatio=candidateCount/baselineCount;}
                return new(candidate.CaseId,ComparisonStatus.Regressed,"最大成功Countが基準を下回りました。",decreaseRatio,null);
            }
            double? capacityRatio=null;
            if(baselineCount>0){capacityRatio=candidateCount/baselineCount;}
            return new(candidate.CaseId,ComparisonStatus.Passed,"最大成功Countは基準以上です。",capacityRatio,null);
        }
        if(baseline.Conditions.MeasurementVersion=="test-outcome-v1"&&baseline.ElapsedNanoseconds.Length==0&&candidate.ElapsedNanoseconds.Length==0){
            return new(candidate.CaseId,ComparisonStatus.Passed,"機能検証は一致しました。性能判定は行いません。");
        }
        if(baseline.ElapsedNanoseconds.Length<5||candidate.ElapsedNanoseconds.Length<5
            ||baseline.ElapsedNanoseconds.Any(static x=>!double.IsFinite(x)||x<0)
            ||candidate.ElapsedNanoseconds.Any(static x=>!double.IsFinite(x)||x<0)){
            return new(candidate.CaseId,ComparisonStatus.Inconclusive,"有効な反復測定が各5回以上必要です。");
        }
        var b=Median(baseline.ElapsedNanoseconds);
        var c=Median(candidate.ElapsedNanoseconds);
        if(b==0){
            if(c==0){return new(candidate.CaseId,ComparisonStatus.Passed,"基準と候補の中央値がともに0です。",1,0);}
            return new(candidate.CaseId,ComparisonStatus.Inconclusive,"基準の中央値が0のため、相対比較できません。");
        }
        var ratio=c/b;
        var share=c/(b+c);
        if(ratio>1+tolerance){return new(candidate.CaseId,ComparisonStatus.Regressed,"中央値が許容劣化率を超えました。",ratio,share);}
        return new(candidate.CaseId,ComparisonStatus.Passed,"許容劣化率の範囲内です。",ratio,share);
    }
    private static double Median(double[] values){
        var ordered=(double[])values.Clone();
        Array.Sort(ordered);
        var middle=ordered.Length/2;
        if(ordered.Length%2==0){return ordered[middle-1]/2+ordered[middle]/2;}
        return ordered[middle];
    }
}
