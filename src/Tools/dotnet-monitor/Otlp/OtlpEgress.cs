// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using System;
using System.Diagnostics.Metrics;

namespace Microsoft.Diagnostics.Tools.Monitor.Otlp
{
    internal sealed class CustomMeterProviderBuilder : MeterProviderBuilder
    {
        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            return this;
        }

        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            return this;
        }
    }

    internal sealed class SimpleInstrument : Instrument
    {
        public SimpleInstrument(Meter meter, string name, string unit, string description) : base(meter, name, unit, description)
        {
        }
    }

    internal class OtlpEgress
    {
        private readonly IMetricsListener _listener;
        private readonly Instrument _instrument;
        public OtlpEgress()
        {
            HostBuilder builder = new HostBuilder();
            builder.ConfigureServices((s) =>
            {
                var otelBuilder = s.AddOpenTelemetry();
                otelBuilder.WithMetrics(configure =>
                {
                    configure.AddMeter("System.Runtime");
                    configure.AddOtlpExporter();
                });
            });

            var host = builder.Build();
            var listener = host.Services.GetRequiredService<IMetricsListener>();
            _listener = listener;
            _instrument = new SimpleInstrument(new Meter("System.Runtime"), "Test", "MiB", "Test");



            
        }

        public void EgressMetrics()
        {
            bool result = _listener.InstrumentPublished(_instrument, out _);
            _listener.GetMeasurementHandlers().IntHandler(_instrument, 5, null, null);

        }

    }
}
