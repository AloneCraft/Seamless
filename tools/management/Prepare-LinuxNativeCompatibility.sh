#!/usr/bin/env bash
set -euo pipefail

fail() {
    printf '%s\n' "$1" >&2
    exit 1
}

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
    printf '%s\n' 'Native compatibility preparation supports Linux x86_64 only.' >&2
    exit 2
fi
if [[ "$#" != 1 ]]; then
    fail 'Usage is Prepare-LinuxNativeCompatibility.sh <published-directory>.'
fi
for command in realpath readelf ldd ln awk grep; do
    command -v "$command" >/dev/null || fail "Required command is unavailable. $command"
done
published=$(realpath -e -- "$1") || fail 'The published directory does not exist.'
[[ -d "$published" ]] || fail 'The published path must be a directory.'
native="$published/runtimes/linux-x64/native"
libraries=("$native/libnative_device.so" "$native/libnative_device_libaio.so")
alias="$native/libaio.so.1"
target=''
target_real=''
for candidate in /usr/lib/x86_64-linux-gnu/libaio.so.1t64 /lib/x86_64-linux-gnu/libaio.so.1t64 /usr/lib64/libaio.so.1t64 /usr/lib/libaio.so.1t64; do
    if [[ -f "$candidate" ]]; then
        target="$candidate"
        target_real=$(realpath -e -- "$candidate")
        break
    fi
done

# An existing file is never replaced, including a dangling or differently targeted link.
if [[ -e "$alias" || -L "$alias" ]]; then
    [[ -L "$alias" ]] || fail 'Conflicting app-local libaio.so.1 is a regular file. Nothing was replaced.'
    [[ -n "$target" ]] || fail 'The existing libaio.so.1 link cannot be verified without the host libaio.so.1t64.'
    resolved=$(realpath -e -- "$alias") || fail 'The existing app-local libaio.so.1 link is dangling.'
    [[ "$resolved" == "$target_real" ]] || fail 'Conflicting app-local libaio.so.1 link target. Nothing was replaced.'
fi

needs_alias=false
for library in "${libraries[@]}"; do
    [[ -f "$library" ]] || fail "Required published native library is missing. $library"
    header=$(LC_ALL=C readelf -h -- "$library") || fail 'Cannot inspect the published ELF library.'
    grep -Fq 'ELF64' <<<"$header" && grep -Fq 'Advanced Micro Devices X86-64' <<<"$header" || fail 'The published native library must be Linux ELF64 x86_64.'
    dependencies=$(LC_ALL=C ldd -- "$library" 2>&1) || fail "Cannot inspect native dependencies. $dependencies"
    missing=$(awk '$2 == "=>" && $3 == "not" && $4 == "found" { print $1 }' <<<"$dependencies")
    if [[ -n "$missing" ]]; then
        while IFS= read -r dependency; do
            [[ "$dependency" == libaio.so.1 ]] || fail "Resolve the other native dependency before preparation. $dependency"
        done <<<"$missing"
        dynamic=$(LC_ALL=C readelf -d -- "$library")
        grep -Fq 'Shared library: [libaio.so.1]' <<<"$dynamic" || fail 'The native library does not declare the expected libaio.so.1 dependency.'
        origin=$(grep -E '\((RPATH|RUNPATH)\)' <<<"$dynamic") || fail 'The native library has no application-local library search path.'
        grep -Fq '$ORIGIN' <<<"$origin" || fail 'The native library must search its own directory for the compatibility link.'
        needs_alias=true
    fi
done
if [[ "$needs_alias" == false ]]; then
    printf '%s\n' 'Native dependencies are already satisfied. No files were changed.'
    exit 0
fi

[[ -n "$target" ]] || fail 'Install the host libaio1t64 package before preparing this Linux x86_64 deployment. No package was installed.'
header=$(LC_ALL=C readelf -h -- "$target") || fail 'Cannot inspect the host libaio1t64 ELF library.'
grep -Fq 'ELF64' <<<"$header" && grep -Fq 'Advanced Micro Devices X86-64' <<<"$header" || fail 'The host libaio1t64 must be ELF64 x86_64.'
symbols=$(LC_ALL=C readelf --dyn-syms --wide -- "$target") || fail 'Cannot inspect the host libaio1t64 symbols.'
for symbol in 'io_submit@+LIBAIO_0\.1' 'io_setup@+LIBAIO_0\.4' 'io_getevents@+LIBAIO_0\.4' 'io_destroy@+LIBAIO_0\.4'; do
    grep -Eq "$symbol([[:space:]]|$)" <<<"$symbols" || fail 'The host libaio1t64 does not provide the required versioned I/O symbols.'
done
[[ -w "$native" ]] || fail 'The native directory is read-only and compatibility is not prepared. Run this script during deployment preparation before making the installation read-only.'

if ! ln -s -- "$target" "$alias"; then
    # A concurrent preparer may have created exactly the same link.
    [[ -L "$alias" ]] || fail 'Compatibility link creation failed. Check deployment write access and existing files.'
    resolved=$(realpath -e -- "$alias") || fail 'A conflicting dangling compatibility link appeared.'
    [[ "$resolved" == "$target_real" ]] || fail 'A conflicting compatibility link appeared. Nothing was replaced.'
fi
for library in "${libraries[@]}"; do
    dependencies=$(LC_ALL=C ldd -- "$library" 2>&1) || fail "The prepared alias remains in place but native dependencies could not be inspected. $dependencies"
    if grep -Fq 'not found' <<<"$dependencies"; then
        fail "The prepared alias remains in place but native dependencies are unresolved. $dependencies"
    fi
done
printf '%s\n' 'App-local libaio compatibility is prepared. The installation can now be made read-only.'
