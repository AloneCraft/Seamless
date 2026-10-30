using Seamless.Collections;

namespace Benchmark.Infrastructure;

public static class IOConfiguration{
    public static StorageTuning? Parse(string mode,string pattern="Sequential"){
        if(mode=="BackendDefault"){
            if(pattern!="Sequential"){throw new ArgumentException("既定の入出力方式ではアクセス方式を上書きできません。");}
            return null;
        }
        if(mode is not ("Buffered" or "Unbuffered")||pattern is not ("Sequential" or "Random")){throw new ArgumentException("未対応の入出力方式です。");}
        return new StorageTuning(FileAccess.ReadWrite,Enum.Parse<StorageAccessPattern>(pattern),Enum.Parse<StorageBufferingMode>(mode));
    }
}
