using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using ActivitiesExample.Usage.OTel;
using System.Collections.Concurrent;

namespace ActivitiesExample.PerfTests
{
    [RPlotExporter, RankColumn]
    [MemoryDiagnoser]
    [DisassemblyDiagnoser]
    //[EtwProfiler]
    public class FilterPerf
    {
        static DiagnosticListener s_listener = new DiagnosticListener("PerfFilterListener");

        ActivityListener _listener;
        DiagnosticListenerCollector _baselineCollector;

        public enum FilterKind
        {
            FilterOutByListener, // statically determine which listeners to listen to. New scheme doesn't use DiagnosticListener so this the same as filter by name.
            FilterOutByName,     // statically determine which activity names to listen to.
            SampleNever,         // succeed eager listener+name checks, then eliminate everything at the sampling stage
            SampleOutByName,     // succeed eager listener+name checks, then filter out activities in the OT sampler using activity name
            SampleOutByTraceId,  // succeed eager listener+name checks, then filter out activities in the OR sampler by trace id. This is typically probabalistic but the perf test
                                 // always measures the cost of the sampled out result.
            SampleAlways         // all checks pass, activity is sampled in. OpenTelemetry allows yet more filtering later on but at minimum the Span must be created, tracked, and
                                 // logged through the export API.
        }

        [Params(FilterKind.FilterOutByListener,
                FilterKind.FilterOutByName,
                FilterKind.SampleNever,
                FilterKind.SampleOutByName,
                FilterKind.SampleOutByTraceId,
                FilterKind.SampleAlways)]
        public FilterKind Filter;

        [GlobalSetup]
        public void Setup()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
            if (Filter == FilterKind.FilterOutByListener)
            {
                _listener = new ActivityListener(NameFilterFalse, SampleFilterFalse);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterFalse, NameFilterFalse, ListenerSamplerFalse);
            }
            else if (Filter == FilterKind.FilterOutByName)
            {
                _listener = new ActivityListener(NameFilterFalse, SampleFilterFalse);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterTrue, NameFilterFalse, ListenerSamplerFalse);
            }
            else if (Filter == FilterKind.SampleNever)
            {
                _listener = new ActivityListener(NameFilterTrue, SampleFilterFalse);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterTrue, NameFilterTrue, ListenerSamplerFalse);
            }
            else if (Filter == FilterKind.SampleOutByName)
            {
                _listener = new ActivityListener(NameFilterTrue, SampleFilterByName);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterTrue, NameFilterTrue, ListenerSamplerByName);
            }
            else if (Filter == FilterKind.SampleOutByTraceId)
            {
                _listener = new ActivityListener(NameFilterTrue, SampleFilterTraceId);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterTrue, NameFilterTrue, ListenerSamplerByTraceId);
            }
            else if (Filter == FilterKind.SampleAlways)
            {
                _listener = new ActivityListener(NameFilterTrue, SampleFilterTrue);
                _baselineCollector = new DiagnosticListenerCollector(ListenerFilterTrue, NameFilterTrue, ListenerSamplerTrue);
            }
        }


        static bool ListenerFilterFalse(string name) => false;
        static bool ListenerFilterTrue(string name) => name == "PerfFilterListener"; // this is our listener, it returns true
        static bool NameFilterFalse(string name) => false;
        static bool NameFilterTrue(string name) => true;
        static bool SampleFilterFalse(ref ActivityScope scope) => false;
        static bool SampleFilterByName(ref ActivityScope scope) => scope.ActivityName == "NonExistantName"; // never happens, forces ActivityName to be evaluated
        static bool SampleFilterTraceId(ref ActivityScope scope) => scope.TraceId == null; // never happens, forces TraceId to be evaluated
        static bool SampleFilterTrue(ref ActivityScope scope) => true;

        // We are biasing to make the baseline faster, all the samplers just compute the right answer without actually checking anything. If they did
        // have to interpret the incoming Activity/event name/opaque args that would only add time.
        static bool ListenerSamplerFalse(object obj) => false;
        static bool ListenerSamplerByName(object obj) => false;
        static bool ListenerSamplerByTraceId(object obj) => false;
        static bool ListenerSamplerTrue(object obj) => true;


        static string incomingActivityId = "00-12345678901234567890123456789012-1234567890123456-00";
        static ActivitySource s_httpIn = new ActivitySource("HttpServer.HttpIn");
        static ActivitySource s_fooBar = new ActivitySource("Azure.Sdk.FooBar.Internal");
        static ActivitySource s_clientRequest1 = new ActivitySource("Azure.Sdk.ClientReq1");
        static ActivitySource s_clientRequest2 = new ActivitySource("Azure.Sdk.ClientReq2");

        
        [Benchmark]
        public void OneActivityWorkItem()
        {
            using (var scope = new ActivityScope(s_httpIn))
            {
            }
        }
        
        
        [Benchmark]
        public void OneActivityWorkItemWithTags()
        {
            using (var scope = new ActivityScope(s_httpIn))
            {
                scope.AddTag("route", "/foo/bar");
                scope.AddTag("usedid", "1239");
            }
        }

        /*
        [Benchmark]
        public void FourActivityWorkItemWithTags()
        {
            using (var scope = new ActivityScope(s_httpIn))
            {
                scope.AddTag("route", "/foo/bar");
                scope.AddTag("usedid", "1239");
                using (var scope2 = new ActivityScope(s_fooBar))
                {
                    scope2.AddTag("one", "1");
                    scope2.AddTag("two", "2");
                    using (var scope3 = new ActivityScope(s_clientRequest1))
                    {
                        scope3.AddTag("one", "1");
                        scope3.AddTag("two", "2");
                    }
                    using (var scope4 = new ActivityScope(s_clientRequest2))
                    {
                        scope4.AddTag("one", "1");
                        scope4.AddTag("two", "2");
                    }
                }
            }
        }
        */

        [Benchmark]
        public void OneActivityWorkItemWithExternalParent()
        {
            ActivityContext parentContext = new ActivityContext();
            ParseActivityTraceId(incomingActivityId, ref parentContext);
            using (var scope = new ActivityScope(s_httpIn, parentContext))
            {
            }
        }

        [Benchmark]
        public void OneActivityWorkItemWithExternalParentLazy()
        {
            using (var scope = new ActivityScope(s_httpIn, ParseFunc, incomingActivityId))
            {
            }
        }

        static GetActivityContext ParseFunc = ParseActivityTraceId;

        static void ParseActivityTraceId(object param, ref ActivityContext parsedId)
        {
            string id = (string)param;
            parsedId = new ActivityContext(id, null);
        }

        [Benchmark]
        public void BaselineActivityWithDiagListener()
        {
            if(s_listener.IsEnabled())
            {
                if(s_listener.IsEnabled("MyActivity"))
                {
                    Activity a = new Activity("MyActivity");
                    s_listener.StartActivity(a, null);
                    s_listener.StopActivity(a, null);
                }
            }
        }
    }


    public class DiagnosticListenerCollector : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>
    {
        Func<string, bool> _activityNameFilter;
        Func<string, bool> _listenerNameFilter;
        Func<object, bool> _shouldSample;

        // this isn't actually thread-safe, I am just making an educated guess that there is an optimized
        // thread-safe implementation where the performance looks more like Dictionary than ConcurrentDictionary
        Dictionary<string, bool> _activityNameFilterCache = new Dictionary<string, bool>();

        public DiagnosticListenerCollector(Func<string,bool> listenerFilter, Func<string, bool> activityNameFilter, Func<object, bool> shouldSample)
        {
            _activityNameFilter = activityNameFilter;
            _listenerNameFilter = listenerFilter;
            _shouldSample = shouldSample;
            DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if (_listenerNameFilter(value.Name))
            {
                value.Subscribe(this, IsEnabled);
            }
        }

        private bool IsEnabled(string name)
        {
            if (!_activityNameFilterCache.TryGetValue(name, out bool val))
            {
                val = _activityNameFilterCache[name] = _activityNameFilter(name);
            }
            return val;
        }

        public void OnNext(KeyValuePair<string, object> startStopEvent)
        {
            if(EndsWithStart(startStopEvent.Key))
            {
                if(_shouldSample(startStopEvent.Value))
                {
                    // I think the original design was that this marking would occur in
                    // OnActivityImport, but that isn't where OT appears to be doing it
                    Activity.Current.ActivityTraceFlags = ActivityTraceFlags.Recorded;
                }
            }
            if (EndsWithStop(startStopEvent.Key))
            {
                Activity a = Activity.Current;
                LogCompletedActivity(a);
            }
        }

        private void LogCompletedActivity(Activity a)
        {
            // record the Activity however desired
        }

        // These Start/End tests are faster than just calling .EndsWith("Start")
        bool EndsWithStart(string val)
        {
            int l = val.Length;
            return l >= 5 &&
                val[--l] == 't' &&
                val[--l] == 'r' &&
                val[--l] == 'a' &&
                val[--l] == 't' &&
                val[--l] == 'S';
        }

        bool EndsWithStop(string val)
        {
            int l = val.Length;
            return l >= 4 &&
                val[--l] == 'p' &&
                val[--l] == 'o' &&
                val[--l] == 't' &&
                val[--l] == 'S';
        }
    }
}
