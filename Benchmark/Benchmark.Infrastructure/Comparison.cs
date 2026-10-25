namespace Benchmark.Infrastructure;

public static class Comparison{
    public static ComparisonResult Compare(Measurement baseline,Measurement candidate,ComparisonMode mode=ComparisonMode.Regression,double tolerance=0.05,ComparisonPolicy? policy=null){
        policy??=ComparisonPolicy.Current;policy.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        if(!double.IsFinite(tolerance)){throw new ArgumentOutOfRangeException(nameof(tolerance));}
        var differences=Differences(baseline.Conditions,candidate.Conditions);
        ComparisonResult Result(ComparisonStatus status,string reason,double? time=null,double? allocation=null)=>new(
            Guid.NewGuid().ToString("N"),baseline.ID,candidate.ID,mode,status,tolerance,time,allocation,reason,differences,DateTimeOffset.UtcNow,policy.Version);
        if(!baseline.Conditions.Known||!candidate.Conditions.Known){return Result(ComparisonStatus.Incompatible,"記録時の測定条件が不明です。");}
        var required=new[]{nameof(Conditions.WorkloadID),nameof(Conditions.InputHash),nameof(Conditions.Count),nameof(Conditions.ElementType),
            nameof(Conditions.EnvironmentKey),nameof(Conditions.MeasurementVersion),nameof(Conditions.SemanticsVersion),nameof(Conditions.CodecVersion)};
        if((mode==ComparisonMode.Regression&&differences.Length!=0)
            ||differences.Any(difference=>required.Contains(difference,StringComparer.Ordinal))){
            return Result(ComparisonStatus.Incompatible,"比較に必要な入力・処理・環境・測定条件が一致しません。");
        }
        static string Evidence(Conditions conditions){
            var node=System.Text.Json.Nodes.JsonNode.Parse(conditions.ParametersJSON) as System.Text.Json.Nodes.JsonObject;
            return Fingerprint.CanonicalJSON(node?["Evidence"]?.ToJsonString()??"null");
        }
        if(Evidence(baseline.Conditions)!=Evidence(candidate.Conditions)){return Result(ComparisonStatus.Incompatible,"ライブラリ・Codec・測定方式・比較ポリシーの証拠版が一致しません。");}
        if(baseline.Status!="Passed"||candidate.Status!="Passed"){return Result(ComparisonStatus.Incomplete,"基準または候補が正常終了していません。");}
        if(string.IsNullOrEmpty(baseline.Digest)||string.IsNullOrEmpty(candidate.Digest)){return Result(ComparisonStatus.Incomplete,"正しさの検証結果が不足しています。");}
        if(baseline.Digest!=candidate.Digest){return Result(ComparisonStatus.CorrectnessFailure,"結果の内容が一致しません。");}
        if(baseline.Conditions.MeasurementVersion=="capacity-sweep-v1"){
            if(baseline.Capacity is null||candidate.Capacity is null){return Result(ComparisonStatus.Incomplete,"容量探索の最大成功件数・失敗種別が不足しています。");}
            if(baseline.Capacity.FailureKind!=candidate.Capacity.FailureKind){return Result(ComparisonStatus.Inconclusive,"容量探索の失敗種別が変化しました。");}
            if(candidate.Capacity.MaximumCount<baseline.Capacity.MaximumCount){return Result(ComparisonStatus.Regressed,"最大成功件数が基準を下回りました。");}
            return Result(ComparisonStatus.Passed,"最大成功件数は基準以上です。");
        }
        if(baseline.Conditions.MeasurementVersion=="test-outcome-v1"&&baseline.Samples.Length==0&&candidate.Samples.Length==0){
            return Result(ComparisonStatus.Passed,"機能検証の結果が一致しました。性能判定は行いません。");
        }
        bool Valid(Measurement measurement)=>measurement.Samples.Length>=policy.MinimumSamples
            &&measurement.Samples.All(static sample=>sample.OperationCount>0&&double.IsFinite(sample.ElapsedNanoseconds)&&sample.ElapsedNanoseconds>=0&&sample.AllocatedBytes>=0);
        if(!Valid(baseline)||!Valid(candidate)){return Result(ComparisonStatus.Incomplete,"各実行に7回以上の有効な測定が必要です。");}
        if(policy.IsNoisy(baseline)||policy.IsNoisy(candidate)){return Result(ComparisonStatus.Inconclusive,"標本内変動が比較ポリシーの上限を超えました。独立実行で再確認してください。");}
        var before=baseline.MedianNanoseconds;
        var after=candidate.MedianNanoseconds;
        if(before==0&&after!=0){return Result(ComparisonStatus.Inconclusive,"基準の時間が0のため比率を求められません。");}
        var ratio=1d;
        if(before>0){ratio=after/before;}
        double? allocationRatio=null;
        if(baseline.MedianAllocatedBytes>0){allocationRatio=candidate.MedianAllocatedBytes/baseline.MedianAllocatedBytes;}
        if(ratio>1+tolerance){return Result(ComparisonStatus.Regressed,"実行時間の中央値が許容悪化率を超えました。",ratio,allocationRatio);}
        return Result(ComparisonStatus.Passed,"実行時間は許容悪化率の範囲内です。割り当て量は別指標として表示します。",ratio,allocationRatio);
    }

    public static string[] Differences(Conditions baseline,Conditions candidate){
        var differences=new List<string>();
        foreach(var property in typeof(Conditions).GetProperties().Where(static property=>property.Name!=nameof(Conditions.ScopeKey))){
            var left=property.GetValue(baseline);
            var right=property.GetValue(candidate);
            if(property.Name.EndsWith("JSON",StringComparison.Ordinal)){
                left=Fingerprint.CanonicalJSON((string)left!);
                right=Fingerprint.CanonicalJSON((string)right!);
            }
            if(!Equals(left,right)){differences.Add(property.Name);}
        }
        return differences.ToArray();
    }
}
