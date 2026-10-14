using Xunit;
using MediatR;
using SponsorAid.Application.Behaviors;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Tests.Behaviors;

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

        // Handler yerine geçecek sahte bir delegate (sonraki adım)
        RequestHandlerDelegate<string> next = async (_) =>
        {
            // Aynı anda kaç tane isteğin içeride olduğunu takip ediyoruz
            Interlocked.Increment(ref activeExecutions);
            lock (typeof(RequestSerializationBehaviorTests))
            {
                if (activeExecutions > maxConcurrentExecutions)
                {
                    maxConcurrentExecutions = activeExecutions;
                }
            }

            // İşlemin sürdüğünü simüle etmek için kısa bir gecikme
            await Task.Delay(50);

            Interlocked.Decrement(ref activeExecutions);
            Interlocked.Increment(ref executionCount);
            return "Success";
        };

        // Act
        // Aynı anda 5 tane isteği aynı kayıt anahtarıyla fırlatıyoruz (Concurrency simülasyonu)
        var task1 = behavior.Handle(command, next, CancellationToken.None);
        var task2 = behavior.Handle(command, next, CancellationToken.None);
        var task3 = behavior.Handle(command, next, CancellationToken.None);
        var task4 = behavior.Handle(command, next, CancellationToken.None);
        var task5 = behavior.Handle(command, next, CancellationToken.None);

        await Task.WhenAll(task1, task2, task3, task4, task5);

        // Assert
        // 1. Tüm istekler başarıyla tamamlanmış olmalı
        Assert.Equal(5, executionCount);

        // 2. KRİTİK DOĞRULAMA: Aynı anda içeride çalışan maksimum istek sayısı KESİNLİKLE 1 olmalı.
        // Eğer kilit mekanizması çalışmasaydı bu sayı 5 olacaktı.
        Assert.Equal(1, maxConcurrentExecutions);
    }
}