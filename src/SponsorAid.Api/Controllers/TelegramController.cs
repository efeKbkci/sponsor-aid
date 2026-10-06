using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;

namespace SponsorAid.Api.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
public class TelegramController(IDialogRouter dialogRouter) : ControllerBase
{
    private readonly IDialogRouter _dialogRouter = dialogRouter;

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
            Console.WriteLine($"[Webhook Hatası]: {ex.Message}");
            return Ok();
        }
    }
}