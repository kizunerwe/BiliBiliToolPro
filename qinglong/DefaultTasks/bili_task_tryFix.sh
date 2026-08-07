#!/usr/bin/env bash
# cron:40 0 1 * *
# new Env("bili尝试修复异常")

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$script_dir/bilitool_lock.sh"

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

lock_wait_seconds="${BILITOOL_LOCK_WAIT_SECONDS:-7200}"
lock_key="$(bilitool_lock_key "$qinglong_bili_repo_dir" "$bili_branch")"
lock_file="/tmp/bilitool-${lock_key}.lock"
if ! command -v flock >/dev/null 2>&1; then
    echo "缺少flock命令，请安装util-linux后重试"
    exit 1
fi
if ! acquire_bilitool_lock "$lock_file" "$lock_wait_seconds"; then
    echo "获取BiliBiliTool锁失败，已等待 ${lock_wait_seconds} 秒"
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
