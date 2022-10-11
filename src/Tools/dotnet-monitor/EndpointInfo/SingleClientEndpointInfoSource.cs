// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.WebApi;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tools.Monitor
{
    internal sealed class SingleClientEndpointInfoSource : BackgroundService, IEndpointInfoSourceInternal
    {
        // The amount of time to wait before abandoning the attempt to create an EndpointInfo from
        // the enumerated processes. This may happen if a runtime instance is unresponsive to
        // diagnostic pipe commands. Give a generous amount of time, but not too long since a single
        // unresponsive process will cause all HTTP requests to be delayed by the timeout period.
        private static readonly TimeSpan AbandonProcessTimeout = TimeSpan.FromSeconds(3);

        private readonly ILogger<SingleClientEndpointInfoSource> _logger;
        private readonly IEnumerable<IEndpointInfoSourceCallbacks> _callbacks;
        private readonly DiagnosticPortOptions _portOptions;
        private IEndpointInfo _endpointInfo;


        public SingleClientEndpointInfoSource(ILogger<SingleClientEndpointInfoSource> logger,
                IEnumerable<IEndpointInfoSourceCallbacks> callbacks,
                IOptions<DiagnosticPortOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _portOptions = options != null ? options.Value : throw new ArgumentNullException(nameof(options));
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            
        }

        public Task<IEnumerable<IEndpointInfo>> GetEndpointInfoAsync(CancellationToken token)
        {
            return Task.FromResult<IEnumerable<IEndpointInfo>>(_endpointInfo != null ? new[] { _endpointInfo } : Array.Empty<IEndpointInfo>());
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            if ( (_portOptions.ConnectionMode == DiagnosticPortConnectionMode.Connect) && (!string.IsNullOrEmpty(_portOptions.EndpointName)))
            {
                using CancellationTokenSource timeoutTokenSource = new();
                using CancellationTokenSource linkedTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(token, timeoutTokenSource.Token);

                CancellationToken timeoutToken = timeoutTokenSource.Token;
                CancellationToken linkedToken = linkedTokenSource.Token;

                IpcEndpointConfig config = new IpcEndpointConfig(_portOptions.EndpointName, IpcEndpointConfig.TransportType.NamedPipe, IpcEndpointConfig.PortType.Listen);

                timeoutTokenSource.CancelAfter(AbandonProcessTimeout);

                DiagnosticPortIpcEndpoint endpoint = new DiagnosticPortIpcEndpoint(config);
                _endpointInfo =  await EndpointInfo.FromIpcEndpointConfig(config, linkedToken);
                foreach (var callback in _callbacks)
                {
                    await callback.OnBeforeResumeAsync(_endpointInfo, linkedToken);
                }
                DiagnosticsClient client = new DiagnosticsClient(endpoint);
                await client.ResumeRuntimeAsync(linkedToken);
            }
        }
    }
}
