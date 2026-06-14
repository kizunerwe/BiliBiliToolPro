using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public class QingLongCookieRemarkFormatterTest
{
    [Fact]
    public void Should_build_new_auto_remark_from_user_name_and_uid()
    {
        var remark = QingLongCookieRemarkFormatter.ResolveRemark(
            "123456",
            "kizunerwe",
            currentRemark: null
        );

        Assert.Equal("kizunerwe | 123456", remark);
    }

    [Fact]
    public void Should_upgrade_legacy_auto_remark()
    {
        var remark = QingLongCookieRemarkFormatter.ResolveRemark(
            "123456",
            "kizunerwe",
            "bili-123456"
        );

        Assert.Equal("kizunerwe | 123456", remark);
    }

    [Fact]
    public void Should_preserve_custom_remark()
    {
        var remark = QingLongCookieRemarkFormatter.ResolveRemark(
            "123456",
            "kizunerwe",
            "主号 | 收藏夹专用"
        );

        Assert.Equal("主号 | 收藏夹专用", remark);
    }
}
