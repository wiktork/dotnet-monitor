// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.IO;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Controllers
{
    [Route("")]
    [ApiController]
#if NETCOREAPP3_1_OR_GREATER
    [ProducesErrorResponseType(typeof(ValidationProblemDetails))]
#endif
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public class MetricsController : ControllerBase
    {
        static string SummaryTemplate = @"
# HELP rpc_duration_seconds A summary of the RPC duration in seconds.
# TYPE rpc_duration_seconds summary
rpc_duration_seconds{{quantile=""0.5""}} {0}
rpc_duration_seconds{{quantile=""0.9""}} {1}
rpc_duration_seconds{{quantile=""0.95""}} {2}
rpc_duration_seconds_sum {3}
rpc_duration_seconds_count {4}";

        private const string ArtifactType_Metrics = "metrics";

        private readonly ILogger<MetricsController> _logger;
        private readonly MetricsStoreService _metricsStore;
        private readonly MetricsOptions _metricsOptions;

        public MetricsController(ILogger<MetricsController> logger,
            IServiceProvider serviceProvider,
            IOptions<MetricsOptions> metricsOptions)
        {
            _logger = logger;
            _metricsStore = serviceProvider.GetService<MetricsStoreService>();
            _metricsOptions = metricsOptions.Value;

            SummaryTemplate = SummaryTemplate.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Get a list of the current backlog of metrics for a process in the Prometheus exposition format.
        /// </summary>
        [HttpGet("metrics", Name = nameof(GetMetrics))]
        [ProducesWithProblemDetails(ContentTypes.TextPlain_v0_0_4)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public ActionResult GetMetrics()
        {
            return this.InvokeService(() =>
            {
                if (!_metricsOptions.GetEnabled())
                {
                    throw new InvalidOperationException(Strings.ErrorMessage_MetricsDisabled);
                }

                KeyValueLogScope scope = new KeyValueLogScope();
                scope.AddArtifactType(ArtifactType_Metrics);

                return new OutputStreamResult(async (outputStream, token) =>
                    {
                        await _metricsStore.MetricsStore.SnapshotMetrics(outputStream, token);
                    },
                    ContentTypes.TextPlain_v0_0_4,
                    null,
                    scope);
            }, _logger);
        }

        /// <summary>
        /// Get a list of the current backlog of metrics for a process in the Prometheus exposition format.
        /// </summary>
        [HttpGet("metricstest", Name = nameof(GetMetricsTest))]
        [ProducesWithProblemDetails(ContentTypes.TextPlain_v0_0_4)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public ActionResult GetMetricsTest()
        {
            return this.InvokeService(() =>
            {
                if (!_metricsOptions.GetEnabled())
                {
                    throw new InvalidOperationException(Strings.ErrorMessage_MetricsDisabled);
                }

                KeyValueLogScope scope = new KeyValueLogScope();
                scope.AddArtifactType(ArtifactType_Metrics);

                return new OutputStreamResult(async (outputStream, token) =>
                {
                    await using StreamWriter writer = new StreamWriter(outputStream, leaveOpen: true);
                    writer.NewLine = "\n";
                    int request = ++_metricsStore.MetricsStore.RequestCount;

                    int multi = 10;
                    if (request % 2 == 0)
                    {
                        multi = 1;
                    }
                    await writer.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, SummaryTemplate,
                        request * 2,
                        request * 3,
                        request * 4,
                        request * 5 * multi,
                        request * 6 * multi));
                },
                ContentTypes.TextPlain_v0_0_4,
                null,
                scope);
            }, _logger);
        }
    }
}
