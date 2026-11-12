using System.Text.Json;

namespace Seamless.Management;

public sealed partial class SourceProjectService{
    private static void ValidateConfiguration(string configuration){
        if(string.IsNullOrWhiteSpace(configuration)||configuration.Length>128||configuration.Any(character=>!char.IsLetterOrDigit(character)&&character is not '_' and not '-' and not '.' and not ' ')){
            throw new ManagementException("InvalidRequest","ビルド構成名は128文字以内の文字・数字・空白・_・-・.で指定してください。");
        }
    }
    private static string[] Configurations(JsonElement properties,string current){
        if(properties.GetProperty("Configuration").GetString()!=current){throw new ManagementException("ProjectEvaluation","評価されたビルド構成が要求と一致しません。");}
        var names=properties.GetProperty("Configurations").GetString()??"";
        if(names.Length>8192){throw new ManagementException("Limit","ビルド構成の一覧が大きすぎます。");}
        var configurations=names.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Append(current)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(name=>{
                if(string.Equals(name,current,StringComparison.OrdinalIgnoreCase)){return current;}
                return name;
            }).ToArray();
        if(configurations.Length>64){throw new ManagementException("Limit","ビルド構成は64件以内で指定してください。");}
        return configurations;
    }
}
