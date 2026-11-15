using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Step-by-step state machine tracking document verification, background jobs, and applicant input.
/// </summary>
public enum RegistrationStep
{
    WaitingForGuardianId = 0,
    WaitingForPopulationRegistry = 1,
    ProcessingPopulationRegistry = 2,
    WaitingForResidence = 3,
    ProcessingResidence = 4,
    WaitingForIdentityFront = 5,
    ProcessingIdentityFront = 6,
    WaitingForIdentityBack = 7,
    ProcessingIdentityBack = 8,
    MergingDocuments = 9,
    WaitingForQuestions = 10,
    WaitingForConfirmation = 11,
    Completed = 12
}
