#!/usr/bin/env bash
# Recompile Stockfish 19 (tag sf_19) pour Android arm64 avec des segments ELF alignés sur 16 Kio
# (exigence Google Play pour les appareils à pages de 16 Ko), puis l'installe dans
# Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so.
#
# Recette = celle de la release officielle (.github/workflows/arm_compilation.yml du tag sf_19,
# binaire stockfish-android-arm64-universal) :
#   NDK r27c (27.2.12479018), LDFLAGS="-static -Wno-unused-command-line-argument",
#   make profile-build ARCH=arm64-universal COMP=ndk RUN_PREFIX=qemu-aarch64, puis make strip.
# Seul ajout : l'édition de liens finale du binaire universel reçoit -Wl,-z,max-page-size=16384
# (aucun objet n'est recompilé pour cela ; le code et la PGO sont ceux de la recette officielle).
#
# Prérequis (Linux x86-64) : git, make, python3, qemu-aarch64 (paquet qemu-user), hexdump (bsdextrautils),
# readelf, sha256sum,
# NDK r27c : sdkmanager "ndk;27.2.12479018" (ANDROID_HOME ou ANDROID_NDK_HOME).
#
# Usage : tools/build-stockfish-android.sh [sortie.so]
#   SF_WORKDIR  dossier de travail (défaut : mktemp -d) ; supprimé à la fin sauf SF_KEEP=1
#   SF_NNUE     chemin d'un nn-1a298aa575a0.nnue déjà téléchargé (vérifié par SHA-256 complet),
#               sinon « make net » le télécharge
#   SF_JOBS     parallélisme de make (défaut : nproc)
set -euo pipefail

TAG=sf_19
COMMIT=edb0d9db6731067ec50ce619ff372b463bc4dd5d
BENCH=2497913                       # « Bench: » du message du commit sf_19
NDK_VERSION=27.2.12479018           # r27c, comme la release officielle
NNUE_NAME=nn-1a298aa575a0.nnue
NNUE_SHA256=1a298aa575a085434d29027978dc36867fe9c5bcea9376654b7a8eba1e52dfc2
PAGE=16384

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$repo_root/Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so}"
out="$(cd "$(dirname "$out")" && pwd)/$(basename "$out")"
jobs="${SF_JOBS:-$(nproc)}"

ndk="${ANDROID_NDK_HOME:-${ANDROID_HOME:-$HOME/android-sdk}/ndk/$NDK_VERSION}"
ndk_bin="$ndk/toolchains/llvm/prebuilt/linux-x86_64/bin"
[ -x "$ndk_bin/aarch64-linux-android29-clang++" ] || { echo "NDK $NDK_VERSION introuvable ($ndk)" >&2; exit 1; }
grep -q "Pkg.Revision = $NDK_VERSION" "$ndk/source.properties" || { echo "Le NDK n'est pas $NDK_VERSION" >&2; exit 1; }
command -v qemu-aarch64 >/dev/null || { echo "qemu-aarch64 manquant (apt install qemu-user)" >&2; exit 1; }
# Le clang du NDK r27c ne connaît pas #embed : scripts/net.sh intègre alors le réseau via hexdump.
command -v hexdump >/dev/null || { echo "hexdump manquant (apt install bsdextrautils)" >&2; exit 1; }

work="${SF_WORKDIR:-$(mktemp -d)}"
cleanup() { [ "${SF_KEEP:-0}" = 1 ] || rm -rf "$work"; }
trap cleanup EXIT

echo "== Sources $TAG ($COMMIT) dans $work"
git clone -q --depth 1 --branch "$TAG" https://github.com/official-stockfish/Stockfish.git "$work/Stockfish"
cd "$work/Stockfish/src"
[ "$(git rev-parse HEAD)" = "$COMMIT" ] || { echo "Le tag $TAG ne pointe pas sur $COMMIT" >&2; exit 1; }

if [ -n "${SF_NNUE:-}" ]; then
  echo "$NNUE_SHA256  $SF_NNUE" | sha256sum -c --quiet -
  cp "$SF_NNUE" "$NNUE_NAME"
fi
make net
echo "$NNUE_SHA256  $NNUE_NAME" | sha256sum -c --quiet -

export PATH="$ndk_bin:$PATH"
export LDFLAGS="-static -Wno-unused-command-line-argument"
mk() { make ARCH=arm64-universal COMP=ndk RUN_PREFIX=qemu-aarch64 "$@"; }

echo "== Build officiel (PGO sous qemu, long)"
mk clean
mk -j"$jobs" profile-build
echo "== Édition de liens finale avec max-page-size=$PAGE"
rm -f stockfish
mk profile-build EXTRALDFLAGS="-Wl,-z,max-page-size=$PAGE"
mk strip

echo "== Vérifications"
python3 -I - stockfish "$PAGE" <<'PY'
import struct, sys
path, page = sys.argv[1], int(sys.argv[2])
d = open(path, 'rb').read()
assert d[:4] == b'\x7fELF' and d[4] == 2 and d[5] == 1, "pas un ELF64 little-endian"
assert struct.unpack_from('<H', d, 18)[0] == 183, "pas un ELF aarch64"
phoff, = struct.unpack_from('<Q', d, 32)
phentsize, phnum = struct.unpack_from('<HH', d, 54)
loads = 0
for i in range(phnum):
    p_type, _, off, vaddr, _, _, _, align = struct.unpack_from('<IIQQQQQQ', d, phoff + i * phentsize)
    if p_type != 1:
        continue
    loads += 1
    assert align >= page, f"LOAD {i}: p_align {align:#x} < {page:#x}"
    assert off % page == vaddr % page, f"LOAD {i}: offset/vaddr non congrus modulo {page:#x}"
assert loads, "aucun segment LOAD"
print(f"OK : {loads} segments LOAD alignés sur {page:#x}")
PY
readelf -lW stockfish | grep -E "^\s+LOAD"
sig=$(printf 'bench\nquit\n' | qemu-aarch64 ./stockfish 2>&1 | grep -E "^Nodes searched" | awk '{print $NF}')
[ "$sig" = "$BENCH" ] || { echo "Signature $sig != $BENCH" >&2; exit 1; }
echo "Signature bench : $sig (= $TAG)"
{ printf 'uci\nisready\nposition startpos\ngo depth 10\n'; sleep 30; printf 'quit\n'; } \
  | timeout 600 qemu-aarch64 ./stockfish | grep -E "^(id name|uciok|readyok|bestmove)"

mkdir -p "$(dirname "$out")"
cp stockfish "$out"
echo "== Installé : $out"
echo "Taille : $(stat -c %s "$out") octets"
echo "SHA-256 : $(sha256sum "$out" | cut -d' ' -f1)"
