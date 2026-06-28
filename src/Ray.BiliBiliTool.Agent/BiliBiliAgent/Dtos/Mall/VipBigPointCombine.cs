using Microsoft.Extensions.Logging;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;

public class VipBigPointCombine
{
    public required PointInfo point_info { get; set; }
    public required TaskInfo Task_info { get; set; }

    public void LogFullInfo(ILogger logger)
    {
        logger.LogInformation("当前经验：{point}", point_info.point);
        foreach (var moduleItem in Task_info.Modules)
        {
            var visibleTasks = moduleItem
                .common_task_item.Where(x => !VipBigPointTaskCatalog.IsAutomationUnsupported(x))
                .ToList();
            if (visibleTasks.Count == 0)
                continue;

            logger.LogInformation("-{title}", moduleItem.module_title);
            foreach (var commonTaskItem in visibleTasks)
            {
                logger.LogInformation(
                    "---{title}：{status}",
                    commonTaskItem.title,
                    commonTaskItem.state == 3 ? "√" : "X"
                );
            }
        }
    }

    public void LogPointInfo(ILogger logger)
    {
        logger.LogInformation("当前经验：{point}", point_info.point);
    }
}
