using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Delivers interactive conversational messages and documents to chat users.
/// </summary>
public interface IUserNotifier
{
    /// <summary>
    /// Sends a text message to the specified conversation.
    /// </summary>
    /// <param name="chatId">Unique identifier of the chat conversation.</param>
    /// <param name="message">Text message to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendMessageAsync(long chatId, string message);

    /// <summary>
    /// Sends a document file to the applicant with an optional caption.
    /// </summary>
    /// <param name="chatId">Unique identifier of the chat conversation.</param>
    /// <param name="filePath">Physical path of the file to send.</param>
    /// <param name="caption">Optional descriptive text displayed under the file.</param>
    /// <param name="cancellationToken">Token to cancel the transmission task.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendDocumentAsync(long chatId, string filePath, string? caption = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a question with interactive choice buttons.
    /// </summary>
    /// <param name="chatId">Unique identifier of the chat conversation.</param>
    /// <param name="question">Question text displayed to the applicant.</param>
    /// <param name="options">Collection of option values and button labels.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendQuestionWithButtonsAsync(long chatId, string question, Dictionary<string, string> options);

    /// <summary>
    /// Updates a previously sent question to display the chosen answer and prevent duplicate selections.
    /// </summary>
    /// <param name="chatId">Unique identifier of the chat conversation.</param>
    /// <param name="messageId">Identifier of the original question message.</param>
    /// <param name="originalQuestion">The original question text.</param>
    /// <param name="selectedOptionLabel">The label of the selected answer.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateMessageAsAnsweredAsync(long chatId, int messageId, string originalQuestion, string selectedOptionLabel);

    /// <summary>
    /// Removes interactive buttons from a message once an answer is received or cancelled.
    /// </summary>
    /// <param name="chatId">Unique identifier of the chat conversation.</param>
    /// <param name="messageId">Identifier of the message containing the buttons.</param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    Task RemoveButtonsAsync(long chatId, int messageId);
}
