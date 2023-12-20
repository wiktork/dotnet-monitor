// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Diagnostics.Monitoring.Otel
{
    /// <summary>
    /// Egress provider options for Azure blob storage.
    /// </summary>
    internal sealed partial class OtelEgressProviderOptions
    {
        [Display(
            ResourceType = typeof(OptionsDisplayStrings),
            Description = nameof(OptionsDisplayStrings.DisplayAttributeDescription_AzureBlobEgressProviderOptions_AccountUri))]
        [Required]
        public Uri Endpoint { get; set; }

        [Display(
            ResourceType = typeof(OptionsDisplayStrings),
            Description = nameof(OptionsDisplayStrings.DisplayAttributeDescription_AzureBlobEgressProviderOptions_Metadata))]
        public IDictionary<string, string> Metadata { get; set; }
            = new Dictionary<string, string>(0);
    }
}
