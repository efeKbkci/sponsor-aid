using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

public interface IDialogRouter
{
    // Payload, JSON veya benzeri bir formattaki sunucudan gelen mesajın ham verisini temsil eder. 
    // JsonPayload kullanmamamın sebebi, Clean Architecture prensiplerine uygun olarak, Application katmanına teknoloji sokmamaktır.
    Task RouteIncomingMessageAsync(string payload, CancellationToken cancellationToken = default);
}
