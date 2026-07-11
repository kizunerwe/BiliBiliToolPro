#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
base_script="$repo_root/qinglong/DefaultTasks/bili_task_base.sh"
dev_script="$repo_root/qinglong/DefaultTasks/dev/bili_dev_task_base.sh"

assert_contains() {
    local file="$1"
    local pattern="$2"
    local message="$3"
    if ! grep -Eq -- "$pattern" "$file"; then
        printf 'FAIL: %s\n' "$message" >&2
        return 1
    fi
}

assert_not_contains() {
    local file="$1"
    local pattern="$2"
    local message="$3"
    if grep -Eq -- "$pattern" "$file"; then
        printf 'FAIL: %s\n' "$message" >&2
        return 1
    fi
}

for script in "$base_script" "$dev_script"; do
    bash -n "$script"
    assert_contains "$script" 'flock' "$(basename "$script") should use a process lock"
    assert_contains "$script" 'check_installed.*\|\|' "$(basename "$script") should verify installation before running"
    assert_contains "$script" 'curl[^\n]*--fail' "$(basename "$script") downloads should fail on HTTP errors"
    assert_contains "$script" 'dotnet publish' "$(basename "$script") should publish once before execution"
    assert_not_contains "$script" 'dotnet run' "$(basename "$script") should not compile on every scheduled run"
    assert_not_contains "$script" 'www\.google\.com' "$(basename "$script") should not infer mirror settings from Google reachability"
done

printf 'PASS: qinglong base scripts satisfy hardening checks\n'
