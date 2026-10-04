using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

public interface IUserNotifier
{
    Task SendMessageAsync(long chatId, string message);
}
