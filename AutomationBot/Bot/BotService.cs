using AutomationBot.TaskManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class BotService
{
    private readonly ITelegramBotClient botClient;
    private readonly CancellationTokenSource cts;
    private readonly TaskManager taskManager;

    private readonly long[] allowedIDs = new long[2] { -5067979828, 7722464240 };

    public BotService(string token)
    {
        cts = new CancellationTokenSource();
        botClient = new TelegramBotClient(token, cancellationToken: cts.Token);
        taskManager = new TaskManager();
    }

    public async Task StartAsync()
    {
        var me = await botClient.GetMe();
        Console.WriteLine($"Bot {me.FirstName} started.");
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>()
        };

        botClient.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );
    }

    public async Task StopAsync()
    {
        cts.Cancel();
        await Task.Delay(500);
        Console.WriteLine("Bot stopped.");
    }

    public async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        long chatId = update.Type switch
        {
            UpdateType.Message => update.Message.Chat.Id,
            UpdateType.CallbackQuery => update.CallbackQuery.Message.Chat.Id,
            _ => 0
        };
        if (allowedIDs.Contains(chatId))
        {
            if (update.Type == UpdateType.Message)
            {
                await HandleMessageAsync(botClient, update.Message, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
                chatId: update.Message.Chat.Id,
                text: "Not in allowed receivers list"
            );
        }

    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        switch (message.Text?.ToLower())
        {
            case "/start":
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Welcome! \nThis bot was designed to monitor local internet speeds and control home automation. \nUse /menu too see more options."
                    );
                break;
            case "/menu":
                var menuKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("🔄 Restart router", "restart_router"),
                            InlineKeyboardButton.WithCallbackData("⌛ Restart status", "restart_status"),
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("📖 Last restart", "restart_last_time"),
                            InlineKeyboardButton.WithCallbackData("🔎 Recent Results", "speed_test_results_recent"),
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("📝 Speed Report", "speed_test_results_recent"),
                            InlineKeyboardButton.WithCallbackData("📊 Speed Graph", "speed_test_results_recent"),
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("⌯⌲ Send message", "send_message"),
                        }
                    });

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Delay by (x) mins:",
                    replyMarkup: menuKeyboard
                );
                break;
        }
    }

    public async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        switch (callbackQuery.Data)
        {
            case "send_message":
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: taskManager.RouterRestartStatus(),
                    cancellationToken: cancellationToken
                    );
                break;
            case "restart_status":
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: taskManager.RouterRestartStatus(),
                    cancellationToken: cancellationToken
                    );
                break;
            case "restart_router":
                await taskManager.ExecuteRouterRestartNowAsync();
                taskManager.CancelRouterRestart();
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: "Router restarted successfully."
                );
                break;
            case "cancel_restart_router":
                taskManager.CancelRouterRestart();
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: "Router restart cancelled successfully."
                );
                break;
            case "delay_restart_router":
                InlineKeyboardMarkup inlineKeyboard = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("15", "delay_restart_router_15"),
                        InlineKeyboardButton.WithCallbackData("30", "delay_restart_router_30"),
                        InlineKeyboardButton.WithCallbackData("45", "delay_restart_router_45"),
                        InlineKeyboardButton.WithCallbackData("60", "delay_restart_router_60")
                    }
                });
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id, 
                    text: "Delay by (x) mins:",
                    replyMarkup: inlineKeyboard
                );
                break;
            case var s when s.StartsWith("delay_restart_router_"):
                var seconds = int.Parse(s.Split('_').Last()) * 60;
                taskManager.ScheduleRouterRestart(seconds);
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: $"Router will be restarted at {DateTime.Now.AddSeconds(seconds).ToString("hh:mm tt")}"
                );
                break;
        }
    }

    public async Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {

    }

    public async Task HandleExternalMessage(string message)
    {
        switch(message.ToLower())            
        {
            case "slow_internet":
                taskManager.ScheduleRouterRestart(60 * 5);
                await HandleSlowInternetMessage();
                break;
            default:
                break;
        };
    }

    public async Task HandleSlowInternetMessage()
    {
        InlineKeyboardMarkup inlineKeyboard = new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Restart now", "restart_router"),
                InlineKeyboardButton.WithCallbackData("Delay", "delay_restart_router"),
                InlineKeyboardButton.WithCallbackData("Cancel", "cancel_restart_router")
            }
        });

        await botClient.SendMessage(
            chatId: allowedIDs[0],
            text: "Slow internet detected. Router will be restarted in 5 minutes unless action is taken.",
            replyMarkup: inlineKeyboard
        );
    }

    public async Task SendMessage(string message)
    {
        await botClient.SendMessage(
            chatId: -5067979828,
            text: message
        );
    }
}

