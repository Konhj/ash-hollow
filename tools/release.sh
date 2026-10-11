#!/bin/bash
# 用法: ./tools/release.sh 1.1.0
# 流程: 校验日志 -> 改版本号 -> 提交打 tag -> 推送 -> 等待 CI 构建 -> 自动下载 exe 到 dist/
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

echo "等待 CI 构建任务出现..."
run_id=""
for i in $(seq 1 12); do
    run_id=$(gh run list --limit 5 --json databaseId,headBranch --jq \
        '.[] | select(.headBranch=="v'"$ver"'") | .databaseId' | head -1)
    [ -n "$run_id" ] && break
    sleep 5
done
if [ -z "$run_id" ]; then
    echo "错误: 未找到构建任务,请到 GitHub Actions 页面检查"
    exit 1
fi

echo "构建中 (run $run_id)..."
gh run watch "$run_id" --exit-status || {
    echo "构建失败,请到 GitHub Actions 查看日志"
    exit 1
}

rm -rf dist
mkdir -p dist
gh release download "v$ver" -D dist
echo "完成: dist/AshHollow-v$ver.exe"
