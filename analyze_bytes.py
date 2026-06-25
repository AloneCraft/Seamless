"""
ファイルのバイト列を詳細に分析する
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
OUT = pathlib.Path("g:/GitHub/Seamless/byte_analysis.txt")

with open(OUT, "w", encoding="utf-8") as f:
    for fn in FILES:
        path = BASE / fn
        raw = path.read_bytes()

        f.write(f"\n{'='*60}\n")
        f.write(f"{fn} ({len(raw)} bytes)\n")
        f.write(f"{'='*60}\n")

        # UTF-8チェック
        try:
            text_utf8 = raw.decode('utf-8')
            f.write(f"UTF-8: VALID\n")
        except UnicodeDecodeError as e:
            f.write(f"UTF-8: INVALID - {e}\n")

        # CP932チェック
        try:
            text_cp932 = raw.decode('cp932')
            f.write(f"CP932: VALID ({len(text_cp932)} chars)\n")

            # Show what CP932 decoded text looks like for the first Japanese comment
            lines = text_cp932.split('\n')
            for i, line in enumerate(lines):
                if '//' in line and any('\u3040' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF' for c in line):
                    f.write(f"  First JP comment (CP932 decode): {line.strip()[:100]}\n")
                    break
        except Exception as e:
            f.write(f"CP932: FAILED - {e}\n")

        # ファイルの最初の80バイトを調査
        f.write(f"\n  First 80 bytes hex:\n")
        hex_str = ' '.join(f'{b:02x}' for b in raw[:80])
        f.write(f"    {hex_str}\n")

        # 最初の非ASCII行を調査
        lines = raw.split(b'\n')
        for i, line in enumerate(lines):
            if any(b >= 128 for b in line):
                f.write(f"\n  First non-ASCII line ({i+1}):\n")
                f.write(f"    Hex: {' '.join(f'{b:02x}' for b in line[:60])}\n")

                # Decode this line in various ways
                # As UTF-8 (with error replacement)
                line_utf8 = line.decode('utf-8', errors='replace')
                f.write(f"    UTF-8(replace): {line_utf8[:80]}\n")

                # As CP932
                try:
                    line_cp932 = line.decode('cp932')
                    f.write(f"    CP932: {line_cp932[:80]}\n")
                except:
                    f.write(f"    CP932: FAILED\n")

                # CP932 decode → Shift-JIS encode → UTF-8 decode
                try:
                    text = line.decode('cp932')
                    # Encode back to Shift-JIS bytes
                    sjis_raw = text.encode('cp932')
                    # Decode as UTF-8
                    text_fixed = sjis_raw.decode('utf-8', errors='replace')
                    f.write(f"    CP932->SJIS->UTF8: {text_fixed[:80]}\n")
                except Exception as e:
                    f.write(f"    CP932->SJIS->UTF8: FAILED ({e})\n")

                # Try: Latin-1 decode → UTF-8 re-interpret
                try:
                    line_latin1 = line.decode('latin-1')
                    line_recovered = line_latin1.encode('utf-8')
                    text_l1u8 = line_recovered.decode('utf-8')
                    f.write(f"    Latin1->UTF8: {text_l1u8[:80]}\n")
                except:
                    f.write(f"    Latin1->UTF8: FAILED\n")

                # Windows-1252 → Latin-1 → UTF-8
                try:
                    text_w1252 = line.decode('windows-1252')
                    bytes_l1 = text_w1252.encode('latin-1')
                    text_decoded = bytes_l1.decode('utf-8')
                    f.write(f"    W1252->L1->UTF8: {text_decoded[:80]}\n")
                except:
                    f.write(f"    W1252->L1->UTF8: FAILED\n")

                break

print(f"Analysis written to {OUT}")
