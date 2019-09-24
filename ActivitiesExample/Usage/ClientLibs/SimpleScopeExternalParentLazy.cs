using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.ClientLibs
{
    public static class SimpleScopeExternalParentLazy
    {
        public static ActivitySource s_requestActivity = new ActivitySource("Fabricam.AwesomeProduct.DoMagic");

        public static void DoMagic(Request r)
        {
            using (var scope = new ActivityScope(s_requestActivity, ParseRequestFunc, r))
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

        static GetActivityContext ParseRequestFunc = ParseRequest;
        private static void ParseRequest(object requestObj, ref ActivityContext scope)
        {
            Request r = (Request)requestObj;
            scope = new ActivityContext(r.TraceParent, r.TraceState);
        }
    }
}
