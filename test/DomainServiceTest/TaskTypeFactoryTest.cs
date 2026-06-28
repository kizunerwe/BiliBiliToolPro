using Ray.BiliBiliTool.Application.Contracts;

namespace DomainServiceTest;

public class TaskTypeFactoryTest
{
    [Fact]
    public void Get_ShouldResolveStandaloneAccessKeyTask()
    {
        Type type = TaskTypeFactory.Get("AccessKey");

        Assert.Equal("IAccessKeyTaskAppService", type.Name);
    }
}
