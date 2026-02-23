// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.Diagnostics.Tools.Monitor.Extensibility
{
    [JsonSerializable(typeof(ExtensionManifest))]
    internal partial class ExtensionManifestSerializerContext : JsonSerializerContext
    {
    }
}
