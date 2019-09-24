using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.OTel
{
    public class Tracer
    {
        ActivityListener _listener;

        public Tracer()
        {
            _listener = new ActivityListener(NameFilter, SampleFilter);
        }

        bool NameFilter(string name) => !name.StartsWith("NoisyLib.");
        bool SampleFilter(ref ActivityScope scope) => false;

        public void AddExporter(ITelemetryExporter exporter)
        {
            // ...
        }
    }
}
