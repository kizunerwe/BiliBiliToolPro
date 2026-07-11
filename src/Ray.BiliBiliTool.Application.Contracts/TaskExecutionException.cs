namespace Ray.BiliBiliTool.Application.Contracts;

public sealed class TaskExecutionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
