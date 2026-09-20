#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
helper="$repo_root/qinglong/DefaultTasks/bili_task_bilitool_lock.sh"
tmp_dir="$(mktemp -d)"
cleanup() {
    rm -rf "$tmp_dir"
}
trap cleanup EXIT

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

[ -f "$helper" ] || fail "missing lock helper"
command -v flock >/dev/null 2>&1 || fail "flock command is required; run this test on Linux or WSL"

holder_lock="$tmp_dir/holder.lock"
holder_ready="$tmp_dir/holder.ready"
(
    # shellcheck source=/dev/null
    . "$helper"
    acquire_bilitool_lock "$holder_lock" 0 || exit 1
    : >"$holder_ready"
    sleep 2
) &
holder_pid=$!
for _ in $(seq 1 40); do
    [ -f "$holder_ready" ] && break
    sleep 0.05
done
[ -f "$holder_ready" ] || fail "background process did not acquire lock"

if bash -c '. "$1"; acquire_bilitool_lock "$2" 0' _ "$helper" "$holder_lock"; then
    fail "wait=0 acquired an already-held lock"
fi
wait "$holder_pid"

release_lock="$tmp_dir/release.lock"
release_ready="$tmp_dir/release.ready"
(
    # shellcheck source=/dev/null
    . "$helper"
    acquire_bilitool_lock "$release_lock" 0 || exit 1
    : >"$release_ready"
    sleep 0.5
) &
release_pid=$!
for _ in $(seq 1 40); do
    [ -f "$release_ready" ] && break
    sleep 0.05
done
[ -f "$release_ready" ] || fail "release process did not acquire lock"
bash -c '. "$1"; acquire_bilitool_lock "$2" 3' _ "$helper" "$release_lock" || fail "wait=3 did not acquire a released lock"
wait "$release_pid"

if bash -c '. "$1"; acquire_bilitool_lock "$2" -1' _ "$helper" "$tmp_dir/invalid.lock"; then
    fail "negative wait was accepted"
fi
if bash -c '. "$1"; acquire_bilitool_lock "$2" nope' _ "$helper" "$tmp_dir/invalid.lock"; then
    fail "non-numeric wait was accepted"
fi

prod_key="$(bash -c '. "$1"; bilitool_lock_key "$2" "$3"' _ "$helper" "$tmp_dir/prod/repo" main)"
dev_key="$(bash -c '. "$1"; bilitool_lock_key "$2" "$3"' _ "$helper" "$tmp_dir/dev/repo" develop)"
[ "$prod_key" != "$dev_key" ] || fail "prod/dev lock keys are identical"

printf 'PASS: QingLong lock behavior is bounded and branch-scoped\n'
