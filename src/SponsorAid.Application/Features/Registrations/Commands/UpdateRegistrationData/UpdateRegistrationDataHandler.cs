using MediatR;
using SponsorAid.Application.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateRegistrationData;

public class UpdateRegistrationDataHandler(IDraftStateRepository draftRepository) : IRequestHandler<UpdateRegistrationDataCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;

    public async Task Handle(UpdateRegistrationDataCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        if (draft == null)
        {
            throw new KeyNotFoundException($"Draft with key '{request.RegistrationKey}' not found.");
        }

        draft.ApplyUpdates(request.Updates);

        await _draftRepository.SaveAsync(draft, cancellationToken);
    }
}
