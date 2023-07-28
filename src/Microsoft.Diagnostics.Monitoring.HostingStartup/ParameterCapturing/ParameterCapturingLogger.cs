// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Monitoring.StartupHook;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing
{
    internal sealed class ParameterCapturingLogger : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Thread _thread;
        private BlockingCollection<(string format, string[] args)> _messages;
        private uint _droppedMessageCounter;
        private const int BackgroundLoggingCapacity = 1024;
        private const string BackgroundLoggingThreadName = "Probe Logging Thread";
        private long _disposedState;

        private static readonly string[] ExcludedThreads = new[]
        {
            "Console logger queue processing thread",
        };

        public ParameterCapturingLogger(ILogger logger)
        {
            _logger = logger;
            _thread = new Thread(ThreadProc);

            //Do not schedule ahead of app work
            _thread.Priority = ThreadPriority.BelowNormal;
            _thread.Name = BackgroundLoggingThreadName;
            _messages = new BlockingCollection<(string, string[])>(BackgroundLoggingCapacity);
            _thread.Start();
        }

        public bool ShouldLog()
        {
            if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
            {
                return false;
            }
            if (ExcludedThreads.Contains(Thread.CurrentThread.Name))
            {
                return false;
            }

            return true;
        }

        public void Log(ParameterCaptureMode mode, string format, string[] args)
        {
            if (mode == ParameterCaptureMode.Inline)
            {
                Log(format, args);
            }
            else if (mode == ParameterCaptureMode.Background)
            {
                if (!_messages.TryAdd((format, args)))
                {
                    _droppedMessageCounter++;
                }
            }
        }

        private void ThreadProc()
        {
            while (!DisposableHelper.IsDisposed(ref _disposedState))
            {
                (string format, string[] args) = _messages.Take();
                Log(format, args);
            }
        }

        private void Log(string format, string[] args)
        {
            _logger.Log(LogLevel.Information, format, args);
        }

        public void Dispose()
        {
            if (!DisposableHelper.CanDispose(ref _disposedState))
            {
                return;
            }
        }
    }
}
