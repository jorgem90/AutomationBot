using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationBot.TaskManager
{
    internal class DelayedTask: IDisposable
    {
        private readonly Func<CancellationToken, Task> _task;
        private readonly object _lock = new();

        private CancellationTokenSource? _cts;
        private Task? _runningTask;
        private bool _isExecuting;
        private DateTimeOffset? _scheduledFor;
        public DelayedTask(Func<CancellationToken, Task> task)
        {
            _task = task;
        }

        public string TaskStatus()
        {
            if (_isExecuting)
            {
                return "Running right now";
            }
            if (_scheduledFor == null)
            {
                return "No task is scheduled";
            }
            else {
                TimeSpan remainingTime = _scheduledFor.Value - DateTimeOffset.Now;
                return $"Will run in {remainingTime.ToString(@"hh\:mm")}";
            }
        }

        public void Delay(TimeSpan delay)
        {
            lock (_lock)
            {
                if (_isExecuting)
                {
                    return;
                }

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _scheduledFor = DateTimeOffset.UtcNow.Add(delay);
                _runningTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delay, token);
                        await ExecuteInternalAsync(token);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                });
            };
        }

        public async Task ExecuteNowAsync()
        {
            lock (_lock)
            {
                if(_isExecuting)
                {
                    return;
                }
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                _scheduledFor = null;
            }
            await ExecuteInternalAsync(_cts.Token);
        }

        public void Cancel()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts = null;
                _scheduledFor = null;
            }
        }

        public async Task ExecuteInternalAsync(CancellationToken token)
        {
            lock (_lock)
            {
                if (_isExecuting)
                {
                    return;
                }
                _isExecuting = true;
                _scheduledFor = null;
            }
            try
            {
                await _task(token);
            }
            finally
            {
                lock (_lock)
                {
                    _isExecuting = false;
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }
    }
}
