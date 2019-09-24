using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ActivitiesExample
{
    public class ActivitySourceRegistry
    {
        public static ActivitySourceRegistry DefaultRegistry = new ActivitySourceRegistry();

        Dictionary<string, ActivitySource> _activitySources = new Dictionary<string, ActivitySource>();

        public void Add(ActivitySource activitySource)
        {
            lock (this)
            {
                _activitySources.Add(activitySource.Name, activitySource);
                ActivitySourceAdded?.Invoke(this, activitySource);
            }
        }

        public void Remove(ActivitySource activitySource)
        {
            lock(this)
            {
                if(_activitySources.Remove(activitySource.Name))
                {
                    ActivitySourceRemoved?.Invoke(this, activitySource);
                }
            }
        }

        public bool TryGetValue(string name, out ActivitySource activitySource)
        {
            lock (this)
            {
                return _activitySources.TryGetValue(name, out activitySource);
            }
        }

        public void ForEach(Action<ActivitySource> sourceFunc)
        {
            lock(this)
            {
                foreach(ActivitySource source in _activitySources.Values)
                {
                    sourceFunc(source);
                }
            }
        }

        public event EventHandler<ActivitySource> ActivitySourceAdded;
        public event EventHandler<ActivitySource> ActivitySourceRemoved;

    }
}
