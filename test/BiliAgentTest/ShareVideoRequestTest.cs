using System.Threading.Tasks;
using System.Web;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using WebApiClientCore.HttpContents;
using WebApiClientCore.Serialization;
using Xunit;

namespace BiliAgentTest;

public class ShareVideoRequestTest
{
    [Fact]
    public async Task FormContent_ShouldMatchBrowserShareParameters()
    {
        var request = new ShareVideoRequest(123456789, "test-csrf");
        using var content = new FormContent(request, new KeyValueSerializerOptions());

        var form = HttpUtility.ParseQueryString(await content.ReadAsStringAsync());

        Assert.Equal("1", form["eab_x"]);
        Assert.True(int.TryParse(form["ramval"], out var ramval) && ramval is >= 3 and <= 19);
        Assert.Equal("web_normal", form["source"]);
        Assert.Equal("1", form["ga"]);
    }
}
