#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
prod_dir="$repo_root/qinglong/DefaultTasks"
dev_dir="$prod_dir/dev"

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

declare -A prod_expected=(
    [base]=manual
    [test]=manual
    [silver2coin]='0 8 * * *'
    [daily]='0 9 * * *'
    [unfollowBatched]='0 10 1 * *'
    [charge]='0 12 * * *'
    [liveLottery]='0 13 * * *'
    [manga_privilege]='0 15 * * *'
    [manga]='0 17 * * *'
    [liveFansMedal]='5 0 * * *'
    [vip_privilege]='0 1 * * *'
    [vipBigPoint]='7 1 * * *'
    [login]='20 0 1 * *'
    [tryFix]='40 0 1 * *'
)

cron_for() {
    local file="$1"
    sed -n 's/^# cron:[[:space:]]*//p' "$file"
}

check_one() {
    local file="$1"
    local expected="$2"
    local name="$3"
    local header_count
    header_count="$(grep -c '^# cron:' "$file" || true)"

    if [ "$expected" = manual ]; then
        [ "$header_count" -eq 0 ] || fail "$name must not have a cron header"
        return
    fi

    [ "$header_count" -eq 1 ] || fail "$name must have exactly one cron header"
    local actual
    actual="$(cron_for "$file")"
    [ "$actual" = "$expected" ] || fail "$name expected cron '$expected', got '$actual'"

    if ! grep -Eq 'bilitool_lock\.sh|bili(_dev)?_task_base\.sh' "$file"; then
        fail "$name must load the shared lock helper directly or through its base script"
    fi
}

for name in "${!prod_expected[@]}"; do
    file="$prod_dir/bili_task_${name}.sh"
    [ -f "$file" ] || fail "missing prod task $file"
    check_one "$file" "${prod_expected[$name]}" "prod/$name"
done

for file in "$prod_dir"/bili_task_*.sh; do
    [ -f "$file" ] || continue
    name="${file##*/}"
    name="${name#bili_task_}"
    name="${name%.sh}"
    # 锁库是共享辅助脚本而非定时任务，dev 侧直接 source prod 版本
    [ "$name" = "bilitool_lock" ] && continue
    [ -n "${prod_expected[$name]+present}" ] || fail "unexpected prod task $file"
done

declare -A seen_prod=()
for name in "${!prod_expected[@]}"; do
    expected="${prod_expected[$name]}"
    [ "$expected" = manual ] && continue
    [ -z "${seen_prod[$expected]+present}" ] || fail "duplicate prod cron '$expected'"
    seen_prod[$expected]="$name"
done

for name in "${!prod_expected[@]}"; do
    file="$dev_dir/bili_dev_task_${name}.sh"
    [ -f "$file" ] || fail "missing dev task $file"
    expected="${prod_expected[$name]}"
    if [ "$expected" = manual ]; then
        check_one "$file" manual "dev/$name"
    else
        minute="${expected%% *}"
        rest="${expected#* }"
        dev_expected="$((minute + 5)) $rest"
        check_one "$file" "$dev_expected" "dev/$name"
    fi
done

for file in "$dev_dir"/bili_dev_task_*.sh; do
    [ -f "$file" ] || continue
    name="${file##*/}"
    name="${name#bili_dev_task_}"
    name="${name%.sh}"
    [ -n "${prod_expected[$name]+present}" ] || fail "unexpected dev task $file"
done

declare -A seen_dev=()
for name in "${!prod_expected[@]}"; do
    expected="${prod_expected[$name]}"
    [ "$expected" = manual ] && continue
    minute="${expected%% *}"
    rest="${expected#* }"
    dev_expected="$((minute + 5)) $rest"
    [ -z "${seen_dev[$dev_expected]+present}" ] || fail "duplicate dev cron '$dev_expected'"
    seen_dev[$dev_expected]="$name"
done

printf 'PASS: QingLong schedules are complete and non-overlapping\n'
