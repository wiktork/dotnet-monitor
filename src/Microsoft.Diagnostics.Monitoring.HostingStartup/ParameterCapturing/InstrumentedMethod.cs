// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing
{
    public enum ParameterCaptureMode
    {
        Disallowed = 0,
        Inline,
        Background,
    }

    public sealed class InstrumentedMethod
    {
        private static readonly string[] SystemTypePrefixes = { "System.", "Microsoft." };
        //private static readonly HashSet<(string, string)> DisallowedMethods = new()
        //{
        //    (typeof(System.Threading.Thread).FullName!, "get_" + nameof(System.Threading.Thread.Name)),
        //    (typeof(Environment).FullName!, "get_" + nameof(Environment.CurrentManagedThreadId)),
        //};

        public InstrumentedMethod(MethodInfo method, uint[] boxingTokens)
        {
            SupportedParameters = BoxingTokens.AreParametersSupported(boxingTokens);
            MethodWithParametersTemplateString = PrettyPrinter.ConstructTemplateStringFromMethod(method, SupportedParameters);

            foreach (bool isParameterSupported in SupportedParameters)
            {
                if (isParameterSupported)
                {
                    NumberOfSupportedParameters++;
                }
            }

            CaptureMode = ComputeCaptureMode(method.DeclaringType?.FullName, method.Name);
        }

        private static ParameterCaptureMode ComputeCaptureMode(string? typeName, string? methodName)
        {
            foreach(string prefix in SystemTypePrefixes)
            {
                if (typeName?.StartsWith(prefix) == true)
                {
                    return ParameterCaptureMode.Background;
                }
            }

            //if (DisallowedMethods.Contains((typeName!, methodName!)))
            //{
            //    return ParameterCaptureMode.Disallowed;
            //}
               

            return ParameterCaptureMode.Inline;
        }

        public ParameterCaptureMode CaptureMode { get; }

        /// <summary>
        /// The total number of parameters (implicit and explicit) that are supported.
        /// </summary>
        public int NumberOfSupportedParameters { get; }

        /// <summary>
        /// An array containing whether each parameter (implicit and explicit) is supported.
        /// </summary>
        public bool[] SupportedParameters { get; }

        /// <summary>
        /// A template string that contains the full method name with parameter names and
        /// format items for each supported parameter.
        /// 
        /// The number of format items equals NumberOfSupportedParameters.
        /// </summary>
        public string MethodWithParametersTemplateString { get; }
    }
}
