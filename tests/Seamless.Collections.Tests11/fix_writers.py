import re

path = r"G:\GitHub\バイブコーディング\Seamless\src\Seamless.Collections11\SessionFunctions\AddSessionFunctions.cs"

with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Match single line definitions with `=> true;`
single_writer_pattern = r"(public bool SingleWriter\(.*?\)) => true;"
concurrent_writer_pattern = r"(public bool ConcurrentWriter\(.*?\)) => true;"
copy_updater_pattern = r"(public bool CopyUpdater\(.*?\)) => true;"

def replace_writer(match):
    header = match.group(1)
    return header + " {\n        src.CopyTo(ref dst);\n        return true;\n    }"

def replace_copy_updater(match):
    header = match.group(1)
    return header + " {\n        oldValue.CopyTo(ref newValue);\n        return true;\n    }"

# Use DOTALL to match across lines if needed, but these are usually single lines
content = re.sub(single_writer_pattern, replace_writer, content)
content = re.sub(concurrent_writer_pattern, replace_writer, content)
content = re.sub(copy_updater_pattern, replace_copy_updater, content)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Done")
