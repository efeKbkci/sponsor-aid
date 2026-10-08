using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

public interface IUserNotifier
{
    Task SendMessageAsync(long chatId, string message);
    Task SendDocumentAsync(long chatId, string filePath, string? caption = null, CancellationToken cancellationToken = default);
    Task SendQuestionWithButtonsAsync(long chatId, string question, Dictionary<string, string> options);
    Task UpdateMessageAsAnsweredAsync(long chatId, int messageId, string originalQuestion, string selectedOptionLabel);
}
