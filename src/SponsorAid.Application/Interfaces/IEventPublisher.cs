namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Publishes integration events to message brokers without depending on a specific transport technology.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event message to the designated message queue.
    /// </summary>
    /// <typeparam name="TEvent">Type of the event object.</typeparam>
    /// <param name="queueName">Target message broker queue name.</param>
    /// <param name="event">Event message instance.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync<TEvent>(string queueName, TEvent @event) where TEvent : class;
}
