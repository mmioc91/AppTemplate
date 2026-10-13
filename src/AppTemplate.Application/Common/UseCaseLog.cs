using Microsoft.Extensions.Logging;

namespace AppTemplate.Application.Common;

// What the logging decorators write about one use case (ADR 0028): its name, how long it took and how it ended —
// never its input, which may hold personal data. A failure is its error code; an exception only its type (the
// exception itself is logged where it is handled).
internal static partial class UseCaseLog
{
    public static async Task<Result> RunAsync(ILogger logger, TimeProvider timeProvider, string useCase, Func<Task<Result>> handle)
    {
        long started = timeProvider.GetTimestamp();

        try
        {
            Result result = await handle();
            Report(logger, useCase, result.Error, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            Threw(logger, useCase, exception.GetType().Name, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    public static async Task<Result<TResult>> RunAsync<TResult>(
        ILogger logger, TimeProvider timeProvider, string useCase, Func<Task<Result<TResult>>> handle)
    {
        long started = timeProvider.GetTimestamp();

        try
        {
            Result<TResult> result = await handle();
            Report(logger, useCase, result.Error, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            Threw(logger, useCase, exception.GetType().Name, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    private static void Report(ILogger logger, string useCase, Error? error, double elapsedMs)
    {
        if (error is null)
        {
            Succeeded(logger, useCase, elapsedMs);
        }
        else
        {
            Failed(logger, useCase, error.Code, error.Kind, elapsedMs);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{UseCase} succeeded in {ElapsedMs:0.0} ms")]
    private static partial void Succeeded(ILogger logger, string useCase, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{UseCase} failed with {ErrorCode} ({ErrorKind}) in {ElapsedMs:0.0} ms")]
    private static partial void Failed(ILogger logger, string useCase, string errorCode, ErrorKind errorKind, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{UseCase} threw {ExceptionType} after {ElapsedMs:0.0} ms")]
    private static partial void Threw(ILogger logger, string useCase, string exceptionType, double elapsedMs);
}
