﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Monitoring.EventPipe;
using Microsoft.Diagnostics.Monitoring.Options;
using Microsoft.Diagnostics.Monitoring.WebApi.Stacks;
using Microsoft.Diagnostics.Monitoring.WebApi.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.WebApi.Controllers
{
    [Route("")] // Root
    [ApiController]
    [HostRestriction]
    [Authorize(Policy = AuthConstants.PolicyName)]
#if NETCOREAPP3_1_OR_GREATER
    [ProducesErrorResponseType(typeof(ValidationProblemDetails))]
#endif
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public partial class DiagController : ControllerBase
    {
        private const Models.TraceProfile DefaultTraceProfiles = Models.TraceProfile.Cpu | Models.TraceProfile.Http | Models.TraceProfile.Metrics;
        private static readonly MediaTypeHeaderValue NdJsonHeader = new MediaTypeHeaderValue(ContentTypes.ApplicationNdJson);
        private static readonly MediaTypeHeaderValue JsonSequenceHeader = new MediaTypeHeaderValue(ContentTypes.ApplicationJsonSequence);
        private static readonly MediaTypeHeaderValue TextPlainHeader = new MediaTypeHeaderValue(ContentTypes.TextPlain);

        private readonly ILogger<DiagController> _logger;
        private readonly IDiagnosticServices _diagnosticServices;
        private readonly IOptions<DiagnosticPortOptions> _diagnosticPortOptions;
        private readonly IOptionsMonitor<GlobalCounterOptions> _counterOptions;
        private readonly EgressOperationStore _operationsStore;
        private readonly IDumpService _dumpService;
        private readonly OperationTrackerService _operationTrackerService;

        public DiagController(ILogger<DiagController> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _diagnosticServices = serviceProvider.GetRequiredService<IDiagnosticServices>();
            _diagnosticPortOptions = serviceProvider.GetService<IOptions<DiagnosticPortOptions>>();
            _operationsStore = serviceProvider.GetRequiredService<EgressOperationStore>();
            _dumpService = serviceProvider.GetRequiredService<IDumpService>();
            _counterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<GlobalCounterOptions>>();
            _operationTrackerService = serviceProvider.GetRequiredService<OperationTrackerService>();
        }

        /// <summary>
        /// Get the list of accessible processes.
        /// </summary>
        [HttpGet("processes", Name = nameof(GetProcesses))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationJson)]
        [ProducesResponseType(typeof(IEnumerable<Models.ProcessIdentifier>), StatusCodes.Status200OK)]
        public Task<ActionResult<IEnumerable<Models.ProcessIdentifier>>> GetProcesses()
        {
            return this.InvokeService(async () =>
            {
                IProcessInfo defaultProcessInfo = null;
                try
                {
                    defaultProcessInfo = await _diagnosticServices.GetProcessAsync(null, HttpContext.RequestAborted);
                }
                catch (Exception)
                {
                    // Unable to locate a default process; no action required
                }

                IList<Models.ProcessIdentifier> processesIdentifiers = new List<Models.ProcessIdentifier>();
                foreach (IProcessInfo p in await _diagnosticServices.GetProcessesAsync(processFilter: null, HttpContext.RequestAborted))
                {
                    processesIdentifiers.Add(new Models.ProcessIdentifier()
                    {
                        Pid = p.EndpointInfo.ProcessId,
                        Uid = p.EndpointInfo.RuntimeInstanceCookie,
                        Name = p.ProcessName,
                        IsDefault = (defaultProcessInfo != null &&
                            p.EndpointInfo.ProcessId == defaultProcessInfo.EndpointInfo.ProcessId &&
                            p.EndpointInfo.RuntimeInstanceCookie == defaultProcessInfo.EndpointInfo.RuntimeInstanceCookie)
                    });
                }
                _logger.WrittenToHttpStream();
                return new ActionResult<IEnumerable<Models.ProcessIdentifier>>(processesIdentifiers);
            }, _logger);
        }

        /// <summary>
        /// Get information about the specified process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        [HttpGet("process", Name = nameof(GetProcessInfo))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationJson)]
        [ProducesResponseType(typeof(Models.ProcessInfo), StatusCodes.Status200OK)]
        public Task<ActionResult<Models.ProcessInfo>> GetProcessInfo(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess<Models.ProcessInfo>(processInfo =>
            {
                Models.ProcessInfo processModel = new Models.ProcessInfo()
                {
                    CommandLine = processInfo.CommandLine,
                    Name = processInfo.ProcessName,
                    OperatingSystem = processInfo.OperatingSystem,
                    ProcessArchitecture = processInfo.ProcessArchitecture,
                    Pid = processInfo.EndpointInfo.ProcessId,
                    Uid = processInfo.EndpointInfo.RuntimeInstanceCookie
                };

                _logger.WrittenToHttpStream();

                return processModel;
            },
            processKey);
        }

        /// <summary>
        /// Get the environment block of the specified process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        [HttpGet("env", Name = nameof(GetProcessEnvironment))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationJson)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
        public Task<ActionResult<Dictionary<string, string>>> GetProcessEnvironment(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess<Dictionary<string, string>>(async processInfo =>
            {
                var client = new DiagnosticsClient(processInfo.EndpointInfo.Endpoint);

                try
                {
                    Dictionary<string, string> environment = await client.GetProcessEnvironmentAsync(HttpContext.RequestAborted);

                    _logger.WrittenToHttpStream();

                    return environment;
                }
                catch (ServerErrorException)
                {
                    throw new InvalidOperationException(Strings.ErrorMessage_CanNotGetEnvironment);
                }
            },
            processKey);
        }

        /// <summary>
        /// Capture a dump of a process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="type">The type of dump to capture.</param>
        /// <param name="egressProvider">The egress provider to which the dump is saved.</param>
        /// <returns></returns>
        [HttpGet("dump", Name = nameof(CaptureDump))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationOctetStream)]
        // FileResult is the closest representation of the output so that the OpenAPI document correctly
        // describes the result as a binary file.
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Dump)]
        [EgressValidation]
        public Task<ActionResult> CaptureDump(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery]
            Models.DumpType type = Models.DumpType.WithHeap,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(async processInfo =>
            {
                string dumpFileName = DumpUtilities.GenerateDumpFileName();

                if (string.IsNullOrEmpty(egressProvider))
                {
                    Stream dumpStream = await _dumpService.DumpAsync(processInfo.EndpointInfo, type, HttpContext.RequestAborted);

                    _logger.WrittenToHttpStream();
                    //Compression is done automatically by the response
                    //Chunking is done because the result has no content-length
                    return File(dumpStream, ContentTypes.ApplicationOctetStream, dumpFileName);
                }
                else
                {
                    KeyValueLogScope scope = Utilities.CreateArtifactScope(Utilities.ArtifactType_Dump, processInfo.EndpointInfo);

                    return await SendToEgress(new EgressOperation(
                        token => _dumpService.DumpAsync(processInfo.EndpointInfo, type, token),
                        egressProvider,
                        dumpFileName,
                        processInfo.EndpointInfo,
                        ContentTypes.ApplicationOctetStream,
                        scope), limitKey: Utilities.ArtifactType_Dump);
                }
            }, processKey, Utilities.ArtifactType_Dump);
        }

        /// <summary>
        /// Capture a GC dump of a process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="egressProvider">The egress provider to which the GC dump is saved.</param>
        /// <returns></returns>
        [HttpGet("gcdump", Name = nameof(CaptureGcDump))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationOctetStream)]
        // FileResult is the closest representation of the output so that the OpenAPI document correctly
        // describes the result as a binary file.
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_GCDump)]
        [EgressValidation]
        public Task<ActionResult> CaptureGcDump(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(processInfo =>
            {
                string fileName = FormattableString.Invariant($"{Utilities.GetFileNameTimeStampUtcNow()}_{processInfo.EndpointInfo.ProcessId}.gcdump");

                return Result(
                    Utilities.ArtifactType_GCDump,
                    egressProvider,
                    (stream, token) => GCDumpUtilities.CaptureGCDumpAsync(processInfo.EndpointInfo, stream, token),
                    fileName,
                    ContentTypes.ApplicationOctetStream,
                    processInfo.EndpointInfo);
            }, processKey, Utilities.ArtifactType_GCDump);
        }

        /// <summary>
        /// Capture a trace of a process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="profile">The profiles enabled for the trace session.</param>
        /// <param name="durationSeconds">The duration of the trace session (in seconds).</param>
        /// <param name="egressProvider">The egress provider to which the trace is saved.</param>
        [HttpGet("trace", Name = nameof(CaptureTrace))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationOctetStream)]
        // FileResult is the closest representation of the output so that the OpenAPI document correctly
        // describes the result as a binary file.
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Trace)]
        [EgressValidation]
        public Task<ActionResult> CaptureTrace(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery]
            Models.TraceProfile profile = DefaultTraceProfiles,
            [FromQuery][Range(-1, int.MaxValue)]
            int durationSeconds = 30,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(processInfo =>
            {
                TimeSpan duration = Utilities.ConvertSecondsToTimeSpan(durationSeconds);

                var aggregateConfiguration = TraceUtilities.GetTraceConfiguration(profile, _counterOptions.CurrentValue.GetIntervalSeconds());

                return StartTrace(processInfo, aggregateConfiguration, duration, egressProvider);
            }, processKey, Utilities.ArtifactType_Trace);
        }

        /// <summary>
        /// Capture a trace of a process.
        /// </summary>
        /// <param name="configuration">The trace configuration describing which events to capture.</param>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="durationSeconds">The duration of the trace session (in seconds).</param>
        /// <param name="egressProvider">The egress provider to which the trace is saved.</param>
        [HttpPost("trace", Name = nameof(CaptureTraceCustom))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationOctetStream)]
        // FileResult is the closest representation of the output so that the OpenAPI document correctly
        // describes the result as a binary file.
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Trace)]
        [EgressValidation]
        public Task<ActionResult> CaptureTraceCustom(
            [FromBody][Required]
            Models.EventPipeConfiguration configuration,
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery][Range(-1, int.MaxValue)]
            int durationSeconds = 30,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(processInfo =>
            {
                foreach(Models.EventPipeProvider provider in configuration.Providers)
                {
                    if (!CounterValidator.ValidateProvider(_counterOptions.CurrentValue,
                        provider, out string errorMessage))
                    {
                        throw new ValidationException(errorMessage);
                    }
                }

                TimeSpan duration = Utilities.ConvertSecondsToTimeSpan(durationSeconds);

                var traceConfiguration = TraceUtilities.GetTraceConfiguration(configuration.Providers, configuration.RequestRundown, configuration.BufferSizeInMB);

                return StartTrace(processInfo, traceConfiguration, duration, egressProvider);
            }, processKey, Utilities.ArtifactType_Trace);
        }

        /// <summary>
        /// Capture a stream of logs from a process.
        /// </summary>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="durationSeconds">The duration of the logs session (in seconds).</param>
        /// <param name="level">The level of the logs to capture.</param>
        /// <param name="egressProvider">The egress provider to which the logs are saved.</param>
        [HttpGet("logs", Name = nameof(CaptureLogs))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationNdJson, ContentTypes.ApplicationJsonSequence, ContentTypes.TextPlain)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Logs)]
        [EgressValidation]
        public Task<ActionResult> CaptureLogs(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery][Range(-1, int.MaxValue)]
            int durationSeconds = 30,
            [FromQuery]
            LogLevel? level = null,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(processInfo =>
            {
                TimeSpan duration = Utilities.ConvertSecondsToTimeSpan(durationSeconds);

                var settings = new EventLogsPipelineSettings()
                {
                    Duration = duration
                };

                // Use log level query parameter if specified, otherwise use application-defined filters.
                if (level.HasValue)
                {
                    settings.LogLevel = level.Value;
                    settings.UseAppFilters = false;
                }
                else
                {
                    settings.UseAppFilters = true;
                }

                return StartLogs(processInfo, settings, egressProvider);
            }, processKey, Utilities.ArtifactType_Logs);
        }

        /// <summary>
        /// Capture a stream of logs from a process.
        /// </summary>
        /// <param name="configuration">The logs configuration describing which logs to capture.</param>
        /// <param name="pid">Process ID used to identify the target process.</param>
        /// <param name="uid">The Runtime instance cookie used to identify the target process.</param>
        /// <param name="name">Process name used to identify the target process.</param>
        /// <param name="durationSeconds">The duration of the logs session (in seconds).</param>
        /// <param name="egressProvider">The egress provider to which the logs are saved.</param>
        [HttpPost("logs", Name = nameof(CaptureLogsCustom))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationNdJson, ContentTypes.ApplicationJsonSequence, ContentTypes.TextPlain)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Logs)]
        [EgressValidation]
        public Task<ActionResult> CaptureLogsCustom(
            [FromBody]
            Models.LogsConfiguration configuration,
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery][Range(-1, int.MaxValue)]
            int durationSeconds = 30,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(processInfo =>
            {
                TimeSpan duration = Utilities.ConvertSecondsToTimeSpan(durationSeconds);

                var settings = new EventLogsPipelineSettings()
                {
                    Duration = duration,
                    FilterSpecs = configuration.FilterSpecs,
                    LogLevel = configuration.LogLevel,
                    UseAppFilters = configuration.UseAppFilters
                };

                return StartLogs(processInfo, settings, egressProvider);
            }, processKey, Utilities.ArtifactType_Logs);
        }

        /// <summary>
        /// Gets versioning and listening mode information about Dotnet-Monitor 
        /// </summary>
        [HttpGet("info", Name = nameof(GetInfo))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationJson)]
        [ProducesResponseType(typeof(Models.DotnetMonitorInfo), StatusCodes.Status200OK)]
        public ActionResult<Models.DotnetMonitorInfo> GetInfo()
        {
            return this.InvokeService(() =>
            {
                string version = GetDotnetMonitorVersion();
                string runtimeVersion = Environment.Version.ToString();
                DiagnosticPortConnectionMode diagnosticPortMode = _diagnosticPortOptions.Value.GetConnectionMode();
                string diagnosticPortName = GetDiagnosticPortName();

                Models.DotnetMonitorInfo dotnetMonitorInfo = new Models.DotnetMonitorInfo()
                {
                    Version = version,
                    RuntimeVersion = runtimeVersion,
                    DiagnosticPortMode = diagnosticPortMode,
                    DiagnosticPortName = diagnosticPortName
                };

                _logger.WrittenToHttpStream();
                return new ActionResult<Models.DotnetMonitorInfo>(dotnetMonitorInfo);
            }, _logger);
        }

        [HttpGet("stacks", Name = nameof(CaptureStacks))]
        [ProducesWithProblemDetails(ContentTypes.ApplicationNdJson, ContentTypes.ApplicationJson, ContentTypes.TextPlain)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
        [RequestLimit(LimitKey = Utilities.ArtifactType_Stacks)]
        [EgressValidation]
        public async Task<ActionResult> CaptureStacks(
            [FromQuery]
            int? pid = null,
            [FromQuery]
            Guid? uid = null,
            [FromQuery]
            string name = null,
            [FromQuery][Range(-1, int.MaxValue)]
            int durationSeconds = 5,
            [FromQuery]
            string egressProvider = null)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            System.Net.Sockets.UnixDomainSocketEndPoint ep =
                new System.Net.Sockets.UnixDomainSocketEndPoint(Environment.ExpandEnvironmentVariables(@"%TEMP%\sampler.sock"));

            using System.Net.Sockets.Socket s =
                new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);

#if NET6_0_OR_GREATER
            await s.ConnectAsync(ep);
#endif
            return await InvokeForProcess(async processInfo =>
            {

                var settings = new EventStacksPipelineSettings
                {
                    Duration = TimeSpan.FromMilliseconds(10)
                };

                string tmpFile = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), ".nettrace");
                using (FileStream file = new FileStream(tmpFile, FileMode.CreateNew))
                {
                    await using var eventTracePipeline = new EventStacksPipeline(new DiagnosticsClient(processInfo.EndpointInfo.Endpoint),
                    settings);

                    await eventTracePipeline.RunAsync(HttpContext.RequestAborted);
                }

                //TODO Convert to a realtime consumption with no interim artifact


                return await Result(Utilities.ArtifactType_Stacks, egressProvider, (stream, token) =>
                {
                    //using (var eventLog = new Tracing.Etlx.TraceLog(tempEtlxFilename))
                    //using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 4096, leaveOpen: true))
                    //{
                    //    foreach(var traceEvent in eventLog.Events)
                    //    {
                    //        continue;
                    //    }
                    //}

                    return Task.FromResult(new OkResult());
                }, "test.stacks", ContentTypes.TextPlain, processInfo.EndpointInfo, asAttachment: false);

            }, processKey, Utilities.ArtifactType_Stacks);
        }

        private Task<ActionResult> UseRundown(int pid, Guid uid, string name, string egressProvider)
        {
            ProcessKey? processKey = GetProcessKey(pid, uid, name);

            return InvokeForProcess(async processInfo =>
            {
                var configuration = new EventPipeProviderSourceConfiguration(
                    requestRundown: true,
                    providers: new EventPipeProvider(MonitoringSourceConfiguration.SampleProfilerProviderName,
                                                     System.Diagnostics.Tracing.EventLevel.Informational));

                EventTracePipelineSettings settings = new EventTracePipelineSettings
                {
                    Configuration = configuration,
                    Duration = TimeSpan.FromMilliseconds(10)
                };

                string tmpFile = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), ".nettrace");
                using (FileStream file = new FileStream(tmpFile, FileMode.CreateNew))
                {
                    await using EventTracePipeline eventTracePipeline = new EventTracePipeline(new DiagnosticsClient(processInfo.EndpointInfo.Endpoint),
                    settings, async (Stream stream, CancellationToken token) =>
                    {
                        await stream.CopyToAsync(file, 0x1000, HttpContext.RequestAborted);
                    });
                    await eventTracePipeline.RunAsync(HttpContext.RequestAborted);
                }

                var traceLog = Microsoft.Diagnostics.Tracing.Etlx.TraceLog.CreateFromEventTraceLogFile(tmpFile);

                return await Result(Utilities.ArtifactType_Stacks, egressProvider, async (stream, token) =>
                {
                    await SerializeStack(stream, traceLog);
                }, "test.stacks", ContentTypes.TextPlain, processInfo.EndpointInfo, asAttachment: false);
            }, processKey, Utilities.ArtifactType_Stacks);
        }

        public async Task SerializeStack(Stream stream, string tempEtlxFilename)
        {
            try
            {
                using (var symbolReader = new SymbolReader(System.IO.TextWriter.Null))
                using (var eventLog = new Tracing.Etlx.TraceLog(tempEtlxFilename))
                using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 4096, leaveOpen: true))
                {
                    var stackSource = new MutableTraceEventStackSource(eventLog)
                    {
                        OnlyManagedCodeStacks = true
                    };

                    var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
                    computer.GenerateThreadTimeStacks(stackSource);

                    var samplesForThread = new Dictionary<int, List<StackSourceSample>>();

                    stackSource.ForEach((sample) =>
                    {
                        var stackIndex = sample.StackIndex;
                        while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
                            stackIndex = stackSource.GetCallerIndex(stackIndex);

                        // long form for: int.Parse(threadFrame["Thread (".Length..^1)])
                        // Thread id is in the frame name as "Thread (<ID>)"
                        string template = "Thread (";
                        string threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                        int threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

                        if (samplesForThread.TryGetValue(threadId, out var samples))
                        {
                            samples.Add(sample);
                        }
                        else
                        {
                            samplesForThread[threadId] = new List<StackSourceSample>() { sample };
                        }
                    });

                    // For every thread recorded in our trace, print the first stack
                    foreach (var (threadId, samples) in samplesForThread)
                    {
                        await PrintStack(writer, threadId, samples[0], stackSource);
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
            }
        }

        private static async Task PrintStack(TextWriter writer, int threadId, StackSourceSample stackSourceSample, StackSource stackSource)
        {
            await writer.WriteLineAsync($"Thread (0x{threadId:X}):");
            var stackIndex = stackSourceSample.StackIndex;
            while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false).StartsWith("Thread ("))
            {
                string frame = $"  {stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false)}"
                    .Replace("UNMANAGED_CODE_TIME", "[Native Frames]");
                stackIndex = stackSource.GetCallerIndex(stackIndex);

                await writer.WriteLineAsync(frame);
            }
            await writer.WriteLineAsync();
        }

        private static string GetDotnetMonitorVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var assemblyVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (assemblyVersionAttribute is null)
            {
                return assembly.GetName().Version.ToString();
            }
            else
            {
                return assemblyVersionAttribute.InformationalVersion;
            }
        }

        private string GetDiagnosticPortName()
        {
            return _diagnosticPortOptions.Value.EndpointName;
        }

        private Task<ActionResult> StartTrace(
            IProcessInfo processInfo,
            MonitoringSourceConfiguration configuration,
            TimeSpan duration,
            string egressProvider)
        {
            string fileName = TraceUtilities.GenerateTraceFileName(processInfo.EndpointInfo);

            return Result(
                Utilities.ArtifactType_Trace,
                egressProvider,
                async (outputStream, token) =>
                {
                    IDisposable operationRegistration = null;
                    try
                    {
                        if (_diagnosticPortOptions.Value.ConnectionMode == DiagnosticPortConnectionMode.Listen)
                        {
                            operationRegistration = _operationTrackerService.Register(processInfo.EndpointInfo);
                        }
                        await TraceUtilities.CaptureTraceAsync(null, processInfo.EndpointInfo, configuration, duration, outputStream, token);
                    }
                    finally
                    {
                        operationRegistration?.Dispose();
                    }
                },
                fileName,
                ContentTypes.ApplicationOctetStream,
                processInfo.EndpointInfo);
        }

        private Task<ActionResult> StartLogs(
            IProcessInfo processInfo,
            EventLogsPipelineSettings settings,
            string egressProvider)
        {
            LogFormat? format = ComputeLogFormat(Request.GetTypedHeaders().Accept);
            if (null == format)
            {
                return Task.FromResult<ActionResult>(this.NotAcceptable());
            }

            string fileName = LogsUtilities.GenerateLogsFileName(processInfo.EndpointInfo);
            string contentType = LogsUtilities.GetLogsContentType(format.Value);

            return Result(
                Utilities.ArtifactType_Logs,
                egressProvider,
                (outputStream, token) => LogsUtilities.CaptureLogsAsync(null, format.Value, processInfo.EndpointInfo, settings, outputStream, token),
                fileName,
                contentType,
                processInfo.EndpointInfo,
                format != LogFormat.PlainText);
        }

        private static ProcessKey? GetProcessKey(int? pid, Guid? uid, string name)
        {
            return (pid == null && uid == null && name == null) ? null : new ProcessKey(pid, uid, name);
        }

        private static LogFormat? ComputeLogFormat(IList<MediaTypeHeaderValue> acceptedHeaders)
        {
            if (acceptedHeaders == null)
            {
                return null;
            }

            if (acceptedHeaders.Contains(TextPlainHeader))
            {
                return LogFormat.PlainText;
            }
            if (acceptedHeaders.Contains(NdJsonHeader))
            {
                return LogFormat.NewlineDelimitedJson;
            }
            if (acceptedHeaders.Contains(JsonSequenceHeader))
            {
                return LogFormat.JsonSequence;
            }
            if (acceptedHeaders.Any(h => TextPlainHeader.IsSubsetOf(h)))
            {
                return LogFormat.PlainText;
            }
            if (acceptedHeaders.Any(h => NdJsonHeader.IsSubsetOf(h)))
            {
                return LogFormat.NewlineDelimitedJson;
            }
            if (acceptedHeaders.Any(h => JsonSequenceHeader.IsSubsetOf(h)))
            {
                return LogFormat.JsonSequence;
            }
            return null;
        }

        private Task<ActionResult> Result(
            string artifactType,
            string providerName,
            Func<Stream, CancellationToken, Task> action,
            string fileName,
            string contentType,
            IEndpointInfo endpointInfo,
            bool asAttachment = true)
        {
            KeyValueLogScope scope = Utilities.CreateArtifactScope(artifactType, endpointInfo);

            if (string.IsNullOrEmpty(providerName))
            {
                return Task.FromResult<ActionResult>(new OutputStreamResult(
                    action,
                    contentType,
                    asAttachment ? fileName : null,
                    scope));
            }
            else
            {
                return SendToEgress(new EgressOperation(
                    action,
                    providerName,
                    fileName,
                    endpointInfo,
                    contentType,
                    scope),
                    limitKey: artifactType);
            }
        }

        private async Task<ActionResult> SendToEgress(EgressOperation egressStreamResult, string limitKey)
        {
            // Will throw TooManyRequestsException if there are too many concurrent operations.
            Guid operationId = await _operationsStore.AddOperation(egressStreamResult, limitKey);
            string newUrl = this.Url.Action(
                action: nameof(OperationsController.GetOperationStatus),
                controller: OperationsController.ControllerName, new { operationId = operationId },
                protocol: this.HttpContext.Request.Scheme, this.HttpContext.Request.Host.ToString());

            return Accepted(newUrl);
        }

        private Task<ActionResult> InvokeForProcess(Func<IProcessInfo, ActionResult> func, ProcessKey? processKey, string artifactType = null)
        {
            Func<IProcessInfo, Task<ActionResult>> asyncFunc =
                processInfo => Task.FromResult(func(processInfo));

            return InvokeForProcess(asyncFunc, processKey, artifactType);
        }

        private async Task<ActionResult> InvokeForProcess(Func<IProcessInfo, Task<ActionResult>> func, ProcessKey? processKey, string artifactType)
        {
            ActionResult<object> result = await InvokeForProcess<object>(async processInfo => await func(processInfo), processKey, artifactType);

            return result.Result;
        }

        private Task<ActionResult<T>> InvokeForProcess<T>(Func<IProcessInfo, ActionResult<T>> func, ProcessKey? processKey, string artifactType = null)
        {
            return InvokeForProcess(processInfo => Task.FromResult(func(processInfo)), processKey, artifactType);
        }

        private async Task<ActionResult<T>> InvokeForProcess<T>(Func<IProcessInfo, Task<ActionResult<T>>> func, ProcessKey? processKey, string artifactType = null)
        {
            IDisposable artifactTypeRegistration = null;
            if (!string.IsNullOrEmpty(artifactType))
            {
                KeyValueLogScope artifactTypeScope = new KeyValueLogScope();
                artifactTypeScope.AddArtifactType(artifactType);
                artifactTypeRegistration = _logger.BeginScope(artifactTypeScope);
            }

            try
            {
                return await this.InvokeService(async () =>
                {
                    IProcessInfo processInfo = await _diagnosticServices.GetProcessAsync(processKey, HttpContext.RequestAborted);

                    KeyValueLogScope processInfoScope = new KeyValueLogScope();
                    processInfoScope.AddArtifactEndpointInfo(processInfo.EndpointInfo);
                    using var _ = _logger.BeginScope(processInfoScope);

                    _logger.ResolvedTargetProcess();

                    return await func(processInfo);
                }, _logger);
            }
            finally
            {
                artifactTypeRegistration?.Dispose();
            }
        }
    }
}