using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Stacks
{
    internal sealed class EventStacksPipelineSettings : EventSourcePipelineSettings
    {
        public EventStacksPipelineSettings()
        {
            Duration = System.Threading.Timeout.InfiniteTimeSpan;
        }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    }

    internal sealed class EventStacksPipeline : EventSourcePipeline<EventStacksPipelineSettings>
    {
        private TaskCompletionSource<StackResult> _stackResult = new();
        private StackResult _result = new();

        public EventStacksPipeline(DiagnosticsClient client, EventStacksPipelineSettings settings)
            : base(client, settings)
        {
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return new EventPipeProviderSourceConfiguration(requestRundown: false, bufferSizeInMB: 256, new[]
            {
                new EventPipeProvider("DotnetMonitorStacksEventProvider", System.Diagnostics.Tracing.EventLevel.LogAlways)
            });
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            eventSource.Dynamic.AddCallbackForProviderEvents((string provider, string _) => provider == "DotnetMonitorStacksEventProvider" ?
            EventFilterResponse.AcceptEvent : EventFilterResponse.RejectEvent, Callback);


            Task eventsTimeoutTask = Task.Delay(Settings.Timeout, token);
            Task completedTask = await Task.WhenAny(_stackResult.Task, eventsTimeoutTask);

            token.ThrowIfCancellationRequested();

            await stopSessionAsync();

            eventsTimeoutTask = Task.Delay(Settings.Timeout, token);
            completedTask = await Task.WhenAny(_stackResult.Task, eventsTimeoutTask);

            if (_stackResult.Task.Status != TaskStatus.RanToCompletion)
            {
                throw new TimeoutException("Unable to process stack in timely manner.");
            }
        }

        public Task<StackResult> Result => _stackResult.Task;

        private void Callback(TraceEvent action)
        {
            if (action.ProviderName == "DotnetMonitorStacksEventProvider")
            {
                if (action.ID == (TraceEventID)1)
                {
                    Stack stack = new Stack();
                    stack.ThreadId = (ulong)action.PayloadByName("ThreadId");
                    var functionIds = (ulong[])action.PayloadByName("FunctionIds");
                    var offsets = (ulong[])action.PayloadByName("IpOffsets");
                    _result.Stacks.Add(stack);

                    for (int i = 0; i < functionIds.Length; i++)
                    {
                        stack.Frames.Add(new StackFrame { FunctionId = functionIds[i], Offset = offsets[i] });
                    }
                }
                else if (action.ID == (TraceEventID)2)
                {
                    FunctionData functionData = new();
                    functionData.Name = (string)action.PayloadByName("Name");
                    ulong id = (ulong)action.PayloadByName("FunctionId");
                    functionData.ParentClass = (ulong)action.PayloadByName("ClassId");
                    functionData.ParentToken = (uint)action.PayloadByName("ClassToken");
                    functionData.ModuleId = (ulong)action.PayloadByName("ModuleId");
                    functionData.TypeArgs = (ulong[])action.PayloadByName("TypeArgs") ?? Array.Empty<ulong>();
                    _result.NameCache.FunctionData.Add(id, functionData);
                }
                else if (action.ID == (TraceEventID)3)
                {
                    ClassData classData = new();
                    classData.ModuleId = (ulong)action.PayloadByName("ModuleId");
                    classData.Token = (ulong)(uint)action.PayloadByName("Token");
                    classData.TypeArgs = (ulong[])action.PayloadByName("TypeArgs") ?? Array.Empty<ulong>();
                    ulong id = (ulong)action.PayloadByName("ClassId");

                    _result.NameCache.ClassData.Add(id, classData);
                }
                else if (action.ID == (TraceEventID)4)
                {
                    ModuleData moduleData = new();
                    moduleData.Name = (string)action.PayloadByName("Name");
                    ulong id = (ulong)action.PayloadByName("ModuleId");
                    _result.NameCache.ModuleData.Add(id, moduleData);
                }
                else if (action.ID == (TraceEventID)5)
                {
                    TokenData tokenData = new();

                    tokenData.Name = (string)action.PayloadByName("Name");
                    ulong modId = (ulong)action.PayloadByName("ModuleId");
                    ulong token = (ulong)(uint)action.PayloadByName("Token");
                    tokenData.OuterToken = (ulong)(uint)action.PayloadByName("OuterToken");

                    _result.NameCache.TokenData.Add((modId, token), tokenData);
                }
                else if (action.ID == (TraceEventID)6)
                {
                    _stackResult.TrySetResult(_result);
                }
            }
            else
            {
                Debug.WriteLine(action.ProviderName);
            }
        }
    }

    internal sealed class ClassData
    {

        public ulong[] TypeArgs { get; set; }

        public ulong Token { get; set; }

        public ulong ModuleId { get; set; }
    }

    internal sealed class TokenData
    {
        public ulong OuterToken { get; set; }

        public string Name { get; set; }
    }

    internal sealed class FunctionData
    {
        public string Name { get; set; }
        public ulong ParentClass { get; set; }

        public ulong ParentToken { get; set; }

        public ulong[] TypeArgs { get; set; }

        public ulong ModuleId { get; set; }
    }

    internal sealed class ModuleData
    {
        public string Name { get; set; }
    }

    internal sealed class StackFrame
    {
        public ulong FunctionId { get; set; }

        public ulong Offset { get; set; }
    }

    internal sealed class Stack
    {
        public List<StackFrame> Frames = new List<StackFrame>();
        public ulong ThreadId { get; set; }
    }

    internal sealed class NameCache
    {
        public Dictionary<ulong, ClassData> ClassData = new();
        public Dictionary<ulong, FunctionData> FunctionData = new();
        public Dictionary<ulong, ModuleData> ModuleData = new();
        public Dictionary<(ulong moduleId, ulong typeDef), TokenData> TokenData = new();
    }

    internal sealed class StackResult
    {
        public List<Stack> Stacks = new();
        public NameCache NameCache = new NameCache();
    }
}
