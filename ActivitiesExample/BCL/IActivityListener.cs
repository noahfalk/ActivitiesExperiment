using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample
{
    public interface IActivityListener
    {
        void ActivityScopeStarted(ActivitySource source, ref ActivityScope scope);
        void ActivityScopeStopped(ActivitySource source, ref ActivityScope scope);
    }
}
