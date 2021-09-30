// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.WebApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tools.Monitor.CollectionRules
{
    internal sealed class MetricsCallbacks : IEndpointInfoSourceCallbacks
    {
        private readonly MetricsServiceReduced _metricsService;

        public MetricsCallbacks(MetricsServiceReduced service)
        {
            _metricsService = service;
        }

        public Task OnAddedEndpointInfoAsync(IEndpointInfo endpointInfo, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task OnBeforeResumeAsync(IEndpointInfo endpointInfo, CancellationToken cancellationToken)
        {
            //TODO Check if it's the default process, but cannot use IDiagnosticServices
            //_ = _metricsService.ExecuteAsync(endpointInfo, cancellationToken);
            await Task.Delay(1000);
            return;
        }

        public void OnRemovedEndpointInfo(IEndpointInfo endpointInfo)
        {
        }
    }

    internal class CollectionRuleEndpointInfoSourceCallbacks :
        IEndpointInfoSourceCallbacks
    {
        private readonly CollectionRuleService _service;

        public CollectionRuleEndpointInfoSourceCallbacks(CollectionRuleService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Task OnAddedEndpointInfoAsync(IEndpointInfo endpointInfo, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnBeforeResumeAsync(IEndpointInfo endpointInfo, CancellationToken cancellationToken)
        {
            return _service.ApplyRules(endpointInfo, cancellationToken);
        }

        public void OnRemovedEndpointInfo(IEndpointInfo endpointInfo)
        {
        }
    }
}
