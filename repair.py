# -*- coding: utf-8 -*-
"""Shared/Tsavorite.Collections 文字化け修復"""
import pathlib, shutil

BASE = pathlib.Path("g:/GitHub/Seamless/Shared/Tsavorite.Collections")

def fix_file(fn, method):
    path = BASE / fn
    raw = path.read_bytes()
    bak = path.with_suffix('.bak')
    if not bak.exists():
        shutil.copy2(path, bak)
    if method == 'cp932_utf8':
        text = raw.decode('cp932')
        sjis = text.encode('cp932')
        result = sjis.decode('utf-8').encode('utf-8')
    elif method == 'hybrid':
        result = bytearray()
        i = 0
        while i < len(raw):
            b = raw[i]
            if b < 0x80:
                result.append(b); i += 1; continue
            ok = False
            for length in [3, 2, 4]:
                if i + length <= len(raw):
                    try:
                        raw[i:i+length].decode('utf-8')
                        result.extend(raw[i:i+length]); i += length; ok = True; break
                    except: pass
            if ok: continue
            if i+1 < len(raw):
                try:
                    d = bytes([b, raw[i+1]]).decode('cp932')
                    result.extend(d.encode('utf-8')); i += 2; continue
                except: pass
            result.append(b); i += 1
        result = bytes(result)
    elif method == 'latin1_utf8':
        result = raw.decode('latin-1').encode('utf-8')
    path.write_bytes(result)

# files and their fix methods
fixes = [
    ('Enumerator.cs', 'cp932_utf8'),
    ('HashSetTestBase.cs', 'hybrid'),
    ('IDictionaryTestBase.cs', 'hybrid'),
    ('StoreCheckpoint.cs', 'hybrid'),
    ('HashSetDebugView.cs', 'latin1_utf8'),
]

for fn, method in fixes:
    print(f"Fixing {fn} with {method}...")
    fix_file(fn, method)
    print(f"  OK")

print("Done!")
