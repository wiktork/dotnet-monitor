// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Diagnostics.Tools.Monitor.Auth.ApiKey
{
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(JsonWebKey))]
    internal partial class JwtKeySerializerContext : JsonSerializerContext
    {
    }
}
