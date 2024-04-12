using LandingBot.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LandingBot.Services.Processors;

public class InfoInput
{
    private static ConcurrentDictionary<long, StepTypes> UsersStep = new ConcurrentDictionary<long, StepTypes>();
    private static readonly InlineKeyboardMarkup inlineKeyboard = new(
        new[]
        {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("❌Закрыть изменения", InlineTypes.cancel_edit.ToString())
                    }
        });
    private const string SubscriptionRestrictionMessage = "🛑 Купите подписку, чтобы пользоваться этой функцией 🛑";

    private readonly ITelegramBotClient _botClient;
    private readonly IAppRepository _db;

    public InfoInput(ITelegramBotClient botClient, IAppRepository db)
    {
        _botClient = botClient;
        _db = db;
    }

    public Task<Message>? ProcessInput(Message message, CancellationToken cancellationToken)
    {
        Task<Message>? action;
        long userID = message.From!.Id;
        StepTypes stepType;
        UsersStep.TryGetValue(userID, out stepType);

        string noImages = Regex.Replace(message.Text!, @"!\[[^\]]*\]\([^\)]*\)", string.Empty);
        string noLinks = Regex.Replace(noImages, @"\[[^\]]*\]\([^\)]*\)", string.Empty);
        string noMarkdown = Regex.Replace(noLinks, @"[*_`~/]", string.Empty);
        message.Text = noMarkdown;

        action = stepType switch
        {
            StepTypes.None => null,
            StepTypes.FAQInput => GetFAQInput(message, cancellationToken),
            StepTypes.OrderInput => GetOrderInput(message, cancellationToken),
            _ => null
        };

        return action;
    }

    public async Task<Message> CancelEdit(Message message, CancellationToken cancellationToken)
    {
        long userID = message.Chat.Id;
        UsersStep.AddOrUpdate(userID, StepTypes.None, (id, oldStepType) => StepTypes.None);

        return await _botClient.EditMessageTextAsync(
            chatId: message.Chat.Id,
            message.MessageId,
            text: "Изменение отменено!",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    public bool UserIsOnStep(long userID)
    {
        StepTypes stepType;
        UsersStep.TryGetValue(userID, out stepType);

        if (stepType == StepTypes.None) return false;
        else return true;
    }

    #region METHODS
    public async Task<Message> StartFAQInput(Message message, CancellationToken cancellationToken)
    {
        long userID = message.From!.Id;
        int subLevel = await _db.GetUserSubscriptionLevel(userID);

        if (subLevel < 1)
        {
            return await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: SubscriptionRestrictionMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        string faqMsg = await _db.GetUserInfoMessage(userID, "faqmessages");

        UsersStep.AddOrUpdate(userID, StepTypes.FAQInput, (id, oldStepType) => StepTypes.FAQInput);

        string msg = $"Теперь отправьте текст для секции _FAQ_\n\n*Вот ваше текущее сообщение:*\n{faqMsg}";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task<Message> GetFAQInput(Message message, CancellationToken cancellationToken)
    {
        long userID = message.From!.Id;
        UsersStep.AddOrUpdate(userID, StepTypes.None, (id, oldStepType) => StepTypes.None);

        await _db.SetUserInfoMessage(userID, message.Text!, "faqmessages");

        return await _botClient.EditMessageTextAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId - 1,
            text: "Ваше FAQ Успешно обновлено!",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> StartOrderInput(Message message, CancellationToken cancellationToken)
    {
        long userID = message.From!.Id;
        int subLevel = await _db.GetUserSubscriptionLevel(userID);

        if (subLevel < 1)
        {
            return await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: SubscriptionRestrictionMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        string orderMsg = await _db.GetUserInfoMessage(userID, "ordermessages");

        UsersStep.AddOrUpdate(userID, StepTypes.OrderInput, (id, oldStepType) => StepTypes.OrderInput);

        string msg = $"Теперь отправьте текст для секции _Заказать_\n\n*Вот ваше текущее сообщение:*\n{orderMsg}";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task<Message> GetOrderInput(Message message, CancellationToken cancellationToken)
    {
        long userID = message.From!.Id;
        UsersStep.AddOrUpdate(userID, StepTypes.None, (id, oldStepType) => StepTypes.None);

        await _db.SetUserInfoMessage(userID, message.Text!, "ordermessages");

        return await _botClient.EditMessageTextAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId - 1,
            text: "Ваше сообщение для _Заказать_ Успешно обновлено!",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }
    #endregion
}