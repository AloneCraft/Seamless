"""文字化け解析スクリプト - ファイルに直接出力"""
import pathlib, sys

FILES = [
    "Tsavorite.Collection.Shared/Enumerator.cs",
    "Tsavorite.Collection.Shared/HashSetTestBase.cs",
    "Tsavorite.Collection.Shared/IDictionaryTestBase.cs",
    "Tsavorite.Collection.Shared/StoreCheckpoint.cs",
]

outpath = pathlib.Path("g:/GitHub/Seamless/encoding_analysis.txt")
with open(outpath, "w", encoding="utf-8") as out:
    for filepath in FILES:
        raw = pathlib.Path(filepath).read_bytes()
        out.write(f"\n=== {filepath} ({len(raw)} bytes) ===\n")

        # Check if valid UTF-8
        try:
            raw.decode('utf-8')
            out.write("UTF-8: VALID\n")
        except UnicodeDecodeError as e:
            out.write(f"UTF-8: INVALID ({e})\n")

        # Try cp932
        try:
            text = raw.decode('cp932')
            out.write("CP932: VALID\n")
            lines = text.split('\n')
            for i, line in enumerate(lines):
                if i < 5 and any(ord(c) > 127 for c in line):
                    out.write(f"  Line {i+1}: {line[:100]}\n")
        except Exception as e:
            out.write(f"CP932: FAILED ({e})\n")

        # Check 0x81 bytes
        pos81 = raw.find(b'\x81')
        if pos81 >= 0:
            out.write(f"First 0x81 at pos {pos81}, context hex: {raw[max(0,pos81-5):pos81+10].hex()}\n")

        # Show first non-ASCII line
        lines = raw.split(b'\n')
        for i, line in enumerate(lines):
            if any(b >= 128 for b in line):
                try:
                    as_cp932 = line.decode('cp932')
                    out.write(f"Line {i+1} as CP932: {as_cp932[:100]}\n")
                except:
                    out.write(f"Line {i+1} CP932 decode failed\n")
                try:
                    as_latin1 = line.decode('latin-1')
                    out.write(f"Line {i+1} as Latin-1: {as_latin1[:100]}\n")
                except:
                    out.write(f"Line {i+1} Latin-1 decode failed\n")
                break

print(f"Done. Output written to {outpath}")
