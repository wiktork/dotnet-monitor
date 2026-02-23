// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Models
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "MinLengthAttribute is used for validation only.")]
    public class EventPipeConfiguration
    {
        [JsonPropertyName("providers")]
        [Required, MinLength(1)]
        public EventPipeProvider[] Providers { get; set; } = [];

        [JsonPropertyName("requestRundown")]
        public bool RequestRundown { get; set; } = true;

        [JsonPropertyName("bufferSizeInMB")]
        [Range(1, 1024)]
        public int BufferSizeInMB { get; set; } = 256;
    }
}
