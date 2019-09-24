using ActivitiesExample.Usage.AI;
using ActivitiesExample.Usage.OTel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ActivitiesExample.Usage
{
    public class App
    {
        public static void MainMethod(string[] args)
        {
            // The API is somewhat arbitrary and orthogonal to the sample, but the key point is that
            // the user wasn't required to explicitly configure anything relating to activity names,
            // listener names, sampling rates, etc.
            //
            TelemetryConfiguration.Active.InstrumentationKey = " *your key* ";
            Tracing.Tracer.AddExporter(new ApplicationInsightsExporter(TelemetryConfiguration.Active));

            // go do some work
        }
    }
}
