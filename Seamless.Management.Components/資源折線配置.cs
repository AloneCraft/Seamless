using System.Globalization;

namespace Seamless.Management.Components;

public sealed record 資源折線区間(string レーンID,string 経路,string 単位,double 最小値,double 最大値);
public sealed record 資源折線点(string レーンID,double X,double Y,double 値,string 単位);
public sealed record 資源折線配置(資源折線区間[] 区間,資源折線点[] 点){
    public static 資源折線配置 作成(資源ヒート配置 配置){
        var paths=new List<資源折線区間>();var points=new List<資源折線点>();
        foreach(var lane in 配置.レーン){
            var percent=lane.値.PercentBasis is not null;var unit=lane.値.Unit;if(percent){unit="%";}
            double? Value(資源サンプル配置 item){if(item.値.State!="Available"){return null;}if(percent){return item.値.Percent;}return item.値.Value;}
            var items=配置.サンプル.Where(item=>item.レーン.Id==lane.値.Id).OrderBy(item=>item.値.StartTimestamp).ThenBy(item=>item.値.EndTimestamp).ToArray();
            var values=items.Select(Value).Where(value=>value.HasValue).Select(value=>value!.Value).ToArray();if(values.Length==0){continue;}
            var minimum=Math.Min(0,values.Min());var maximum=Math.Max(0,values.Max());if(percent){maximum=Math.Max(100,maximum);}
            double Y(double value){if(maximum==minimum){return lane.Y+19;}var scale=Math.Max(Math.Abs(minimum),Math.Abs(maximum));return lane.Y+19-(value/scale-minimum/scale)/(maximum/scale-minimum/scale)*17;}
            資源サンプル配置? previous=null;var path="";
            void Flush(){if(path.Length>0){paths.Add(new(lane.値.Id,path,unit,minimum,maximum));path="";}previous=null;}
            foreach(var item in items){
                if(Value(item) is not{} value){Flush();continue;}
                var y=Y(value);
                if(item.値.Aggregation=="Instant"||item.値.StartTimestamp==item.値.EndTimestamp){Flush();points.Add(new(lane.値.Id,item.X,y,value,unit));continue;}
                if(previous is null||previous.値.EndTimestamp!=item.値.StartTimestamp||previous.値.Evidence!=item.値.Evidence){Flush();path=$"M{F(item.X)} {F(y)}";}
                else{path+=$" L{F(item.X)} {F(y)}";}
                path+=$" L{F(item.X+item.幅)} {F(y)}";previous=item;
            }
            Flush();
        }
        return new(paths.ToArray(),points.ToArray());
    }
    public static int 再生位置(int startCursor,double elapsedSeconds,double speed,long start,long end,long frequency){
        if(startCursor is <0 or >1000||!double.IsFinite(elapsedSeconds)||elapsedSeconds<0||!double.IsFinite(speed)||speed<=0||frequency<=0||end<start){throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));}
        var duration=(double)(((decimal)end-start)/frequency);if(duration==0){return 1000;}
        return (int)Math.Clamp(startCursor+elapsedSeconds*speed/duration*1000,0,1000);
    }
    private static string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
}
