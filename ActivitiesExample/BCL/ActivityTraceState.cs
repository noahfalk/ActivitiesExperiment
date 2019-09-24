using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample
{
    public struct ActivityTraceState
    {
        //TODO: implement better
        public ActivityTraceState(string traceStateString)
        {
            Value = traceStateString;
        }

        public string Value;
    }
}
