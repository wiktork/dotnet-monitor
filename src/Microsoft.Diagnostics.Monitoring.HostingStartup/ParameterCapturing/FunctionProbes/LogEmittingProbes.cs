// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Xml.Serialization;

namespace Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing.FunctionProbes
{
    internal sealed class LogEmittingProbes : IFunctionProbes
    {
        private readonly ParameterCapturingLogger _logger;

        public LogEmittingProbes(ParameterCapturingLogger logger)
        {
            _logger = logger;
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

            if (!_logger.ShouldLog())
            {
                return;
            }
                //Mostly works but still some circular relationships:
//                Microsoft.Diagnostics.Monitoring.HostingStartup.dll!Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing.FunctionProbes.LogEmittingProbes.EnterProbe(ulong uniquifier, object[] args) Line 85 C#
// 	Microsoft.Diagnostics.Monitoring.HostingStartup.dll!Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing.FunctionProbes.FunctionProbesStub.EnterProbeStub(ulong uniquifier, object[] args) Line 37    C#
//> System.Private.CoreLib.dll!System.Collections.Generic.Queue<Microsoft.Extensions.Logging.Console.LogMessageEntry>.Count.get()   C#
// 	Microsoft.Extensions.Logging.Console.dll!Microsoft.Extensions.Logging.Console.ConsoleLoggerProcessor.TryDequeue(out Microsoft.Extensions.Logging.Console.LogMessageEntry item) Line 148 C#
// 	Microsoft.Extensions.Logging.Console.dll!Microsoft.Extensions.Logging.Console.ConsoleLoggerProcessor.ProcessLogQueue() Line 106 C#
                // Because Console Logger pushes to a separate thread, that thread processor is instrumented and now adding additional
                // entries. This is circular.


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

            // System.Console.WriteLine("IO Test");
            _logger.Log(instrumentedMethod.CaptureMode, instrumentedMethod.MethodWithParametersTemplateString, argValues);
        }
    }
}
