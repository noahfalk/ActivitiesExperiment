using ActivitiesExample.Usage.OTel;
using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.AI
{
    class ApplicationInsightsExporter : ITelemetryExporter
    {
        public ApplicationInsightsExporter(TelemetryConfiguration config) { }
    }
}
