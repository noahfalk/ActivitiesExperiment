using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ActivitiesExample.Usage.ClientLibs.AzureSDK
{

    // ConfigurationClient performs an Activity wrapped operation
    /* https://github.com/Azure/azure-sdk-for-net/blob/369ea8ea4498d82d4ca435d14415145cf69fef59/sdk/appconfiguration/Azure.ApplicationModel.Configuration/src/ConfigurationClient.cs#L126 */
    class ConfigurationClient
    {
        HttpPipeline _pipeline;

        public virtual Response<ConfigurationSetting> Add(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("ConfigurationClient.Add");
            scope.AddAttribute("key", setting?.Key);
            scope.Start();

            try
            {
                using Request request = CreateAddRequest(setting);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)

                {
                    case 200:
                    case 201:
                        return CreateResponse(response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        #region Mocked APIs
        Request CreateAddRequest(ConfigurationSetting setting) { return new Request(); }
        Response<ConfigurationSetting> CreateResponse(Response response) { return new Response<ConfigurationSetting>(); }
        #endregion
    }

    public readonly struct DiagnosticScope : IDisposable
    {
        private readonly DiagnosticActivity? _activity;

        private readonly string _name;

        private readonly DiagnosticListener _source;

        internal DiagnosticScope(string name, DiagnosticListener source)
        {
            _name = name;
            _source = source;
            _activity = _source.IsEnabled() ? new DiagnosticActivity(_name) : null;
            _activity?.SetW3CFormat();
        }

        public bool IsEnabled => _activity != null;

        public void AddAttribute(string name, string value)
        {
            _activity?.AddTag(name, value);
        }

        public void AddAttribute<T>(string name, T value)
        {
            if (_activity != null && value != null)
            {
                AddAttribute(name, value.ToString());
            }
        }

        public void AddAttribute<T>(string name, T value, Func<T, string> format)
        {
            if (_activity != null)
            {
                AddAttribute(name, format(value));
            }
        }

        public void AddLink(string id)
        {
            if (_activity != null)
            {
                var linkedActivity = new Activity("LinkedActivity");
                linkedActivity.SetW3CFormat();
                linkedActivity.SetParentId(id);

                _activity.AddLink(linkedActivity);
            }
        }

        public void Start()
        {
            if (_activity != null && _source.IsEnabled(_name))
            {
                _source.StartActivity(_activity, _activity);
            }
        }

        public void Dispose()
        {
            if (_activity == null)
            {
                return;
            }


            if (_source != null)
            {
                _source.StopActivity(_activity, null);
            }
            else
            {
                _activity?.Stop();
            }
        }

        public void Failed(Exception e)
        {
            if (_activity == null)
            {
                return;
            }
            _source?.Write(_activity.OperationName + ".Exception", e);
        }

        private class DiagnosticActivity : Activity
        {
            private List<Activity>? _links;

            public IEnumerable<Activity> Links => (IEnumerable<Activity>?)_links ?? Array.Empty<Activity>();

            public DiagnosticActivity(string operationName) : base(operationName)
            {
            }

            public void AddLink(Activity activity)
            {
                _links ??= new List<Activity>();
                _links.Add(activity);
            }
        }
    }

    /* https://github.com/Azure/azure-sdk-for-net/blob/f15d756d2d96b69510a9e04022edff47b6e9262e/sdk/core/Azure.Core/src/Pipeline/ActivityExtensions.cs#L20 */
    internal static class ActivityExtensions
    {
        private static readonly MethodInfo? s_setIdFormatMethod = typeof(Activity).GetMethod("SetIdFormat");

        public static bool SetW3CFormat(this Activity activity)
        {
            if (s_setIdFormatMethod == null) return false;
            s_setIdFormatMethod.Invoke(activity, new object[] { 2 /* ActivityIdFormat.W3C */});
            return true;

        }
    }

    /* https://github.com/Azure/azure-sdk-for-net/blob/369ea8ea4498d82d4ca435d14415145cf69fef59/sdk/core/Azure.Core/src/Pipeline/HttpPipelineDiagnostics.cs */
    public sealed class ClientDiagnostics
    {
        private readonly bool _isActivityEnabled;

        public ClientDiagnostics(bool isActivityEnabled)
        {
            _isActivityEnabled = isActivityEnabled;
        }

        private static readonly DiagnosticListener s_source = new DiagnosticListener("Azure.Clients");

        public DiagnosticScope CreateScope(string name)
        {
            if (!_isActivityEnabled)
            {
                return default;
            }
            return new DiagnosticScope(name, s_source);
        }
    }

    #region Mocked out types

    public class Response
    {
        public int Status { get; }
        public Exception CreateRequestFailedException() { return new Exception(); }
    }
    public class Response<T> { }
    public class ConfigurationSetting
    {
        public string Key { get; set; }
    }
    public class Request : IDisposable
    {
        public void Dispose() {}
    }

    public class HttpPipeline
    {
        public ClientDiagnostics Diagnostics { get; }
        public Response SendRequest(Request request, CancellationToken cancelToken) { return new Response(); }
    }
    #endregion
}
