using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.ClientLibs
{
    public static class SimpleScopeExternalParent
    {
        public static ActivitySource s_requestActivity = new ActivitySource("Fabricam.AwesomeProduct.DoMagic");

        public static void DoMagic(Request r)
        {
            using (var scope = new ActivityScope(s_requestActivity, new ActivityContext(r.TraceParent, r.TraceState)))
            {
                try
                {
                    // do some work
                }
                catch (Exception e) // hopefully this is a more specific exception type related to the work being done
                {
                    scope.SetStatus(e);
                }
            }
        }
    }

    public class Request
    {
        public string TraceParent { get; }
        public string TraceState { get; }
    }
}
