// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.Diagnostics.Monitoring.WebApi.Models;

namespace Microsoft.Diagnostics.Monitoring.WebApi
{
    [JsonSerializable(typeof(CallStackResult))]
    [JsonSerializable(typeof(CapturedMethod))]
    [JsonSerializable(typeof(ExceptionInstance))]
    internal partial class MonitorSerializerContext : JsonSerializerContext
    {
    }
}
