#!/usr/bin/env bash
# cron:10 0 * * *
# new Env("bili直播粉丝牌[dev先行版]")

. bili_dev_task_base.sh

target_task_code="LiveFansMedal"
run_bilitool_job "${target_task_code}"
