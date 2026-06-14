#!/usr/bin/env bash
# cron:0 0 1 1 *
# new Env("bili尝试修复异常")

dir_shell=$QL_DIR/shell
. $dir_shell/share.sh
. /root/.bashrc

bili_repo=${BILI_REPO:-"kizunerwe/bilibilitoolpro"}
bili_branch=${BILI_BRANCH:-""}

echo "青龙repo目录: $dir_repo"
qinglong_bili_repo="$(echo "$bili_repo" | sed 's/\//_/g')${bili_branch}"
qinglong_bili_repo_dir="$(find "$dir_repo" -type d \( -iname "$qinglong_bili_repo" -o -iname "${qinglong_bili_repo}_main" \) | head -1)"
echo "bili仓库目录: $qinglong_bili_repo_dir"

if [ -z "$qinglong_bili_repo_dir" ]; then
    echo "未找到 bili 仓库目录"
    echo "查找目标：$qinglong_bili_repo"
    echo "请确认已在青龙中拉取仓库 ${bili_repo}${bili_branch}，或通过环境变量 BILI_REPO / BILI_BRANCH 覆盖默认值"
    exit 1
fi

echo -e "清理缓存...\n"
cd "$qinglong_bili_repo_dir"
find . -type d -name "bin" -exec rm -rf {} +
find . -type d -name "obj" -exec rm -rf {} +
echo -e "清理完成\n"

echo "检测dotnet..."
dotnetVersion=$(dotnet --version)
echo "当前dotnet版本：$dotnetVersion"
if [[ $(echo "$dotnetVersion" | grep -oE '^[0-9]+') -ge 8 ]]; then
    echo "已安装，且版本满足"
else
    echo "which dotnet: $(which dotnet)"
    echo "Path: $PATH"
    rm -f /usr/local/bin/dotnet
fi
echo "检测dotnet结束"
