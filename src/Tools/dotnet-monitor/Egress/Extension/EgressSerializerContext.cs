// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.Diagnostics.Tools.Monitor.Egress
{
    [JsonSerializable(typeof(ExtensionEgressPayload))]
    [JsonSerializable(typeof(EgressArtifactResult))]
    internal partial class EgressSerializerContext : JsonSerializerContext
    {
    }
}
