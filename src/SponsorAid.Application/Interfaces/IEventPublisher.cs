namespace SponsorAid.Application.Interfaces;

public interface IEventPublisher
{
    // Kuyruk teknolojisi ne olursa olsun, bir mesajı (event) hedefe gönderir.
    Task PublishAsync<TEvent>(string queueName, TEvent @event) where TEvent : class;
}