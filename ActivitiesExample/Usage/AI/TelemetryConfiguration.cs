using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.AI
{
    public class TelemetryConfiguration
    {
        public static TelemetryConfiguration Active { get; set; } = new TelemetryConfiguration();

        public string InstrumentationKey { get; set; }
    }
}
