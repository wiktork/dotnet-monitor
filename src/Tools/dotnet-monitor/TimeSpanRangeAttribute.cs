// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Microsoft.Diagnostics.Tools.Monitor
{
    /// <summary>
    /// A trim-safe replacement for <c>[Range(typeof(TimeSpan), ...)]</c> that
    /// validates a <see cref="TimeSpan"/> value without using reflection.
    /// The built-in <see cref="RangeAttribute"/> with a <see cref="Type"/> parameter
    /// calls <c>Type.GetMethod("Parse")</c> internally, which is not trim-safe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    internal sealed class TimeSpanRangeAttribute : ValidationAttribute
    {
        public TimeSpan Minimum { get; }
        public TimeSpan Maximum { get; }

        public TimeSpanRangeAttribute(string minimum, string maximum)
        {
            Minimum = TimeSpan.Parse(minimum, CultureInfo.InvariantCulture);
            Maximum = TimeSpan.Parse(maximum, CultureInfo.InvariantCulture);
        }

        public override bool IsValid(object? value)
        {
            if (value is null)
            {
                return true;
            }

            if (value is not TimeSpan timeSpan)
            {
                return false;
            }

            return timeSpan >= Minimum && timeSpan <= Maximum;
        }

        public override string FormatErrorMessage(string name)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "The field {0} must be between {1} and {2}.",
                name,
                Minimum,
                Maximum);
        }
    }
}
