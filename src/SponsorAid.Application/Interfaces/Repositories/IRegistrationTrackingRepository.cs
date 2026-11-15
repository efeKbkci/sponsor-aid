using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Interfaces.Repositories;

/// <summary>
/// Tracks high-level lifecycle status and ownership for applicant registrations.
/// </summary>
public interface IRegistrationTrackingRepository
{
    /// <summary>
    /// Creates a tracking record for a new registration session linked to a chat.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration session.</param>
    /// <param name="chatId">Identifier of the chat where the registration began.</param>
    /// <param name="cancellationToken">Token to cancel the creation task.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    Task CreateAsync(string registrationKey, long chatId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tracking record when an intake process is cancelled.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration to remove.</param>
    /// <param name="cancellationToken">Token to cancel the removal task.</param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    Task RemoveAsync(string registrationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the guardian name associated with an active registration session.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration.</param>
    /// <param name="guardianName">Full name of the applicant guardian.</param>
    /// <param name="cancellationToken">Token to cancel the update task.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateGuardianNameAsync(string registrationKey, string guardianName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a registration tracking record as completed after dispatch.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration.</param>
    /// <param name="cancellationToken">Token to cancel the update task.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task MarkAsCompletedAsync(string registrationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registration tracking entries and statuses belonging to a specific chat.
    /// </summary>
    /// <param name="chatId">Chat identifier to query.</param>
    /// <param name="cancellationToken">Token to cancel the listing task.</param>
    /// <returns>A collection of registration key, guardian name, and status tuples.</returns>
    Task<IEnumerable<(string RegistrationKey, string? GuardianName, RegistrationStatus Status)>> ListByChatIdAsync(long chatId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current lifecycle status of a specific registration.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration.</param>
    /// <param name="cancellationToken">Token to cancel the retrieval task.</param>
    /// <returns>Current registration status if found; otherwise null.</returns>
    Task<RegistrationStatus?> GetStatusAsync(string registrationKey, CancellationToken cancellationToken = default);
}
