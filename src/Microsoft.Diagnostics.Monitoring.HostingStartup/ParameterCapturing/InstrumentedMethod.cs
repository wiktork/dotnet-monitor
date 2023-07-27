﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Microsoft.Diagnostics.Monitoring.HostingStartup.ParameterCapturing
{
    public enum ParameterCaptureMode
    {
        Inline,
        Background,
        Disallowed
    }

    public sealed class InstrumentedMethod
    {
        private static readonly string[] SystemTypePrefixes = { "System.", "Microsoft." };

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



            if (disallowedNamespaces.Contains(method.DeclaringType?.Namespace))
            {
                CaptureMode = ParameterCaptureMode.Disallowed;
            }
            else if (disallowedTypes.Select(type => method.DeclaringType?.FullName?.StartsWith(type) ?? false).Any(t => t))
            {
                CaptureMode = ParameterCaptureMode.Disallowed;
            }
            else if (systemTypes.Select(type => method.DeclaringType?.FullName?.StartsWith(type) ?? false).Any(t => t))
            {
                CaptureMode = ParameterCaptureMode.Background;
            }
            else
            {
                CaptureMode = ParameterCaptureMode.Inline;
            }
        }

        private static ParameterCaptureMode ComputeCaptureMode(string? typeName)
        {
            foreach(string prefix in SystemTypePrefixes)
            {
                if (typeName?.StartsWith(prefix) == true)
                {
                    return ParameterCaptureMode.Background;
                }
            }
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
