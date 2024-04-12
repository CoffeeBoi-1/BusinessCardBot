using LandingBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace LandingBot.Services.Processors;

public class Subscriptions
{
    private readonly ITelegramBotClient _botClient;
    private readonly IAppRepository _db;
    private readonly ILogger<Subscriptions> _logger;

    public Subscriptions(ITelegramBotClient botClient, IAppRepository db, ILogger<Subscriptions> logger)
    {
        _botClient = botClient;
        _db = db;
        _logger = logger;
    }

    public async Task CanGrantSubscriptionLevel(PreCheckoutQuery query, CancellationToken cancellationToken)
    {
        long userID = query.From.Id;
        string payload = query.InvoicePayload;
        int newSubLevel = int.Parse(payload);

        bool ableToChangeSubLevel = await _db.UserAbleToChangeSubscriptionLevel(userID, newSubLevel);

        if (ableToChangeSubLevel)
            await _botClient.AnswerPreCheckoutQueryAsync(query.Id, cancellationToken);
        else
            await _botClient.AnswerPreCheckoutQueryAsync(query.Id, "Нельзя купить подписку уровнем ниже", cancellationToken);
    }

    public async Task<Message> GrantTrial(Message message, CancellationToken cancellationToken)
    {
        bool ableToChangeSubLevel = await _db.UserAbleToChangeSubscriptionLevel(message.From!.Id, 1);

        if (ableToChangeSubLevel)
            return await GrantSubscriptionLevel(message, 3, true, cancellationToken);
        else
        {
            return await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Нельзя получить подписку уровнем ниже!",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
        }
    }

    public async Task<Message> GrantSubscriptionLevel(Message message, int subscriptionDuration, bool isTrial, CancellationToken cancellationToken)
    {
        Models.User? user = await GetUserInfo(message.From!.Id);

        int newSubLevel;
        DateTime purchaseTime = DateTime.UtcNow;
        DateTime expiresTime = GetExpiresTime();

        if (isTrial)
        {
            if (user != null && user!.HadFreeSubscription)
                return await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Вы уже использовали свою пробную версию!",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);

            newSubLevel = 1;
            await _db.SetUserSubscriptionLevel(message.From!.Id, newSubLevel, purchaseTime, expiresTime, true);
        }
        else
        {
            newSubLevel = int.Parse(message.SuccessfulPayment!.InvoicePayload);
            await _db.SetUserSubscriptionLevel(message.From!.Id, newSubLevel, purchaseTime, expiresTime, false);
        }

        _logger.LogInformation("User with id: {UserId} bought Subscription Level {SubLevel}", message.From!.Id, newSubLevel);

        string msg = $"Поздравляем вас с приобретением подписки уровня *{newSubLevel}*";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);

        DateTime GetExpiresTime()
        {
            if (user == null || user!.SubscriptionExpires <= purchaseTime) return purchaseTime.AddDays(subscriptionDuration);
            else return user!.SubscriptionExpires.AddDays(subscriptionDuration);
        }
    }

    public async Task<Models.User?> GetUserInfo(long userID) =>
       await _db.GetFullUserInfo(userID);
}