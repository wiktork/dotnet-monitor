﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.WebApi
{
    /// <summary>
    /// Utility class to create metric settings (for both configuration and on demand metrics).
    /// </summary>
    internal static class EventCounterSettingsFactory
    {
        public static EventPipeCounterPipelineSettings CreateSettings(GlobalCounterOptions counterOptions, bool includeDefaults,
            int durationSeconds)
        {
            return CreateSettings(includeDefaults, durationSeconds, counterOptions.IntervalSeconds, () => new List<EventPipeCounterGroup>(0));
        }

        public static EventPipeCounterPipelineSettings CreateSettings(GlobalCounterOptions counterOptions, MetricsOptions options)
        {
            return CreateSettings(options.IncludeDefaultProviders.GetValueOrDefault(MetricsOptionsDefaults.IncludeDefaultProviders),
                Timeout.Infinite, counterOptions.IntervalSeconds,
                () => ConvertCounterGroups(options.Providers));
        }

        public static EventPipeCounterPipelineSettings CreateSettings(GlobalCounterOptions counterOptions, int durationSeconds,
            Models.EventMetricsConfiguration configuration)
        {
            return CreateSettings(configuration.IncludeDefaultProviders,
                durationSeconds,
                counterOptions.IntervalSeconds,
                () => ConvertCounterGroups(configuration.Providers));
        }

        private static EventPipeCounterPipelineSettings CreateSettings(bool includeDefaults,
            int durationSeconds,
            int refreshInterval,
            Func<List<EventPipeCounterGroup>> createCounterGroups)
        {
            List<EventPipeCounterGroup> eventPipeCounterGroups = createCounterGroups();

            if (includeDefaults)
            {
                eventPipeCounterGroups.Add(new EventPipeCounterGroup { ProviderName = MonitoringSourceConfiguration.SystemRuntimeEventSourceName });
                eventPipeCounterGroups.Add(new EventPipeCounterGroup { ProviderName = MonitoringSourceConfiguration.MicrosoftAspNetCoreHostingEventSourceName });
                eventPipeCounterGroups.Add(new EventPipeCounterGroup { ProviderName = MonitoringSourceConfiguration.GrpcAspNetCoreServer });
            }

            return new EventPipeCounterPipelineSettings
            {
                CounterGroups = eventPipeCounterGroups.ToArray(),
                Duration = Utilities.ConvertSecondsToTimeSpan(durationSeconds),
                RefreshInterval = TimeSpan.FromSeconds(refreshInterval)
            };
        }

        private static List<EventPipeCounterGroup> ConvertCounterGroups(IList<MetricProvider> providers)
        {
            List<EventPipeCounterGroup> counterGroups = new();

            if (providers?.Count > 0)
            {
                foreach (MetricProvider customProvider in providers)
                {
                    var customCounterGroup = new EventPipeCounterGroup { ProviderName = customProvider.ProviderName };
                    if (customProvider.CounterNames.Count > 0)
                    {
                        customCounterGroup.CounterNames = customProvider.CounterNames.ToArray();
                    }
                    counterGroups.Add(customCounterGroup);
                }
            }

            return counterGroups;
        }

        private static List<EventPipeCounterGroup> ConvertCounterGroups(IList<Models.EventMetricsProvider> providers)
        {
            List<EventPipeCounterGroup> counterGroups = new();

            if (providers?.Count > 0)
            {
                foreach (Models.EventMetricsProvider customProvider in providers)
                {
                    var customCounterGroup = new EventPipeCounterGroup() { ProviderName = customProvider.ProviderName };
                    if (customProvider.CounterNames?.Length > 0)
                    {
                        customCounterGroup.CounterNames = customProvider.CounterNames.ToArray();
                    }

                    counterGroups.Add(customCounterGroup);
                }
            }

            return counterGroups;
        }
    }
}
