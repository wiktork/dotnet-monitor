using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Stacks
{
    internal sealed class EventStacksPipelineSettings : EventSourcePipelineSettings
    {
    }

    internal sealed class EventStacksPipeline : EventSourcePipeline<EventStacksPipelineSettings>
    {
        public EventStacksPipeline(DiagnosticsClient client, EventStacksPipelineSettings settings)
            : base(client, settings)
        {
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return new EventPipeProviderSourceConfiguration(requestRundown: false, bufferSizeInMB: 256, new[]
            {
                new EventPipeProvider("MySuperAwesomeEventPipeProvider", System.Diagnostics.Tracing.EventLevel.LogAlways)
            });
        }

        protected override Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
           eventSource.Dynamic.AddCallbackForProviderEvents((string provider, string _) => provider == "MySuperAwesomeEventPipeProvider" ?
           EventFilterResponse.AcceptEvent : EventFilterResponse.RejectProvider, Callback);

           var attach = new StackTraceEventParser(eventSource);
            attach.

            return base.OnEventSourceAvailable(eventSource, stopSessionAsync, token);
        }

        private void Callback(TraceEvent action)
        {
            
        }
    }
}
