using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using SponsorAid.Application.Interfaces;
using SponsorAid.Infrastructure.Persistence;
using SponsorAid.Infrastructure.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace SponsorAid.Tests;

public class SponsorAidTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    // Global Mock Nesneleri
    public Mock<IUserNotifier> UserNotifierMock { get; } = new();
    public Mock<IFileStorageService> FileStorageMock { get; } = new();
    public Mock<IEventPublisher> EventPublisherMock { get; } = new();
    public Mock<IEGovermentDocumentService> EGovernmentServiceMock { get; } = new();
    public Mock<ISemanticMatcher> SemanticMatcherMock { get; } = new();
    public Mock<IRegistrationDispatchService> RegistrationDispatchMock { get; } = new();

    public SponsorAidTestFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("sponsoraid_test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<SponsorAidDbContext>>();
                services.RemoveAll<SponsorAidDbContext>();
                services.AddDbContext<SponsorAidDbContext>(options =>
                {
                    options.UseNpgsql(_postgreSqlContainer.GetConnectionString());
                });

                var hostedService = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(WorkerResultConsumerService));
                if (hostedService != null) services.Remove(hostedService);

                services.RemoveAll<IUserNotifier>();
                services.AddSingleton(UserNotifierMock.Object);

                services.RemoveAll<IFileStorageService>();
                services.AddSingleton(FileStorageMock.Object);

                services.RemoveAll<IEventPublisher>();
                services.AddSingleton(EventPublisherMock.Object);

                services.RemoveAll<IEGovermentDocumentService>();
                services.AddSingleton(EGovernmentServiceMock.Object);

                services.RemoveAll<ISemanticMatcher>();
                services.AddSingleton(SemanticMatcherMock.Object);

                services.RemoveAll<IRegistrationDispatchService>();
                services.AddSingleton(RegistrationDispatchMock.Object);
            });
        });

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgreSqlContainer.DisposeAsync();
    }

    public void ResetMocks()
    {
        UserNotifierMock.Invocations.Clear();
        FileStorageMock.Invocations.Clear();
        EventPublisherMock.Invocations.Clear();
        EGovernmentServiceMock.Invocations.Clear();
        SemanticMatcherMock.Invocations.Clear();
        RegistrationDispatchMock.Invocations.Clear();
    }
}

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<SponsorAidTestFixture>
{
    // xUnit'in Collection tanımlaması için içi boş bırakılır.
}