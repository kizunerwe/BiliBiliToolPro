namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;

public sealed class CreateFavoriteFolderRequest(string folderTitle, string csrfToken)
{
    public string title { get; set; } = folderTitle;
    public int privacy { get; set; } = 0;
    public string csrf { get; set; } = csrfToken;
}
