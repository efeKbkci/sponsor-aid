using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Enums;

public enum RegistrationStep
{
    WaitingForGuardianId = 0,
    WaitingForPopulationRegistry = 1,
    ProcessingPopulationRegistry = 2, // Asenkron kilit (Worker'da işleniyor)
    WaitingForResidence = 3,
    ProcessingResidence = 4,         
    WaitingForIdentityFront = 5,
    ProcessingIdentityFront = 6,      
    WaitingForIdentityBack = 7,
    ProcessingIdentityBack = 8,      
    MergingDocuments = 9,             // PDF birleştiriliyor
    WaitingForQuestions = 10,          // Metin soruları aşaması
    WaitingForConfirmation = 11, // Özet ve Onay Bekleme Aşaması
    Completed = 12
}