#!/bin/bash
# 用法: ./tools/release.sh 1.1.0
set -e
cd "$(dirname "$0")/.."

ver="$1"
if [ -z "$ver" ]; then
    echo "usage: $0 <version>  (e.g. $0 1.1.0)"
    exit 1
fi

grep -q "\"$ver\":" changelog.py || {
    echo "错误: changelog.py 中没有版本 $ver 的更新日志,请先在 CHANGELOG 里添加"
    exit 1
}

sed -i "s/^VERSION = .*/VERSION = \"$ver\"/" settings.py
git add settings.py
git commit -m "Release v$ver"
git tag "v$ver"
git push origin main
git push origin "v$ver"
echo "pushed v$ver -> GitHub Actions will build and create a release"
