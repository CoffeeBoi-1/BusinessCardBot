using LandingBot.Models;
using LandingBot.Services;
using LandingBot.Services.Processors;
using Microsoft.VisualBasic;
using System;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace Telegram.Bot.Services;

public class UpdateHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandlers> _logger;
    private readonly Commands _commands;
    private readonly Subscriptions _subscriptions;
    private readonly InfoInput _infoInput;


    public UpdateHandlers(ITelegramBotClient botClient, Commands commands, Subscriptions subscriptions, InfoInput infoInput, ILogger<UpdateHandlers> logger)
    {
        _botClient = botClient;
        _commands = commands;
        _subscriptions = subscriptions;
        _infoInput = infoInput;
        _logger = logger;
    }

    public Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            { PreCheckoutQuery: { } preCheckOutQuery } => BotOnPreCheckoutQuery(preCheckOutQuery, cancellationToken),
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken)
        };

        await handler;
    }

    private async Task BotOnPreCheckoutQuery(PreCheckoutQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await _subscriptions.CanGrantSubscriptionLevel(query, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "PreCheckout Error");
            await _botClient.AnswerPreCheckoutQueryAsync(query.Id, "Что-то пошло не так", cancellationToken);
        }
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive message type: {MessageType}", message.Type);

        Task<Message>? action;

        if (message.Type == MessageType.Text)
        {
            if (message.Text is not { } messageText)
                return;

            if (_infoInput.UserIsOnStep(message.From!.Id))
            {
                action = _infoInput.ProcessInput(message, cancellationToken);
            }
            else
            {
                action = messageText.Split(' ')[0] switch
                {
                    "/start" => _commands.WelcomeMessage(message, cancellationToken),
                    "/get_trial" => _subscriptions.GrantTrial(message, cancellationToken),
                    "/buy_subscription" => _commands.SendSubscriptionInvoice(message, cancellationToken),
                    "/edit_faq" => _infoInput.StartFAQInput(message, cancellationToken),
                    "/edit_order" => _infoInput.StartOrderInput(message, cancellationToken),
#if DEBUG
                    "/test_payment" => _commands.TestPayment(message, cancellationToken),
#endif
                    _ => null
                };
            }

            if (action == null) return;
        }
        else if (message.Type == MessageType.SuccessfulPayment)
        {
            // Купил подписку
            action = _subscriptions.GrantSubscriptionLevel(message, 30, false, cancellationToken);
        }
        else
        {
            action = _commands.WelcomeMessage(message, cancellationToken);
        }

        try
        {
            Message sentMessage = await action;
            _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Some error while Action");

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Произошла ошибка, обратитесь в поддержку!",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }
    }

    // Process Inline Keyboard callback data
    private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);

        Task<Message> action = callbackQuery.Data!.Split(' ')[0] switch
        {
            nameof(InlineTypes.subscription) => _commands.SubscriptionStatus(callbackQuery.Message!, cancellationToken),
            nameof(InlineTypes.faq) => _commands.FAQMessage(callbackQuery.Message!, cancellationToken),
            nameof(InlineTypes.cancel_edit) => _infoInput.CancelEdit(callbackQuery.Message!, cancellationToken),
            _ => _commands.FAQMessage(callbackQuery.Message!, cancellationToken)
        };

        try
        {
            Message sentMessage = await action;
            _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
        }
        catch
        {
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }
    }

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }
}
