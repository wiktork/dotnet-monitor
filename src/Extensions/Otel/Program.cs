// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Monitoring.Extension.Common;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.CommandLine;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Microsoft.Diagnostics.Monitoring.Otel
{
    internal sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Expected command line format is: dotnet-monitor-egress-otel.exe Egress
            CliRootCommand rootCommand = new CliRootCommand("Egresses an artifact to Azure storage.");


            CliCommand executeCommand = new CliCommand("Execute", "Execute is for egressing an artifact.");
            executeCommand.SetAction((result, token) => Egress(token));

            CliCommand egressCommand = new CliCommand("Egress", "The class of extension being invoked.")
            {
                executeCommand
            };

            rootCommand.Add(egressCommand);

            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static async Task Egress(CancellationToken token)
        {
           // var payload = await EgressHelper.GetPayload(CancellationToken.None);

            var stream = EgressHelper.GetStdInStream();

            //TODO Bind options here

            //TODO Can we do otel with just a ServiceCollection?

            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            //var serviceProvider = services.BuildServiceProvider();

            //var factory = serviceProvider.GetRequiredService<ILoggerFactory>();


//            Dictionary<string, ILogger> _loggers = new Dictionary<string, ILogger>();

            using (StreamReader reader = new StreamReader(stream, leaveOpen: true))
            {

                string line = null;
                while ((line = await reader.ReadLineAsync(token)) != null)
                {
                    JsonSerializer.Deserialize<JsonElement>(line);

                    //Create or find logger
                    //push log entry to otel.
                }
            }

        }


        private static void ConfigureServices(IServiceCollection services)
        {
            var reader = new BaseExportingMetricReader(new OtlpMetricExporter(new OtlpExporterOptions
            {
                Protocol = OtlpExportProtocol.Grpc,
                Endpoint = new System.Uri("http://127.0.0.1:4317")
            }));

            var b = services.AddOpenTelemetry();
            b.WithMetrics(builder => {
                builder.AddMeter("System.Runtime");
                builder.AddView(Config);
                builder.AddReader(reader);
            });
            //TODO Convert this to WithLogging once it's no longer experimental
            services.AddLogging(builder => builder.AddOpenTelemetry(opt => {
                opt.AddProcessor(new SimpleLogRecordExportProcessor(new OtlpLogExporter(new OtlpExporterOptions
                {
                    Protocol = OtlpExportProtocol.Grpc,
                    Endpoint = new System.Uri("http://127.0.0.1:4317"),
                })));
            }));
        }

        private static MetricStreamConfiguration Config(Instrument instrument)
        {
            if (!instrument.Name.StartsWith("dm_"))
            {
                return MetricStreamConfiguration.Drop;
            }

            return new MetricStreamConfiguration
            {
                Name = instrument.Name.Substring(3)
            };
        }
    }
}
