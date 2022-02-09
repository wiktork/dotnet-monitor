﻿using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Stacks
{
    internal sealed class EventStacksPipelineSettings : EventSourcePipelineSettings
    {
    }

    internal sealed class EventStacksPipeline : EventSourcePipeline<EventStacksPipelineSettings>
    {
        private TaskCompletionSource<StackResult> _stackResult = new();

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

           return base.OnEventSourceAvailable(eventSource, stopSessionAsync, token);
        }

        protected override async Task OnRun(CancellationToken token)
        {
            await Task.WhenAny(base.OnRun(token), _stackResult.Task).Unwrap();
        }

        public Task<StackResult> Result => _stackResult.Task;

        private void Callback(TraceEvent action)
        {
            StackResult result = new StackResult();
            if (action.ID == (TraceEventID)1)
            {
                Stack stack = new Stack();
                stack.ThreadId = (long)action.PayloadByName("ThreadId");
                var functionIds = (long[])action.PayloadByName("FunctionIds");
                var offsets = (long[])action.PayloadByName("IpOffsets");
                result.Stacks.Add(stack);

                for (int i = 0; i < functionIds.Length; i++)
                {
                    stack.Frames.Add(new StackFrame { FunctionId = functionIds[i], Offset = offsets[i] });
                }
            }
            if (action.ID == (TraceEventID)2)
            {
 
            }

            _stackResult.TrySetResult(result);
        }
    }

    internal sealed class ClassData
    {
        public string Name { get; set; }
        public long ParentClass { get; set; }

        public long[] TypeArgs { get; set; }

        long ModuleId { get; set; }
    }

    internal sealed class FunctionData
    {
        public string Name { get; set; }
        public long ParentClass { get; set; }

        public long[] TypeArgs { get; set; }

        long ModuleId { get; set; }
    }

    internal sealed class ModuleData
    {
        public string Name { get; set; }
    }

    internal sealed class StackFrame
    {
        public long FunctionId { get; set; }

        public long Offset { get; set; }
    }

    internal sealed class Stack
    {
        public List<StackFrame> Frames = new List<StackFrame>();
        public long ThreadId { get; set; }
    }

    internal sealed class NameCache
    {
        public Dictionary<long, ClassData> ClassData = new();
        public Dictionary<long, FunctionData> FunctionData = new();
        public Dictionary<long, ModuleData> ModuleData = new();
    }

    internal sealed class StackResult
    {
        public List<Stack> Stacks = new();
        public NameCache NameCache = new NameCache();
    }
}
