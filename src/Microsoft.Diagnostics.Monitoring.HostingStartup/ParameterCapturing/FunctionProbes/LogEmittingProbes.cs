// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing.FunctionProbes
{
    internal sealed class LogEmittingProbes : IFunctionProbes
    {
        private readonly ILogger _logger;
        private readonly Thread _thread;
        private BlockingCollection<(string, string[])> _messages;

        public LogEmittingProbes(ILogger logger)
        {
            _logger = logger;
            _thread = new Thread(ThreadProc);

            //Do not schedule ahead of app work
            _thread.Priority = ThreadPriority.BelowNormal;
            _messages = new BlockingCollection<(string, string[])>(1024);
            _thread.Start();
        }

        private void ThreadProc()
        {
            while (true)
            {
                var message = _messages.Take();
                _logger.Log(LogLevel.Information, message.Item1, message.Item2);
            }
        }

        public void EnterProbe(ulong uniquifier, object[] args)
        {
            // JSFIX: Expensive test code to avoid recursing on any methods called by loggers
            /*
            var trace = new StackTrace(fNeedFileInfo: false);
            foreach (var frame in trace.GetFrames())
            {
                if (frame.HasMethod() && frame.GetMethod()?.DeclaringType?.FullName?.Contains("log", System.StringComparison.OrdinalIgnoreCase) == true)
                {
                    return;
                }
            }
            */

            var methodCache = FunctionProbesStub.InstrumentedMethodCache;
            if (methodCache == null ||
                args == null ||
                !methodCache.TryGetValue(uniquifier, out InstrumentedMethod? instrumentedMethod) ||
                args.Length != instrumentedMethod?.SupportedParameters.Length)
            {
                return;
            }

            if (instrumentedMethod.CaptureMode == ParameterCaptureMode.Disallowed)
            {
                return;
            }

            string[] argValues = new string[instrumentedMethod.NumberOfSupportedParameters];
            int fmtIndex = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (!instrumentedMethod.SupportedParameters[i])
                {
                    continue;
                }

                argValues[fmtIndex++] = PrettyPrinter.FormatObject(args[i]);
            }

            if (instrumentedMethod.CaptureMode == ParameterCaptureMode.Background)
            {
                _messages.TryAdd((instrumentedMethod.MethodWithParametersTemplateString, argValues));
                return;
            }


            //
            // System.Console.WriteLine("IO Test");
            _logger.Log(LogLevel.Information, instrumentedMethod.MethodWithParametersTemplateString, argValues);

            return;
        }
    }
}
