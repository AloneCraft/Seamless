"""
文字化け修復 最終版

分析: ファイルは Shift-JIS(CP932) で保存されている。
中身は「UTF-8の日本語バイト列をCP932文字として誤解釈したもの」。

例:
- 元の日本語: 「コレクション 空 false」
- 元のUTF-8バイト: E3 82 B3 E3 83 AC E3 82 AF E3 82 B7 ...
- CP932として誤解釈: 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ 遨ｺ false
- このテキストがShift-JISバイトとして保存されている

修復手順:
1. CP932でデコード → 繧ｳ繝ｬ... (mojibakeテキスト)
2. 同じエンコーディング(CP932)でエンコード → 元のShift-JISバイト列 = 元々のUTF-8バイト列
3. UTF-8でデコード → 日本語テキスト
"""
import pathlib

FILES = [
    "Enumerator.cs",
    "HashSetTestBase.cs",
    "IDictionaryTestBase.cs",
    "StoreCheckpoint.cs",
    "HashSetDebugView.cs",
]

BASE = pathlib.Path("g:/GitHub/Seamless/Tsavorite.Collection.Shared")
REPORT_PATH = pathlib.Path("g:/GitHub/Seamless/fix_final_report2.txt")

def fix_file(fn: str) -> list[str]:
    """文字化けファイルを修復"""
    lines = []
    path = BASE / fn

    if not path.exists():
        return [f"{fn}: SKIP (not found)"]

    raw = path.read_bytes()
    lines.append(f"\n=== {fn} ({len(raw)} bytes) ===")

    # Step 1: Try to read as CP932
    try:
        text_cp932 = raw.decode('cp932')
        lines.append(f"  CP932 decode: OK ({len(text_cp932)} chars)")
    except Exception as e:
        lines.append(f"  CP932 decode: FAILED ({e})")
        return lines

    # Step 2: Try to fix by encoding back to Shift-JIS and decoding as UTF-8
    try:
        sjis_bytes = text_cp932.encode('cp932')
        text_fixed = sjis_bytes.decode('utf-8')
        lines.append(f"  CP932→Shift-JIS→UTF-8: OK ({len(sjis_bytes)} bytes)")

        # Count Japanese chars
        jp_count = sum(1 for c in text_fixed if '\u3040' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF')
        lines.append(f"  正しい日本語文字数: {jp_count}")

        # Show fixed comments
        fixed_lines = text_fixed.split('\n')
        for i, line in enumerate(fixed_lines):
            if ('//' in line or '///' in line) and any(ord(c) > 0x7F for c in line):
                lines.append(f"    L{i+1}: {line.strip()[:120]}")
                if i > 15:
                    lines.append(f"    ... (total {sum(1 for l in fixed_lines if ('//' in l or '///' in l) and any(ord(c) > 0x7F for c in l))} lines)")
                    break

        if jp_count > 0:
            # Create backup
            bak = path.with_suffix('.bak')
            if not bak.exists():
                path.rename(bak)
                lines.append(f"  Backup: {bak.name}")

            # Write fixed file
            path.write_bytes(text_fixed.encode('utf-8'))
            lines.append(f"  ✓ WRITTEN ({len(raw)} → {len(text_fixed.encode('utf-8'))} bytes)")
        else:
            lines.append(f"  ✓ No Japanese text - keeping original")

    except Exception as e:
        lines.append(f"  FIX FAILED: {e}")
        # Fallback: just convert CP932 to UTF-8
        try:
            result = text_cp932.encode('utf-8')
            path.write_bytes(result)
            lines.append(f"  Fallback: CP932→UTF-8 direct conversion")
        except Exception as e2:
            lines.append(f"  Fallback FAILED: {e2}")

    return lines

report_lines = []
for fn in FILES:
    report_lines.extend(fix_file(fn))

report_lines.append("\n=== DONE ===")

REPORT_PATH.write_text('\n'.join(report_lines), encoding='utf-8')
print(f"Report: {REPORT_PATH}")
