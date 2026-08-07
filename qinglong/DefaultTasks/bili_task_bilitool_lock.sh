#!/usr/bin/env bash

bilitool_lock_key() {
    local repo_dir="$1"
    local branch="$2"
    local normalized_repo
    local normalized_branch

    normalized_repo="$(printf '%s' "$repo_dir" | sed 's/[^[:alnum:]_.-]/_/g')"
    if [ -n "$branch" ]; then
        normalized_branch="$(printf '%s' "$branch" | sed 's/[^[:alnum:]_.-]/_/g')"
    else
        normalized_branch=default
    fi
    printf '%s-%s' "$normalized_repo" "$normalized_branch"
}

acquire_bilitool_lock() {
    local lock_file="$1"
    local wait_seconds="$2"

    case "$wait_seconds" in
        ''|*[!0-9]*)
            return 2
            ;;
    esac

    exec 9>"$lock_file" || return 1
    flock -w "$wait_seconds" 9
}
