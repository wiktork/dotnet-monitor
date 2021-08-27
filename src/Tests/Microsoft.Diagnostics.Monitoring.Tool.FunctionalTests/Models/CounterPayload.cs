using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.Diagnostics.Monitoring.Tool.FunctionalTests.Models
{
    internal class CounterPayload
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
