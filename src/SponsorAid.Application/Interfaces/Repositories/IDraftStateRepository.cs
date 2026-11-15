using SponsorAid.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces.Repositories;

/// <summary>
/// Provides storage operations for applicant registration drafts.
/// </summary>
public interface IDraftStateRepository
{
    /// <summary>
    /// Retrieves a registration draft by its session key.
    /// </summary>
    /// <param name="registrationKey">Unique key of the registration session.</param>
    /// <param name="cancellationToken">Token to cancel the retrieval task.</param>
    /// <returns>The registration draft if found; otherwise null.</returns>
    Task<RegistrationDraftState?> GetAsync(string registrationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the state of a registration draft.
    /// </summary>
    /// <param name="state">The draft state to persist.</param>
    /// <param name="cancellationToken">Token to cancel the save task.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveAsync(RegistrationDraftState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a registration draft after the application is submitted or cancelled.
    /// </summary>
    /// <param name="registrationKey">Unique key of the draft to remove.</param>
    /// <param name="cancellationToken">Token to cancel the delete task.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task DeleteAsync(string registrationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registration drafts associated with a given chat identifier.
    /// </summary>
    /// <param name="chatId">Chat identifier to filter drafts.</param>
    /// <param name="cancellationToken">Token to cancel the list task.</param>
    /// <returns>A collection of matching registration drafts.</returns>
    Task<IEnumerable<RegistrationDraftState>> ListAllAsync(string chatId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the single active draft for an ongoing chat conversation.
    /// </summary>
    /// <param name="chatId">Chat identifier of the conversation.</param>
    /// <param name="cancellationToken">Token to cancel the retrieval task.</param>
    /// <returns>The active draft state if present; otherwise null.</returns>
    Task<RegistrationDraftState?> GetActiveByChatIdAsync(long chatId, CancellationToken cancellationToken = default);
}
