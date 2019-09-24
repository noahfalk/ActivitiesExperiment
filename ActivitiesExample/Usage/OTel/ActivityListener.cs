using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ActivitiesExample.Usage.OTel
{

    delegate bool SampleFunc(ref ActivityScope scope);

    class ActivityListener : IActivityListener
    {
        Func<string, bool> _nameFilter;
        SampleFunc _sampleFunc;    

        public ActivityListener(Func<string, bool> nameFilter, SampleFunc sampleFunc)
        {
            _nameFilter = nameFilter;
            _sampleFunc = sampleFunc;
            SetupCallbacks();
        }

        public void SetupCallbacks()
        {
            ActivitySourceRegistry registry = ActivitySourceRegistry.DefaultRegistry;
            registry.ActivitySourceAdded += (r, s) => AddActivitySource(s);
            registry.ActivitySourceRemoved += (r, s) => s.RemoveListener(this);
            registry.ForEach(AddActivitySource);
        }

        private void AddActivitySource(ActivitySource source)
        {
            if(_nameFilter(source.Name))
            {
                source.AddListener(this);
            }
            else
            {
                source.RemoveListener(this);
            }
        }

        public void ActivityScopeStarted(ActivitySource source, ref ActivityScope scope)
        {
            if(_sampleFunc(ref scope))
            {
                scope.EnsureActivityCreated().ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;
            }
        }

        public void ActivityScopeStopped(ActivitySource source, ref ActivityScope scope)
        {
            Activity a = scope.Activity;
            if (a != null)
            {
                LogCompletedActivity(a);
            }
        }

        private void LogCompletedActivity(Activity a)
        {
            // record the Activity however desired
        }
    }
}
