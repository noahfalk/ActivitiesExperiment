using System;
using System.Collections.Generic;
using System.Text;

namespace ActivitiesExample
{
    delegate void ActivityScopeHandler(ref ActivityScope activityScope);

    public class ActivitySource : IDisposable
    {
        ActivitySourceRegistry _registry;
        volatile IActivityListener[] _listeners;

        public ActivitySource(string name) : this(name, ActivitySourceRegistry.DefaultRegistry) { }
        public ActivitySource(string name, ActivitySourceRegistry registry)
        {
            Name = name;
            _registry = registry;
            _registry?.Add(this);
        }

        public string Name { get; }

        public void Dispose()
        {
            _registry?.Remove(this);
            _registry = null;
            _listeners = null;
        }

        public void AddListener(IActivityListener listener)
        {
            lock(this)
            {
                if(_registry == null)
                {
                    return; //already disposed
                }
                List<IActivityListener> newListeners = new List<IActivityListener>();
                if(_listeners != null)
                {
                    newListeners.AddRange(_listeners);
                }
                newListeners.Add(listener);
                _listeners = newListeners.ToArray();
            }
        }

        public void RemoveListener(IActivityListener listener)
        {
            lock (this)
            {
                if (_registry == null)
                {
                    return; //already disposed
                }
                List<IActivityListener> newListeners = new List<IActivityListener>();
                if (_listeners != null)
                {
                    newListeners.AddRange(_listeners);
                }
                newListeners.Remove(listener);
                _listeners = newListeners.ToArray();
            }
        }

        internal void OnScopeStarted(ref ActivityScope activityScope)
        {
            // This array is immutable once assigned to _listeners
            // TODO: If this is in a final implementation we'd want
            // to confirm .NET gives us the memory model guarantees to
            // do this without a lock or an explicit read barrier, but
            // I this it does.
            IActivityListener[] listeners = _listeners;
            if (listeners == null)
                return;
            for(int i = 0; i < listeners.Length; i++)
            {
                listeners[i].ActivityScopeStarted(this, ref activityScope);
            }
        }

        internal void OnScopeStopped(ref ActivityScope activityScope)
        {
            // This array is immutable once assigned to _listeners
            // TODO: If this is in a final implementation we'd want
            // to confirm .NET gives us the memory model guarantees to
            // do this without a lock or an explicit read barrier, but
            // I this it does.
            IActivityListener[] listeners = _listeners;
            if (listeners == null)
                return;
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].ActivityScopeStopped(this, ref activityScope);
            }
        }
    }
}
