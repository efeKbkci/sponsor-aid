using Xunit;
using MediatR;
using SponsorAid.Application.Behaviors;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Tests.Behaviors;

/// <summary>
/// Unit tests for <see cref="RequestSerializationBehavior{TRequest, TResponse}"/>.
/// Verifies that concurrent requests sharing the same registration key are queued and processed sequentially
/// to prevent race conditions during state transitions.
/// </summary>
public class RequestSerializationBehaviorTests
{
    private record TestRegistrationCommand(string RegistrationKey) : IRequest<string>, IRegistrationCommand;

    [Fact]
    public async Task Handle_WhenConcurrentRequestsWithSameKeyArrive_ShouldProcessThemSequentially()
    {
        // Arrange
        var behavior = new RequestSerializationBehavior<TestRegistrationCommand, string>();
        var command = new TestRegistrationCommand("KEY_ABC");

        int executionCount = 0;
        int maxConcurrentExecutions = 0;
        int activeExecutions = 0;

        // Pipeline delegate simulating the next request handler in the MediatR pipeline
        RequestHandlerDelegate<string> next = async (_) =>
        {
            // Track concurrent execution count to detect overlapping handlers
            Interlocked.Increment(ref activeExecutions);
            lock (typeof(RequestSerializationBehaviorTests))
            {
                if (activeExecutions > maxConcurrentExecutions)
                {
                    maxConcurrentExecutions = activeExecutions;
                }
            }

            // Introduce an artificial delay to allow overlapping concurrent calls if locking fails
            await Task.Delay(50);

            Interlocked.Decrement(ref activeExecutions);
            Interlocked.Increment(ref executionCount);
            return "Success";
        };

        // Act
        // Dispatch 5 concurrent requests with identical registration keys to test synchronization lock
        var task1 = behavior.Handle(command, next, CancellationToken.None);
        var task2 = behavior.Handle(command, next, CancellationToken.None);
        var task3 = behavior.Handle(command, next, CancellationToken.None);
        var task4 = behavior.Handle(command, next, CancellationToken.None);
        var task5 = behavior.Handle(command, next, CancellationToken.None);

        await Task.WhenAll(task1, task2, task3, task4, task5);

        // Assert
        // Verify all 5 requests completed successfully
        Assert.Equal(5, executionCount);

        // Verify peak concurrency remained at 1, ensuring sequential execution without race conditions
        Assert.Equal(1, maxConcurrentExecutions);
    }
}
