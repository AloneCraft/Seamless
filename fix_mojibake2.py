"""
文字化け修正 v2:
元のファイル（バックアップ）はUTF-8の日本語テキストがCP932(Shift-JIS)として誤解釈・再エンコードされた状態。
これを戻すには:
  1. ファイルをCP932で読む → 誤解釈された「日本語もどき」文字列になる
  2. これをShift-JISバイト列に戻す → 元のUTF-8バイト列
  3. UTF-8として読む → 本来の日本語テキスト

「文字化けしたテキスト = CP932として読めるように変換されたUTF-8バイト列」
これを戻すには「CP932として書いてShift-JISバイト列に→UTF-8として読む」
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

report = []

for fn in FILES:
    current = BASE / fn
    bak = BASE / (fn + ".bak")

    if not current.exists():
        report.append(f"{fn}: SKIP (not found)")
        continue

    raw = current.read_bytes()

    # Check if current is valid UTF-8
    try:
        raw.decode('utf-8')
        is_utf8 = True
    except:
        is_utf8 = False

    text = raw.decode('utf-8', errors='replace')

    # The mojibake text looks like Shift-JIS decoded as something else.
    # Let's try: encode UTF-8 text back to Shift-JIS bytes, then read those as UTF-8
    try:
        # mojibake のテキストをShift-JISと仮定してバイト列に戻す
        sjis_bytes = text.encode('cp932', errors='strict')
        # そのバイト列をUTF-8としてデコード
        fixed = sjis_bytes.decode('utf-8', errors='replace')

        # 結果を検証: 日本語文字が多数含まれているか
        jp_count = sum(1 for c in fixed if '\u3040' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF')
        ascii_count = sum(1 for c in fixed if c.isascii())

        report.append(f"{fn}: UTF8={is_utf8}, src={len(raw)}B, dst={len(fixed.encode('utf-8'))}B, JP={jp_count}, ASCII={ascii_count}")

        # Show fixed comments
        lines = fixed.split('\n')
        comment_lines = []
        for i, line in enumerate(lines):
            if ('//' in line or '///' in line) and any('\u3040' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF' for c in line):
                comment_lines.append(f"  L{i+1}: {line.strip()[:120]}")
                if len(comment_lines) >= 5:
                    break

        report.append(f"  Fix result:")
        report.extend(comment_lines if comment_lines else ["  (no JP comments)"])

        # Write result
        if jp_count > 0:
            current.write_bytes(fixed.encode('utf-8'))
            report.append(f"  => WRITTEN to {fn}")
        else:
            report.append(f"  => SKIPPED (no Japanese text found)")

    except Exception as e:
        report.append(f"{fn}: ERROR: {e}")

    report.append("")

# Write report
result_path = pathlib.Path("g:/GitHub/Seamless/fix_result.txt")
result_path.write_text('\n'.join(report), encoding='utf-8')
print(f"Report written to {result_path}")
