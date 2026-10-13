using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;
using SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;
using SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;
using SponsorAid.Application.Features.Registrations.Commands.GreetUser;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessTextInput;

public class ProcessTextInputHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IMediator mediator,
    IStringLocalizer<BotMessages> localizer
) : IRequestHandler<ProcessTextInputCommand>
{
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;
    private static readonly HashSet<string> MenuTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "komut", "komutlar", "menu", "menü", "/komut", "/menu", "/yardim", "/help"
    };

    // Slash komutlarını Description ile eşleyen önbellek
    private static readonly Dictionary<string, BotCommand> SlashCommandMap =
        Enum.GetValues<BotCommand>().ToDictionary(
            cmd => typeof(BotCommand).GetField(cmd.ToString())?
                .GetCustomAttribute<DescriptionAttribute>()?.Description.ToLowerInvariant() ?? string.Empty,
            cmd => cmd,
            StringComparer.OrdinalIgnoreCase);

    public async Task Handle(ProcessTextInputCommand request, CancellationToken cancellationToken)
    {
        var text = request.Text.Trim();
        var draft = await draftRepository.GetActiveByChatIdAsync(request.ChatId, cancellationToken);

        // 1. BUTON TIKLAMALARI (Callback)
        if (request.CallbackMessageId.HasValue)
        {
            await HandleCallbackAsync(request.ChatId, request.CallbackMessageId.Value, text, draft?.RegistrationKey, cancellationToken);
            return;
        }

        // 2. MENÜ TETİKLEYİCİSİ ("menü", "komut" vb.)
        if (MenuTriggers.Contains(text))
        {
            await notifier.SendQuestionWithButtonsAsync(request.ChatId, _localizer["Prompt_AvailableCommands"], GetCommandMenuOptions());
            return;
        }

        // 3. AÇIK SLASH KOMUTLARI (/start, /new, /continue, /cancel)
        if (text.StartsWith('/'))
        {
            await ExecuteSlashCommandAsync(request.ChatId, draft?.RegistrationKey, text, cancellationToken);
            return;
        }

        // 4. GÜNCELLEME ALANI SEÇİM MODU (NLI İLE HİBRİT YAKALAMA)
        if (draft != null && draft.IsWaitingForUpdateFieldSelection)
        {
            await mediator.Send(new UpdateQuestionAnswerCommand(draft.RegistrationKey, text, request.ChatId), cancellationToken);
            return;
        }

        // 5. YALNIZCA TAM METİN 6 KARAKTERLİ VE EN AZ BİR HARF İÇERİYORSA TAKİP KODUDUR
        // (Cümle içi "Doktor", "Garson" gibi kelimelerin yanlışlıkla kod sanılmasını engeller)
        if (Regex.IsMatch(text, @"^(?=[A-Za-z0-9]*[A-Za-z])[A-Za-z0-9]{6}$"))
        {
            await mediator.Send(new ContinueRegistrationCommand(text.ToUpperInvariant(), request.ChatId), cancellationToken);
            return;
        }

        // 6. AKTİF TASLAK YOKSA
        if (draft == null)
        {
            await notifier.SendMessageAsync(request.ChatId, _localizer["Error_NoActiveRegistration_StartFromMenu"]);
            return;
        }

        // 7. MEVCUT ADIMDAKİ ANKET / GİRDİ İŞLEMLERİ
        switch (draft.CurrentStep)
        {
            case RegistrationStep.WaitingForGuardianId:
                draft.ApplyUpdates(new() { { "GuardianIdNumber", text } });
                draft.SetStep(RegistrationStep.WaitingForPopulationRegistry);
                await draftRepository.SaveAsync(draft, cancellationToken);

                await notifier.SendMessageAsync(draft.ChatId,
                    _localizer["Success_RegistrationCreated", text, draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForQuestions:
                await mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey, text), cancellationToken);
                break;

            case RegistrationStep.WaitingForConfirmation:
                await notifier.SendMessageAsync(draft.ChatId, _localizer["Error_RegistrationConfirmationRequired"]);
                break;

            default:
                await notifier.SendMessageAsync(draft.ChatId, _localizer["Error_DocumentUploadRequired"]);
                break;
        }
    }

    private async Task ExecuteSlashCommandAsync(long chatId, string? regKey, string cmd, CancellationToken ct)
    {
        var commandName = cmd.Split(' ')[0];
        if (SlashCommandMap.TryGetValue(commandName, out var command))
        {
            await ExecuteCommandAsync(chatId, regKey, command, ct);
        }
        else
        {
            await notifier.SendMessageAsync(chatId, _localizer["Error_InvalidCommand"]);
        }
    }

    private async Task ExecuteCommandAsync(long chatId, string? regKey, BotCommand command, CancellationToken ct)
    {
        switch (command)
        {
            case BotCommand.StartBot:
                await mediator.Send(new GreetUserCommand(chatId), ct);
                break;

            case BotCommand.NewRegistration:
                await mediator.Send(new StartRegistrationCommand(chatId), ct);
                break;

            case BotCommand.ContinueRegistration:
                await mediator.Send(new ContinueRegistrationCommand(null, chatId), ct);
                break;

            case BotCommand.CancelRegistration:
                if (regKey != null) await mediator.Send(new CancelRegistrationCommand(regKey, chatId), ct);
                else throw new BusinessRuleException("Error_NoActiveRegistrationToCancel");
                break;

            case BotCommand.UpdateRegistration:
                if (regKey != null) await mediator.Send(new UpdateQuestionAnswerCommand(regKey, null, chatId), ct);
                else throw new BusinessRuleException("Error_ActiveRegistrationRequiredForUpdate");
                break;
        }
    }

    private async Task HandleCallbackAsync(long chatId, int callBackMessageId, string callBackMessage, string? regKey, CancellationToken cancellation)
    {
        // 1. Kayıt Listesinden Seçim Yapıldıysa
        if (callBackMessage.StartsWith(ContinueRegistrationCommand.SelectionCallbackPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var selectedKey = callBackMessage[ContinueRegistrationCommand.SelectionCallbackPrefix.Length..];
            await notifier.UpdateMessageAsAnsweredAsync(chatId, callBackMessageId, _localizer["Label_RegistrationSelection"], selectedKey);
            await mediator.Send(new ContinueRegistrationCommand(selectedKey, chatId), cancellation);
            return;
        }

        // Başvuru Onay Butonuna Basıldıysa (Finalize)
        if (callBackMessage.Equals("confirm_registration", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(regKey))
            {
                await notifier.SendMessageAsync(chatId, _localizer["Error_NoActiveRegistration"]);
                return;
            }

            // Özet metnini KORU, sadece altındaki butonu kaldır:
            await notifier.RemoveButtonsAsync(chatId, callBackMessageId);

            // Finalize akışını başlat (ZIP + E-posta + Telegram Şifre + Hard Delete)
            await mediator.Send(new FinalizeRegistrationCommand(regKey), cancellation);
            return;
        }

        // 2. Ana Menü Komut Butonuna Basıldıysa
        if (SlashCommandMap.TryGetValue(callBackMessage, out var command) &&
            GetCommandMenuOptions().TryGetValue(callBackMessage, out var buttonLabel))
        {
            await notifier.UpdateMessageAsAnsweredAsync(chatId, callBackMessageId, _localizer["Label_Menu"], buttonLabel);
            await ExecuteCommandAsync(chatId, regKey, command, cancellation);
            return;
        }

        // 3. Normal Anket Butonları
        await mediator.Send(new ProcessQuestionnaireAnswerCommand(regKey!, callBackMessage, callBackMessageId), cancellation);
    }

    private Dictionary<string, string> GetCommandMenuOptions()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/new"] = _localizer["Command_NewRegistration"],
            ["/continue"] = _localizer["Command_ContinueRegistration"],
            ["/cancel"] = _localizer["Command_CancelRegistration"],
            ["/update"] = _localizer["Command_UpdateRegistration"]
        };
    }
}