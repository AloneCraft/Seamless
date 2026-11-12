using System.Reflection;
using System.Text.Json;

namespace Seamless.Management;

public sealed partial class SourceProjectService{
    private static async Task RefreshReferenceTypesAsync(OpenProject project,JsonElement evaluation,CancellationToken cancellationToken){
        var properties=evaluation.GetProperty("Properties");
        var target=properties.GetProperty("TargetPath").GetString()??"";
        var assets=properties.GetProperty("ProjectAssetsFile").GetString()??"";
        if(target.Length>0){target=Path.GetFullPath(target,Path.GetDirectoryName(project.Path)!);}
        if(assets.Length>0){assets=Path.GetFullPath(assets,Path.GetDirectoryName(project.Path)!);}
        string Stamp(string path){
            if(path.Length==0||!File.Exists(path)){return "missing";}
            var file=new FileInfo(path);return file.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var stamp=Stamp(target)+":"+Stamp(assets);
        if(project.ReferenceStamp==stamp){return;}
        project.ReferenceTypes=[];project.TypeCompletionDiagnostic=null;
        if(assets.Length==0||!File.Exists(assets)){
            project.TypeCompletionDiagnostic="参照DLLの型補完は、SDKで復元・ビルド後に同期すると利用できます。";
            project.ReferenceStamp=stamp;return;
        }
        try{
            var resolved=await EvaluateAsync(project.Path,project.TargetFramework,cancellationToken,project.Configuration,resolveReferences:true).ConfigureAwait(false);
            var references=resolved.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray()
                .Select(item=>item.GetProperty("FullPath").GetString()!).Distinct(PathComparer).ToArray();
            if(references.Length>2048){throw new ManagementException("Limit","型補完の参照DLLは2048件以内で指定してください。");}
            AssemblyName? identity=null;if(File.Exists(target)){identity=AssemblyName.GetAssemblyName(target);}
            project.ReferenceTypes=CSharpTypeCompletion.ReadMetadataTypes(references,identity,cancellationToken);
            project.ReferenceStamp=stamp;
            if(references.Any(path=>!File.Exists(path))){project.TypeCompletionDiagnostic="未ビルドの参照プロジェクトがあります。ビルド後に同期してください。";}
        }catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){
            project.ReferenceStamp=stamp;
            project.TypeCompletionDiagnostic="参照型のSDK評価が期限を超えました。プロジェクトを再評価してください。";
        }catch(Exception error)when(error is ManagementException or IOException or BadImageFormatException or UnauthorizedAccessException){
            project.ReferenceStamp=stamp;
            var detail=error.Message.Split('\n')[0];if(detail.Length>256){detail=detail[..256];}
            project.TypeCompletionDiagnostic="参照型を取得できません。SDKで復元・ビルド後に同期してください。 "+detail;
        }
    }
}
