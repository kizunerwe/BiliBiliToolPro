using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

namespace Ray.BiliBiliTool.DomainService;

public static class LiveLotteryRoomMatcher
{
    public static bool IsPotentialLotteryRoom(ListItemDto item)
    {
        if (item.Pendant_info == null || item.Pendant_info.Count == 0)
            return false;

        foreach (PendantInfo pendant in item.Pendant_info.Values)
        {
            if (pendant.Pendent_id == 504)
                return true;

            if (ContainsTianXuanKeyword(pendant.Content) || ContainsTianXuanKeyword(pendant.Name))
                return true;
        }

        return false;
    }

    private static bool ContainsTianXuanKeyword(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("天选", StringComparison.OrdinalIgnoreCase);
    }
}
