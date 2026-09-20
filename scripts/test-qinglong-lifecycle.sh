#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
base_script="$repo_root/qinglong/DefaultTasks/bili_task_base.sh"
lock_helper="$repo_root/qinglong/DefaultTasks/bili_task_bilitool_lock.sh"
tmp_dir="$(mktemp -d)"

cleanup() {
    if [ -n "${holder_pid:-}" ]; then
        kill "$holder_pid" 2>/dev/null || true
        wait "$holder_pid" 2>/dev/null || true
    fi
    rm -rf "$tmp_dir"
}
trap cleanup EXIT

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

wait_for_file() {
    local file="$1"
    local attempts="${2:-100}"
    for _ in $(seq 1 "$attempts"); do
        [ -f "$file" ] && return 0
        sleep 0.05
    done
    return 1
}

find_descendant_by_name() {
    local parent_pid="$1"
    local process_name="$2"
    local child_pid found_pid

    for child_pid in $(pgrep -P "$parent_pid" 2>/dev/null || true); do
        if [ "$(ps -o comm= -p "$child_pid" 2>/dev/null)" = "$process_name" ]; then
            printf '%s\n' "$child_pid"
            return 0
        fi
        found_pid="$(find_descendant_by_name "$child_pid" "$process_name" || true)"
        if [ -n "$found_pid" ]; then
            printf '%s\n' "$found_pid"
            return 0
        fi
    done

    return 1
}

if ! command -v setsid >/dev/null 2>&1 \
    && command -v perl >/dev/null 2>&1 \
    && perl -MPOSIX -e 'exit(POSIX->can("setsid") ? 0 : 1)'; then
    test_bin="$tmp_dir/test-bin"
    mkdir -p "$test_bin"
    cat >"$test_bin/setsid" <<'EOF'
#!/usr/bin/env perl
use strict;
use warnings;
use POSIX qw(setsid);
setsid() >= 0 or die "setsid failed: $!";
exec @ARGV or die "exec failed: $!";
EOF
    chmod +x "$test_bin/setsid"
    PATH="$test_bin:$PATH"
    export PATH
fi

option_state="$tmp_dir/options"
BASE_SCRIPT="$base_script" bash -c '
    set +e +u
    set +o pipefail
    before="$(set +o | grep -E "errexit|nounset|pipefail")"
    BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
    after="$(set +o | grep -E "errexit|nounset|pipefail")"
    [ "$before" = "$after" ]
' >"$option_state" || fail "sourcing base changed caller shell options"

after_marker="$tmp_dir/after"
BASE_SCRIPT="$base_script" AFTER_MARKER="$after_marker" bash -c '
    set +e
    BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
    run_managed_process bash -c "exit 23"
    runtime_status=$?
    printf "%s\n" "$runtime_status" >"$AFTER_MARKER"
' || fail "source wrapper stopped before its cleanup marker"
[ "$(cat "$after_marker")" = 23 ] || fail "runtime exit status was not preserved"

spawn_barrier="$tmp_dir/spawn.barrier"
spawn_release="$tmp_dir/spawn.release"
spawn_after="$tmp_dir/spawn.after"
BASE_SCRIPT="$base_script" SPAWN_BARRIER="$spawn_barrier" SPAWN_RELEASE="$spawn_release" \
    SPAWN_AFTER="$spawn_after" BILITOOL_TEST_BEFORE_WORKER_SPAWN_HOOK=': >"$SPAWN_BARRIER"; while [ ! -f "$SPAWN_RELEASE" ]; do sleep 0.05; done' \
    bash -c '
        set +e
        BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
        run_bilitool_job Daily
        status=$?
        printf "%s\n" "$status" >"$SPAWN_AFTER"
    ' &
spawn_wrapper_pid=$!
wait_for_file "$spawn_barrier" || fail "spawn barrier hook was not reached"
kill -TERM "$spawn_wrapper_pid"
: >"$spawn_release"
wait "$spawn_wrapper_pid" || true
wait_for_file "$spawn_after" || fail "spawn-race wrapper skipped its after marker"
[ "$(cat "$spawn_after")" = 143 ] || fail "spawn-race TERM should return 143"

if ! command -v setsid >/dev/null 2>&1; then
    printf 'SKIP: setsid unavailable; source, failure-cleanup, and spawn-race checks passed\n'
    exit 0
fi

phase_child_pid_file="$tmp_dir/phase-child.pid"
phase_term_file="$tmp_dir/phase-child.term"
phase_after="$tmp_dir/phase.after"
phase_setup="$tmp_dir/phase-setup.sh"
cat >"$phase_setup" <<'EOF'
initialize_bilitool_context() { :; }
check_os() { :; }
install() {
    bash -c '
        printf "%s\n" "$$" >"$PHASE_CHILD_PID_FILE"
        trap "printf term >\"$PHASE_TERM_FILE\"; exit 0" TERM INT HUP
        while :; do sleep 1; done
    '
}
run_task() { exit 99; }
EOF
BASE_SCRIPT="$base_script" PHASE_CHILD_PID_FILE="$phase_child_pid_file" \
    PHASE_TERM_FILE="$phase_term_file" PHASE_AFTER="$phase_after" \
    BILITOOL_TEST_WORKER_SETUP="$phase_setup" BILITOOL_STOP_GRACE_SECONDS=2 bash -c '
        set +e
        BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
        run_bilitool_job Daily
        status=$?
        printf "%s\n" "$status" >"$PHASE_AFTER"
    ' &
phase_wrapper_pid=$!
wait_for_file "$phase_child_pid_file" || fail "long install phase did not start"
phase_child_pid="$(cat "$phase_child_pid_file")"
kill -TERM "$phase_wrapper_pid"
wait "$phase_wrapper_pid" || true
wait_for_file "$phase_after" || fail "install-phase wrapper skipped its after marker"
[ "$(cat "$phase_after")" = 143 ] || fail "install-phase TERM should return 143"
wait_for_file "$phase_term_file" || fail "TERM did not reach long install child"
kill -0 "$phase_child_pid" 2>/dev/null && fail "long install child survived TERM"

publish_child_pid_file="$tmp_dir/publish-child.pid"
publish_term_file="$tmp_dir/publish-child.term"
publish_after="$tmp_dir/publish.after"
publish_setup="$tmp_dir/publish-setup.sh"
cat >"$publish_setup" <<'EOF'
initialize_bilitool_context() { :; }
check_os() { :; }
install() { :; }
run_task() {
    bash -c '
        printf "%s\n" "$$" >"$PUBLISH_CHILD_PID_FILE"
        trap "printf term >\"$PUBLISH_TERM_FILE\"; exit 0" TERM INT HUP
        while :; do sleep 1; done
    '
}
EOF
BASE_SCRIPT="$base_script" PUBLISH_CHILD_PID_FILE="$publish_child_pid_file" \
    PUBLISH_TERM_FILE="$publish_term_file" PUBLISH_AFTER="$publish_after" \
    BILITOOL_TEST_WORKER_SETUP="$publish_setup" BILITOOL_STOP_GRACE_SECONDS=2 bash -c '
        set +e
        BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
        run_bilitool_job Daily
        status=$?
        printf "%s\n" "$status" >"$PUBLISH_AFTER"
    ' &
publish_wrapper_pid=$!
wait_for_file "$publish_child_pid_file" || fail "long publish phase did not start"
publish_child_pid="$(cat "$publish_child_pid_file")"
kill -TERM "$publish_wrapper_pid"
wait "$publish_wrapper_pid" || true
wait_for_file "$publish_after" || fail "publish-phase wrapper skipped its after marker"
[ "$(cat "$publish_after")" = 143 ] || fail "publish-phase TERM should return 143"
wait_for_file "$publish_term_file" || fail "TERM did not reach long publish child"
kill -0 "$publish_child_pid" 2>/dev/null && fail "long publish child survived TERM"

if ! command -v flock >/dev/null 2>&1; then
    printf 'SKIP: flock unavailable; source, spawn-race, install, and publish lifecycle checks passed\n'
    exit 0
fi

runtime_pid_file="$tmp_dir/runtime.pid"
runtime_term_file="$tmp_dir/runtime.term"
runtime_lock="$tmp_dir/runtime.lock"
runtime_setup="$tmp_dir/runtime-setup.sh"
cat >"$runtime_setup" <<'EOF'
initialize_bilitool_context() {
    acquire_bilitool_lock "$RUNTIME_LOCK" 0
}
check_os() { :; }
install() { :; }
run_task() {
    run_managed_process bash -c '
        printf "%s\n" "$$" >"$RUNTIME_PID_FILE"
        trap "printf term >\"$RUNTIME_TERM_FILE\"; exit 0" TERM INT HUP
        while :; do sleep 1; done
    '
}
EOF
BASE_SCRIPT="$base_script" RUNTIME_PID_FILE="$runtime_pid_file" \
    RUNTIME_TERM_FILE="$runtime_term_file" RUNTIME_LOCK="$runtime_lock" \
    BILITOOL_TEST_WORKER_SETUP="$runtime_setup" BILITOOL_STOP_GRACE_SECONDS=2 bash -c '
        BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
        run_bilitool_job Daily
    ' &
wrapper_pid=$!
wait_for_file "$runtime_pid_file" || fail "managed runtime did not start"
runtime_pid="$(cat "$runtime_pid_file")"
kill -TERM "$wrapper_pid"
set +e
wait "$wrapper_pid"
wrapper_status=$?
set -e
[ "$wrapper_status" -eq 143 ] || fail "TERM should return 143, got $wrapper_status"
wait_for_file "$runtime_term_file" || fail "TERM was not forwarded to runtime"
if kill -0 "$runtime_pid" 2>/dev/null; then
    fail "runtime process survived wrapper termination"
fi
bash -c '. "$1"; acquire_bilitool_lock "$2" 0' _ "$lock_helper" "$runtime_lock" \
    || fail "lock remained held after runtime termination"

held_lock="$tmp_dir/held.lock"
holder_ready="$tmp_dir/holder.ready"
(
    . "$lock_helper"
    acquire_bilitool_lock "$held_lock" 0 || exit 1
    : >"$holder_ready"
    sleep 20
) &
holder_pid=$!
wait_for_file "$holder_ready" || fail "lock holder did not start"

waiter_started="$tmp_dir/waiter.started"
waiter_setup="$tmp_dir/waiter-setup.sh"
cat >"$waiter_setup" <<'EOF'
initialize_bilitool_context() {
    : >"$WAITER_STARTED"
    acquire_bilitool_lock "$HELD_LOCK" 30
}
check_os() { :; }
install() { :; }
run_task() { exit 99; }
EOF
BASE_SCRIPT="$base_script" HELD_LOCK="$held_lock" WAITER_STARTED="$waiter_started" \
    BILITOOL_TEST_WORKER_SETUP="$waiter_setup" bash -c '
    BILITOOL_BASE_LIBRARY_ONLY=true . "$BASE_SCRIPT"
    run_bilitool_job Daily
' &
waiter_pid=$!
wait_for_file "$waiter_started" || fail "lock waiter did not start"
sleep 0.2
waiter_worker_pid="$(pgrep -P "$waiter_pid" | head -1)"
[ -n "$waiter_worker_pid" ] || fail "lock waiter worker PID was not found"
waiter_flock_pid="$(find_descendant_by_name "$waiter_worker_pid" flock || true)"
[ -n "$waiter_flock_pid" ] || fail "flock waiter PID was not found"
kill -TERM "$waiter_pid"
set +e
wait "$waiter_pid"
waiter_status=$?
set -e
[ "$waiter_status" -eq 143 ] || fail "lock waiter TERM should return 143, got $waiter_status"
kill -0 "$waiter_worker_pid" 2>/dev/null && fail "lock worker remained after termination"
kill -0 "$waiter_flock_pid" 2>/dev/null && fail "flock child remained after termination"

printf 'PASS: QingLong source, signal forwarding, runtime cleanup, and lock lifecycle checks passed\n'
