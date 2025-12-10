using AutomationBot.Models;
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
    private readonly ITelegramBotClient _botClient;
    private readonly CancellationTokenSource _cts;
    private readonly TaskManager _taskManager;
    private readonly MessageService _messageService;

    private readonly long[] allowedIDs = new long[2] { -5067979828, 7722464240 };

    public BotService(string token, MessageService service)
    {
        _cts = new CancellationTokenSource();
        _botClient = new TelegramBotClient(token, cancellationToken: _cts.Token);
        _taskManager = new TaskManager();
        _messageService = service;
        service.OnMessageReceived += HandleExternalMessage;
    }

    public async Task StartAsync()
    {
        var me = await _botClient.GetMe();
        Console.WriteLine($"Bot {me.FirstName} started.");
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
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
                            InlineKeyboardButton.WithCallbackData("🚀 Start speedtest", "speed_test_start"),
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("📝 Speed Report", "speed_test_results_report"),
                            InlineKeyboardButton.WithCallbackData("📊 Speed Graph", "speed_test_results_graph"),
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
            case "speed_test_results_report":
            case "speed_test_start":
                await _botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: "Loading data..."
                );
                await _messageService.BroadcastMessageAsync(new TCPMessage
                {
                    type = "text",
                    title = callbackQuery.Data,
                    chatId= callbackQuery.Message.Chat.Id,
                    messageId= callbackQuery.Message.Id,
                });
                break;
            case "speed_test_results_graph":
                await _messageService.BroadcastMessageAsync(new TCPMessage
                {
                    type = "text",
                    title = callbackQuery.Data,
                    chatId = callbackQuery.Message.Chat.Id,
                    messageId = callbackQuery.Message.Id,
                });
                break;
            case "restart_status":
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: _taskManager.RouterRestartStatus(),
                    cancellationToken: cancellationToken
                    );
                break;
            case "restart_router":
                await _taskManager.ExecuteRouterRestartNowAsync();
                _taskManager.CancelRouterRestart();
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.Id,
                    text: "Router restarted successfully."
                );
                break;
            case "cancel_restart_router":
                _taskManager.CancelRouterRestart();
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
                _taskManager.ScheduleRouterRestart(seconds);
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

    public async void HandleExternalMessage(TCPMessage message)
    {
        if (message is null) return;

        switch(message.title.ToLower())            
        {
            case "slow_internet":
                await HandleSlowInternetMessage(message.content);
                break;
            case "speed_test_results_report":
            case "speed_test_start":
                await _botClient.EditMessageText(
                    chatId: message.chatId,
                    messageId: message.messageId,
                    text: message.content
                );
                break;
            case "speed_test_results_graph":
                byte[] bytes = Convert.FromBase64String(message.content);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    await _botClient.DeleteMessage(
                        chatId: message.chatId,
                        messageId: message.messageId
                       );
                    await _botClient.SendPhoto(
                        chatId: message.chatId,
                        photo: InputFile.FromStream(ms)
                       );
                }

                break;
            default:
                break;
        };
    }

    public async Task HandleSlowInternetMessage(string message)
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

        var now = DateTime.Now;
        if (now.DayOfWeek >= DayOfWeek.Monday && now.DayOfWeek <= DayOfWeek.Friday && now.Hour >= 9 && now.Hour <= 18)
        {

            _taskManager.ScheduleRouterRestart(60 * 30);
            await _botClient.SendMessage(
                chatId: allowedIDs[0],
                text: "Slow internet detected. Router will be restarted in 30 minutes unless action is taken.",
                replyMarkup: inlineKeyboard
            );
        }
        else
        {

            _taskManager.ScheduleRouterRestart(60 * 5);
            await _botClient.SendMessage(
                chatId: allowedIDs[0],
                text: "Slow internet detected. Router will be restarted in 5 minutes unless action is taken.",
                replyMarkup: inlineKeyboard
            );
        }



    }

    public async Task SendMessage(string message)
    {
        await _botClient.SendMessage(
            chatId: allowedIDs[0],
            text: message
        );
    }
}

