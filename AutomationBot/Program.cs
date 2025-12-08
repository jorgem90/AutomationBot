using Telegram.Bot;

using System.Threading.Tasks;

namespace AutomationBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
#if DEBUG

            string botToken = "";
            int port = 54321;
#else
            string botToken = "";
            int port = 12345;
#endif
            var bot = new BotService(botToken);
            await bot.StartAsync();
            var messageService = new MessageService(port, bot);

            using var cts = new CancellationTokenSource();

            await messageService.StartAsync(cts.Token);

            String? message;
            Console.WriteLine("Bot is running. Type 'exit' to exit.");
            do
            {
                message = Console.ReadLine();
                if(!String.IsNullOrWhiteSpace(message)) await bot.SendMessage(message);
            }
            while (message.ToLower() != "exit");
            await bot.StopAsync();
        }
    }
}

