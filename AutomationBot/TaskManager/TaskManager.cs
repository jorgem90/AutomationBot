using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationBot.TaskManager
{
    internal class TaskManager
    {
        private static DelayedTask _restart_router_task = new DelayedTask(async (ct) =>
        {
            await System.Diagnostics.Process.Start("tapoautomation", "restart -t 90 -h 192.168.86.178").WaitForExitAsync(ct);
            await System.Diagnostics.Process.Start("tapoautomation", "restart -t 90 -h 192.168.86.180").WaitForExitAsync(ct);
        });
        public TaskManager() { }

        public void ScheduleRouterRestart(int seconds)
        {
            _restart_router_task.Delay(TimeSpan.FromSeconds(seconds));
        }
        public async Task ExecuteRouterRestartNowAsync()
        {
            await _restart_router_task.ExecuteNowAsync();
        }
        public void CancelRouterRestart()
        {
            _restart_router_task.Cancel();
        }

        public string RouterRestartStatus() {
            return _restart_router_task.TaskStatus();
        }

    }
}
