#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$BASH_SOURCE")/.." && pwd)"
base_script="$repo_root/qinglong/DefaultTasks/bili_task_base.sh"
dev_script="$repo_root/qinglong/DefaultTasks/dev/bili_dev_task_base.sh"
tmp_root="$(mktemp -d)"
trap 'rm -rf "$tmp_root"' EXIT

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

assert_contains() {
    local file="$1"
    local pattern="$2"
    local message="$3"
    grep -Eq -- "$pattern" "$file" || fail "$message"
}

assert_not_contains() {
    local file="$1"
    local pattern="$2"
    local message="$3"
    if grep -Eq -- "$pattern" "$file"; then
        fail "$message"
    fi
}

assert_eq() {
    local expected="$1"
    local actual="$2"
    local message="$3"
    [ "$expected" = "$actual" ] || fail "$message: expected '$expected', got '$actual'"
}

for script in "$base_script" "$dev_script"; do
    bash -n "$script"
    assert_contains "$script" 'bilitool_lock\.sh' "$(basename "$script") should load the shared lock helper"
    assert_contains "$script" 'BILITOOL_LOCK_WAIT_SECONDS' "$(basename "$script") should support bounded lock waits"
    assert_contains "$script" 'acquire_bilitool_lock' "$(basename "$script") should acquire the shared lock"
    assert_contains "$script" 'check_installed.*\|\|' "$(basename "$script") should verify installation before running"
    assert_contains "$script" 'curl[^\n]*--fail' "$(basename "$script") downloads should fail on HTTP errors"
    assert_contains "$script" 'dotnet publish' "$(basename "$script") should publish once before execution"
    assert_not_contains "$script" 'dotnet run' "$(basename "$script") should not compile on every scheduled run"
    assert_not_contains "$script" 'www\.google\.com' "$(basename "$script") should not infer mirror settings from Google reachability"
    assert_contains "$script" 'should_check_bilitool_update' "$(basename "$script") should use cached update decisions"
    assert_contains "$script" 'BILITOOL_UPDATE_CHECK_INTERVAL_SECONDS' "$(basename "$script") should support update interval overrides"
done

BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 100000 "" 86400
' || fail 'empty check time should force an update check'
BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 200000 113600 86400
' || fail '24 hour interval should trigger an update check'
if BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 200000 117200 86400
'; then
    fail '23 hour interval should skip an update check'
fi
BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 200000 corrupted 86400
' || fail 'corrupt check time should force an update check without arithmetic errors'
BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 200000 100000 -1
' || fail 'negative update interval should not be accepted'
BASE_SCRIPT="$base_script" BILITOOL_BASE_LIBRARY_ONLY=true bash -c '
    . "$BASE_SCRIPT"
    should_check_bilitool_update 200000 100000 invalid
' || fail 'non-numeric update interval should not cause an arithmetic error'

stub_dir="$tmp_root/stubs"
mkdir -p "$stub_dir"
real_mv="$(command -v mv)"

printf '%s\n' '#!/usr/bin/env bash' 'printf "%s\n" "$TEST_NOW"' >"$stub_dir/date"
printf '%s\n' '#!/usr/bin/env bash' \
    'set -euo pipefail' \
    'printf "%s\n" "$*" >>"$TEST_CURL_LOG"' \
    'if [[ "$*" == *api.github.com* ]]; then' \
    '    printf "{\"tag_name\":\"v2\"}\n"' \
    '    exit 0' \
    'fi' \
    '[ "$FAIL_MODE" = download ] && exit 1' \
    'output=""' \
    'previous=""' \
    'for arg in "$@"; do' \
    '    if [ "$previous" = -o ]; then output="$arg"; break; fi' \
    '    previous="$arg"' \
    'done' \
    '[ -n "$output" ] || exit 1' \
    'printf "%s" "fake zip" >"$output"' >"$stub_dir/curl"
printf '%s\n' '#!/usr/bin/env bash' \
    'if [ "$FAIL_MODE" = parse ]; then printf "%s\n" null; else printf "%s\n" v2; fi' >"$stub_dir/jq"
printf '%s\n' '#!/usr/bin/env bash' \
    'set -euo pipefail' \
    '[ "$FAIL_MODE" = unzip ] && exit 1' \
    'output=""' \
    'for arg in "$@"; do output="$arg"; done' \
    'mkdir -p "$output"' \
    'printf "%s\n" new-binary >"$output/Ray.BiliBiliTool.Console"' \
    'chmod +x "$output/Ray.BiliBiliTool.Console"' >"$stub_dir/unzip"
printf '%s\n' '#!/usr/bin/env bash' \
    'set -euo pipefail' \
    'if [ "$FAIL_MODE" = replace ] && [[ "$*" == *current.tmp* ]]; then exit 1; fi' \
    'exec "$REAL_MV" "$@"' >"$stub_dir/mv"
chmod +x "$stub_dir"/*

run_update_case() {
    local case_name="$1"
    local now="$2"
    local checked="$3"
    local interval="$4"
    local fail_mode=""
    if [ "$#" -ge 5 ]; then
        fail_mode="$5"
    fi
    local case_dir="$tmp_root/$case_name"
    local bin_dir="$case_dir/bin"
    local curl_log="$case_dir/curl.log"
    mkdir -p "$bin_dir"
    : >"$curl_log"

    if [ "$case_name" != first ]; then
        printf '%s\n' old-binary >"$bin_dir/Ray.BiliBiliTool.Console"
        printf '%s\n' v1 >"$bin_dir/tag.txt"
        printf '%s\n' "$checked" >"$bin_dir/.bilitool-update-checked-at"
    fi

    PATH="$stub_dir:$PATH" \
        REAL_MV="$real_mv" \
        TEST_NOW="$now" \
        TEST_CURL_LOG="$curl_log" \
        FAIL_MODE="$fail_mode" \
        BASE_SCRIPT="$base_script" \
        BIN_DIR="$bin_dir" \
        INTERVAL="$interval" \
        bash -c '
            set -euo pipefail
            BILITOOL_BASE_LIBRARY_ONLY=true
            . "$BASE_SCRIPT"
            qinglong_bili_repo_dir="$(dirname "$BIN_DIR")"
            update_checked_file="$BIN_DIR/.bilitool-update-checked-at"
            update_check_interval_seconds="$INTERVAL"
            current_os=linux
            machine_architecture=x64
            bili_repo=test/repo
            cd "$BIN_DIR"
            check_jq() { :; }
            check_unzip() { :; }
            check_bilitool() {
                printf "%s\n" called >>"$TEST_CURL_LOG.check"
                local runtime_dir="$BIN_DIR"
                if [ -e "$BIN_DIR/.bilitool-current" ]; then
                    runtime_dir="$BIN_DIR/.bilitool-current"
                fi
                if [ -f "$runtime_dir/Ray.BiliBiliTool.Console" ] && [ -f "$runtime_dir/tag.txt" ]; then
                    bilitool_installed_version="$(cat "$runtime_dir/tag.txt")"
                    bilitool_installed_dir="$runtime_dir"
                    return 0
                fi
                return 1
            }
            update_bilitool_if_due
            [ -f "$bilitool_installed_dir/Ray.BiliBiliTool.Console" ]
        '
}

runtime_file() {
    local case_dir="$1"
    local file_name="$2"
    if [ -e "$case_dir/bin/.bilitool-current" ]; then
        printf '%s\n' "$case_dir/bin/.bilitool-current/$file_name"
    else
        printf '%s\n' "$case_dir/bin/$file_name"
    fi
}

run_update_case first 100000 0 86400
assert_eq 2 "$(wc -l <"$tmp_root/first/curl.log" | tr -d ' ')" 'first check should query release and download'
assert_eq new-binary "$(tr -d '\n' <"$(runtime_file "$tmp_root/first" Ray.BiliBiliTool.Console)")" 'first check should install the new binary'
assert_eq v2 "$(tr -d '\n' <"$(runtime_file "$tmp_root/first" tag.txt)")" 'first check should write the new tag'
assert_eq 100000 "$(tr -d '\n' <"$tmp_root/first/bin/.bilitool-update-checked-at")" 'first check should write the check time'

run_update_case skip_23h 200000 117200 86400
assert_eq 0 "$(wc -l <"$tmp_root/skip_23h/curl.log" | tr -d ' ')" '23 hour check should skip release lookup'
assert_eq old-binary "$(tr -d '\n' <"$tmp_root/skip_23h/bin/Ray.BiliBiliTool.Console")" '23 hour check should preserve binary'
assert_eq 117200 "$(tr -d '\n' <"$tmp_root/skip_23h/bin/.bilitool-update-checked-at")" '23 hour check should preserve check time'

run_update_case due_24h 200000 113600 86400
assert_eq 2 "$(wc -l <"$tmp_root/due_24h/curl.log" | tr -d ' ')" '24 hour check should query release and download'

run_update_case custom_interval 200000 196400 3600
assert_eq 2 "$(wc -l <"$tmp_root/custom_interval/curl.log" | tr -d ' ')" 'custom interval should control the update decision'

for fail_mode in download parse unzip replace; do
    if run_update_case "failure_$fail_mode" 200000 100000 86400 "$fail_mode"; then
        fail "$fail_mode failure should be visible"
    fi
    assert_eq old-binary "$(tr -d '\n' <"$tmp_root/failure_$fail_mode/bin/Ray.BiliBiliTool.Console")" "$fail_mode failure should preserve binary"
    assert_eq v1 "$(tr -d '\n' <"$tmp_root/failure_$fail_mode/bin/tag.txt")" "$fail_mode failure should preserve tag"
    assert_eq 100000 "$(tr -d '\n' <"$tmp_root/failure_$fail_mode/bin/.bilitool-update-checked-at")" "$fail_mode failure should preserve check time"
    if [ "$fail_mode" = download ]; then
        assert_eq 2 "$(wc -l <"$tmp_root/failure_download/curl.log" | tr -d ' ')" 'download failure should reach archive download after release metadata succeeds'
    fi
done

printf 'PASS: QingLong bilitool update cache and failure retention checks passed\n'
