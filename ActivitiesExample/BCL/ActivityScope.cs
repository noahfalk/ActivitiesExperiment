using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ActivitiesExample
{

    public delegate void GetActivityContext(object param, ref ActivityContext ctx);

    public struct ActivityScope : IDisposable
    {
        static Random s_random = new Random();


        Activity _activity;
        Activity _parentActivity;
        bool _parentActivityInited;
        ActivitySource _source;
        ActivityContext _parentContext;
        bool _hasParentContext;
        GetActivityContext _lazyFetchParent;
        object _lazyFetchParentParam;
        ActivityTraceId? _traceId;
        ActivitySpanId? _spanId;

        public ActivityScope(ActivitySource source)
        {
            _activity = null;
            _parentActivity = null;
            _parentActivityInited = false;
            _source = source;
            _parentContext = default;
            _hasParentContext = false;
            _lazyFetchParent = default;
            _lazyFetchParentParam = default;
            _traceId = default;
            _spanId = default;
            Start();
        }

        public ActivityScope(ActivitySource source, ActivityContext parentContext)
        {
            _activity = null;
            _parentActivity = null;
            _parentActivityInited = false;
            _source = source;
            _parentContext = parentContext;
            _hasParentContext = true;
            _lazyFetchParent = default;
            _lazyFetchParentParam = default;
            _traceId = default;
            _spanId = default;
            Start();
        }

        public ActivityScope(ActivitySource source, GetActivityContext lazyParentContext, object lazyGetParentContextParam)
        {
            _activity = null;
            _parentActivity = null;
            _parentActivityInited = false;
            _source = source;
            _parentContext = default;
            _hasParentContext = false;
            _lazyFetchParent = lazyParentContext;
            _lazyFetchParentParam = lazyGetParentContextParam;
            _traceId = default;
            _spanId = default;
            Start();
        }

        internal void Start()
        {
            _source.OnScopeStarted(ref this);
        }

        public Activity EnsureActivityCreated()
        {
            if(_activity == null)
            {
                _activity = new Activity(_source.Name);
                _activity.Start();
            }
            return _activity;
        }

        public Activity Activity
        {
            get => _activity;
        }

        public string ActivityName
        {
            get => _source.Name;
        }

        internal Activity Parent
        {
            get
            {
                if(!_parentActivityInited)
                {
                    _parentActivity = Activity.Current;
                }
                return _parentActivity;
            }
        }

        void GetParentContextFromParentActivity(ref ActivityContext ctx)
        {
            Activity parent = Parent;
            if (parent == null)
            {
                return;
            }
            else
            {
                ctx = new ActivityContext(parent.TraceId, parent.SpanId, parent.ActivityTraceFlags, new ActivityTraceState(parent.TraceStateString));
            }
        }

        public ActivityContext ParentActivityContext
        {
            get
            {
                if(!_hasParentContext)
                {
                    if (_lazyFetchParent != null)
                    {
                        _lazyFetchParent(_lazyFetchParentParam, ref _parentContext);
                    }
                    else
                    {
                        GetParentContextFromParentActivity(ref _parentContext);
                    }
                    _hasParentContext = true;
                }
                return _parentContext;
            }
        }

        private struct TraceIdLongs
        {
            public long long1;
            public long long2;
        }

        public ActivityTraceId TraceId
        {
            get
            {
                if(!_traceId.HasValue)
                {
                    if (_hasParentContext || _lazyFetchParent != null || Parent != null)
                    {
                        _traceId = ParentActivityContext.TraceId;
                    }
                    else
                    {
                        TraceIdLongs longs = new TraceIdLongs();
                        longs.long1 = ((long)s_random.Next()) << 32;
                        longs.long1 |= s_random.Next();
                        longs.long2 = ((long)s_random.Next()) << 32;
                        longs.long2 |= s_random.Next();
                        unsafe
                        {
                            _traceId = ActivityTraceId.CreateFromBytes(new ReadOnlySpan<byte>((void*)&longs, 16));
                        }
                    }
                }
                return _traceId.Value;
            }
        }

        public ActivitySpanId SpanId
        {
            get
            {
                if (!_spanId.HasValue)
                {
                    if (_hasParentContext || _lazyFetchParent != null || Parent != null)
                    {
                        _spanId = ParentActivityContext.SpanId;
                    }
                    else
                    {
                        long randId = ((long)s_random.Next()) << 32;
                        randId |= s_random.Next();
                        unsafe
                        {
                            _spanId = ActivitySpanId.CreateFromBytes(new ReadOnlySpan<byte>((void*)&randId, 8));
                        }
                    }
                }
                return _spanId.Value;
            }
        }

        public void AddTag(string key, string value)
        {
            _activity?.AddTag(key, value);
        }

        public void SetStatus(Exception e)
        {
            //_activity?.SetStatus(e);
        }

        public void Dispose()
        {
            if (_activity != null)
            {
                if (_activity.Duration == TimeSpan.Zero)
                    _activity.SetEndTime(DateTime.UtcNow);
                
                _activity.Stop();
            }
            _source.OnScopeStopped(ref this);
        }
    }
}
