using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace LandingBot.Services.Processors;

public class Commands
{
    private readonly ITelegramBotClient _botClient;
    private readonly Subscriptions _subscriptions;
    private readonly IConfiguration _config;
    private readonly string PaymentProviderToken;

    private readonly InlineKeyboardMarkup inlineKeyboard = new(
     new[]
     {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("ℹ️FAQ", InlineTypes.faq.ToString()),
                        InlineKeyboardButton.WithCallbackData("📢Особая Помощь", InlineTypes.unusual_question.ToString())
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("⛔️Подписка", InlineTypes.subscription.ToString())
                    }
     });

    public Commands(ITelegramBotClient botClient, Subscriptions subscriptions, IConfiguration config)
    {
        _botClient = botClient;
        _subscriptions = subscriptions;
        _config = config;
        PaymentProviderToken = _config.GetValue<string>("PaymentProviderToken");
    }

    public async Task<Message> TestPayment(Message message, CancellationToken cancellationToken)
    {
        List<LabeledPrice> prices = new List<LabeledPrice>();
        prices.Add(new LabeledPrice("Goods", 10050));

        return await _botClient.SendInvoiceAsync(
            chatId: message.Chat.Id,
            title: "TEST",
            prices: prices,
            startParameter: message.Chat.Id.ToString(),
            description: "Nu tipa da",
            providerToken: PaymentProviderToken,
            payload: "1",
            currency: "RUB",
            cancellationToken: cancellationToken);
    }

    public async Task<Message> SendSubscriptionInvoice(Message message, CancellationToken cancellationToken)
    {
        List<LabeledPrice> prices = new List<LabeledPrice>
        {
            new LabeledPrice("Базовая подписка", 55000)
        };

        return await _botClient.SendInvoiceAsync(
            chatId: message.Chat.Id,
            title: "Базовая подписка",
            prices: prices,
            startParameter: message.Chat.Id.ToString(),
            description: "Самая обычная подписка, чтобы прочувствовать силу наших ботов!",
            providerToken: PaymentProviderToken,
            payload: "1",
            currency: "RUB",
            cancellationToken: cancellationToken);
    }

    public async Task<Message> SubscriptionStatus(Message message, CancellationToken cancellationToken)
    {
        Models.User? user = await _subscriptions.GetUserInfo(message.Chat.Id);

        string subName = (user == null || user.SubscriptionLevel.Level == 0) ? "У вас пока нет никакой подписки" : user.SubscriptionLevel.Name;
        string msg = $"Ваша текущая подписка : *{subName}*\n{GetAdditionalInfo()}";

        return await _botClient.EditMessageTextAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        string GetAdditionalInfo()
        {
            bool hasSub = (user == null || user.SubscriptionLevel.Level == 0) ? false : true;

            string result;
            if (hasSub)
                result = $"Подписка приобретена : *{user!.SubscriptionPurchased.ToString("d MMMM yyyy")}*\nПодписка истечёт : *{user!.SubscriptionExpires.ToString("d MMMM yyyy")}*";
            else
            {
                string trialStr = (user == null || user!.HadFreeSubscription == false) ? "Получи пробную версию подписки : */get_trial*\n" : "";
                result = $"{trialStr}Купить подписку : */buy_subscription*";
            }
            return result;
        }
    }

    public async Task<Message> WelcomeMessage(Message message, CancellationToken cancellationToken)
    {
        const string msg = "🚀 *Приветствуем в мире автоматизации общения!* 🚀\n\nУстали от бесконечных сообщений и запросов в мессенджерах, которые не дают сконцентрироваться на действительно важных делах? Наш сервис приходит на помощь с уникальным предложением:\n\nПерсональные бизнес-боты для вашего комфорта!\n\n✅ *Что вы получаете?* \n\n🔸Освобождение вашего времени от рутины общения.\n\n🔸Повышение эффективности вашего бизнеса благодаря безупречному ведению переписки.\n\n🔸Гарантия, что ни одно важное сообщение не останется без ответа.\n\n🔍 *Как это работает?* \n\nНаши клиенты предоставляют боту доступ к своим бизнес-аккаунтам в Telegram. Это обеспечивает непрерывное и профессиональное управление вашими мессенджерами 24/7, гарантируя, что каждое сообщение будет обработано максимально эффективно и в соответствии с вашими бизнес-процессами.\n\n🎯 *Ваша выгода:* \n\nСосредоточьтесь на росте и развитии вашего бизнеса, пока ваш личный бизнес-бот надежно заботится о всех ваших сообщениях.\n\n🌟 Сделайте шаг навстречу новому *уровню взаимодействия* — подпишитесь *сегодня* и дайте вашему бизнесу возможность выделиться!";

        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> FAQMessage(Message message, CancellationToken cancellationToken)
    {
        const string msg = "🔧 *Как настроить бота для ответа на специфические запросы моих клиентов?*\n\nОтвет: Перейдите в раздел \"Настройки\" вашего личного кабинета и используйте функцию \"Создание сценариев\". Здесь вы сможете задать ключевые слова и соответствующие автоответы, а для более сложных запросов использовать шаблоны и переменные.\n\n🔄 *Можно ли интегрировать бота с моей CRM-системой?*\n\nОтвет: Абсолютно! Наш бот поддерживает интеграцию с многими популярными CRM-системами. Вам нужно будет ввести API-ключ вашей CRM в настройках бота, чтобы начать автоматизировать обмен данными.\n\n🔒 *Как обеспечить безопасность персональных данных клиентов, используя бота?*\n\nОтвет: Безопасность данных — наш приоритет. Все данные, обрабатываемые ботом, зашифрованы и хранятся на защищенных серверах. Бот разработан в соответствии с GDPR.\n\n📦 *Может ли бот автоматически принимать заказы или бронирования?*\n\nОтвет: Да, наш бот может быть настроен на прием заказов и бронирований через специальные формы. Просто настройте нужные сценарии через панель управления.\n\n📊 *Как отслеживать эффективность бота?*\n\nОтвет: В личном кабинете доступна детальная аналитика по работе бота, включая количество обработанных запросов и уровень вовлеченности клиентов. Это поможет вам оптимизировать настройки бота.\r\n";

        return await _botClient.EditMessageTextAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            text: msg,
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }
}