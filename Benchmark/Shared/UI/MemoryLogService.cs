using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class MemoryLogService:ILogService {
    private readonly Action<string> append;

    public MemoryLogService(Action<string> append) {
        this.append=append;
    }

    public void Append(string text) {
        this.append(text);
    }
}
