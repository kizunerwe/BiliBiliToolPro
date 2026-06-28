namespace DomainServiceTest;

internal static class GlobalServiceProviderTestLock
{
    public static readonly SemaphoreSlim Gate = new(1, 1);
}
