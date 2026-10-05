using MediatR;
using Microsoft.AspNetCore.Mvc;
using SponsorAid.Application.Features.Registrations.Commands.UpdateRegistrationData;

namespace SponsorAid.Api.Controllers;

[ApiController]
[Route("api/integration/[controller]")]
public class AiAgentController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiAgentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // AI, dilekçeyi ve hobileri ürettiğinde buraya JSON olarak atar
    [HttpPost("automation-result")]
    public async Task<IActionResult> ReceiveAutomationResult(string registrationKey, [FromBody] Dictionary<string, string> generatedData)
    {
        // Gelen veriyi (Örn: { "Petition": "Sayın yetkili...", "Child1_Hobbies": "Futbol" })
        // Zaten var olan esnek "UpdateRegistrationDataCommand" komutumuzun içine koyuyoruz.
        var command = new UpdateRegistrationDataCommand(registrationKey, generatedData);

        await _mediator.Send(command);

        return Ok(new { Message = "Otomasyon verileri başarıyla taslağa işlendi." });
    }
}