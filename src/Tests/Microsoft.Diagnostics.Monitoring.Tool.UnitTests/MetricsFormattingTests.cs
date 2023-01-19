﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Diagnostics.Monitoring.WebApi;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics.Monitoring.Tool.UnitTests
{
    public sealed class MetricsFormattingTests
    {
        private ITestOutputHelper _outputHelper;

        private readonly string MeterName = "MeterName";
        private readonly string InstrumentName = "InstrumentName";
        private readonly DateTime Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(10000000).DateTime;
        private readonly ILogger<MetricsStoreService> _logger;

        private const int MetricCount = 1;
        private const int Value1 = 1;
        private const int Value2 = 2;
        private const int Value3 = 3;
        private const int IntervalSeconds = 10;

        public MetricsFormattingTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;

            LoggerFactory factory = new LoggerFactory();
            _logger = factory.CreateLogger<MetricsStoreService>();
        }

        [Fact]
        public async Task HistogramFormat_Test()
        {
            List<ICounterPayload> payload = new();

            string tags1 = "Percentile=50";
            payload.Add(new PercentilePayload(MeterName, InstrumentName, "DisplayName", string.Empty, tags1, Value1, Timestamp));

            string tags2 = "Percentile=95";
            payload.Add(new PercentilePayload(MeterName, InstrumentName, "DisplayName", string.Empty, tags2, Value2, Timestamp));

            string tags3 = "Percentile=99";
            payload.Add(new PercentilePayload(MeterName, InstrumentName, "DisplayName", string.Empty, tags3, Value3, Timestamp));

            using MemoryStream stream = await GetMetrics(payload);
            List<string> lines = ReadStream(stream);

            // Question - this is manually recreating what PrometheusDataModel.GetPrometheusNormalizedName does to get the metric name;
            // should we call this method, or should this also be implicitly testing its behavior by having this hard-coded?
            string metricName = $"{MeterName.ToLowerInvariant()}_{payload[0].Name}";

            const string percentile_50 = "{Percentile=\"50\"}";
            const string percentile_95 = "{Percentile=\"95\"}";
            const string percentile_99 = "{Percentile=\"99\"}";

            Assert.Equal(5, lines.Count);
            Assert.Equal(FormattableString.Invariant($"# HELP {metricName}{payload[0].Unit} {payload[0].DisplayName}"), lines[0]);
            Assert.Equal(FormattableString.Invariant($"# TYPE {metricName} summary"), lines[1]);
            Assert.Equal(FormattableString.Invariant($"{metricName}{percentile_50} {payload[0].Value}"), lines[2]);
            Assert.Equal(FormattableString.Invariant($"{metricName}{percentile_95} {payload[1].Value}"), lines[3]);
            Assert.Equal(FormattableString.Invariant($"{metricName}{percentile_99} {payload[2].Value}"), lines[4]);
        }

        [Fact]
        public async Task GaugeFormat_Test()
        {
            ICounterPayload payload = new GaugePayload(MeterName, InstrumentName, "DisplayName", "", null, Value1, Timestamp);

            MemoryStream stream = await GetMetrics(new() { payload });

            List<string> lines = ReadStream(stream);

            // Question - this is manually recreating what PrometheusDataModel.GetPrometheusNormalizedName does to get the metric name;
            // should we call this method, or should this also be implicitly testing its behavior by having this hard-coded?
            string metricName = $"{MeterName.ToLowerInvariant()}_{payload.Name}";

            Assert.Equal(3, lines.Count);
            Assert.Equal(FormattableString.Invariant($"# HELP {metricName}{payload.Unit} {payload.DisplayName}"), lines[0]);
            Assert.Equal(FormattableString.Invariant($"# TYPE {metricName} gauge"), lines[1]);
            Assert.Equal(FormattableString.Invariant($"{metricName} {payload.Value} {new DateTimeOffset(payload.Timestamp).ToUnixTimeMilliseconds()}"), lines[2]);
        }

        [Fact]
        public async Task CounterFormat_Test()
        {
            ICounterPayload payload = new RatePayload(MeterName, InstrumentName, "DisplayName", "", null, Value1, IntervalSeconds, Timestamp);

            MemoryStream stream = await GetMetrics(new() { payload });

            List<string> lines = ReadStream(stream);

            // Question - this is manually recreating what PrometheusDataModel.GetPrometheusNormalizedName does to get the metric name;
            // should we call this method, or should this also be implicitly testing its behavior by having this hard-coded?
            string metricName = $"{MeterName.ToLowerInvariant()}_{payload.Name}";

            Assert.Equal(3, lines.Count);
            Assert.Equal($"# HELP {metricName}{payload.Unit} {payload.DisplayName}", lines[0]);
            Assert.Equal($"# TYPE {metricName} counter", lines[1]);
            Assert.Equal($"{metricName} {payload.Value} {new DateTimeOffset(payload.Timestamp).ToUnixTimeMilliseconds()}", lines[2]);
        }

        private async Task<MemoryStream> GetMetrics(List<ICounterPayload> payloads)
        {
            IMetricsStore metricsStore = new MetricsStore(_logger, MetricCount);

            foreach (var payload in payloads)
            {
                metricsStore.AddMetric(payload);
            }

            var outputStream = new MemoryStream();
            await metricsStore.SnapshotMetrics(outputStream, CancellationToken.None);

            return outputStream;
        }

        private static List<string> ReadStream(Stream stream)
        {
            var lines = new List<string>();

            stream.Position = 0;
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    lines.Add(reader.ReadLine());
                }
            }

            return lines;
        }
    }
}
