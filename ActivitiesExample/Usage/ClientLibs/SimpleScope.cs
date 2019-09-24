using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample.Usage.ClientLibs
{
    public static class SimpleScope
    {
        public static ActivitySource s_requestActivity = new ActivitySource("Fabricam.AwesomeProduct.DoMagic");

        public static void DoMagic()
        {
            using (var scope = new ActivityScope(s_requestActivity))
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
}
