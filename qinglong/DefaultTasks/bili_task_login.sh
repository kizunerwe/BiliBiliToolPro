#!/usr/bin/env bash
# cron:20 0 1 * *
# new Env("bili扫码登录")

. bili_task_base.sh

target_task_code="Login"
run_bilitool_job "${target_task_code}"
