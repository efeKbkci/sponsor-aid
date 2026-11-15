using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;

namespace SponsorAid.Api.Controllers;

/// <summary>
/// Handles incoming webhook notifications dispatched from the Telegram Bot API.
/// </summary>
[ApiController]
[Route("api/telegram/webhook")]
public class TelegramController(IDialogRouter dialogRouter) : ControllerBase
{
    private readonly IDialogRouter _dialogRouter = dialogRouter;

    /// <summary>
    /// Processes incoming Telegram webhook updates asynchronously.
    /// </summary>
    /// <param name="updatePayload">Raw JSON update payload received from Telegram.</param>
    /// <returns>HTTP 200 OK result to acknowledge receipt.</returns>
    [HttpPost]
    public async Task<IActionResult> HandleTelegramUpdate([FromBody] JsonElement updatePayload)
    {
        try
        {
            await _dialogRouter.RouteIncomingMessageAsync(updatePayload.ToString());
            return Ok();
        }
        catch (Exception ex)
        {
            // Returns HTTP 200 OK even upon error to prevent Telegram from retrying the update repeatedly.
            Console.WriteLine($"[Webhook Hatası]: {ex.Message}");
            return Ok();
        }
    }
}
