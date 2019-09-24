using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ActivitiesExample.Usage.ClientLibs.AzureSDK.Proposed
{
    class ConfigurationClient
    {
        static ActivitySource s_configClientAdd = new ActivitySource("Azure.Clients.ConfigurationClient.Add");

        HttpPipeline _pipeline;

        public virtual Response<ConfigurationSetting> Add(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using ActivityScope scope = new ActivityScope(s_configClientAdd);
            scope.AddTag("key", setting?.Key);

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
                scope.SetStatus(e);
                throw;
            }
        }

        #region Mocked APIs
        Request CreateAddRequest(ConfigurationSetting setting) { return new Request(); }
        Response<ConfigurationSetting> CreateResponse(Response response) { return new Response<ConfigurationSetting>(); }
        #endregion
    }


    // It appeared that back-compat with pre-existing listeners was not a requirement but in case it was you could also do... 
    // static ActivitySource s_configClientAdd = new ActivitySource("Azure.Clients.ConfigurationClient.Add").
    //                                           SubscribeAzureClientForwarder("ConfigurationClient.Add");
    static class ActivitySourceListenerCompat
    {
        internal static DiagnosticListener s_azureClientListener = new DiagnosticListener("Azure.Clients");

        static void SubscribeAzureClientForwarder(this ActivitySource source, string originalActivityName)
        {
            source.AddListener(new AzureClientForwarder(originalActivityName));
        }
    }

    class AzureClientForwarder : IActivityListener
    {
        string _originalActivityName;
        string _startEventName;
        string _stopEventName;

        public AzureClientForwarder(string originalActivityName)
        {
            _originalActivityName = originalActivityName;
            _startEventName = _originalActivityName + ".Start";
            _stopEventName = _originalActivityName + ".Stop";
        }

        public void ActivityScopeStarted(ActivitySource source, ref ActivityScope scope)
        {
            if(ActivitySourceListenerCompat.s_azureClientListener.IsEnabled(_originalActivityName))
            {
                Activity a = scope.EnsureActivityCreated();
                ActivitySourceListenerCompat.s_azureClientListener.Write(_startEventName, a);
            }
        }

        public void ActivityScopeStopped(ActivitySource source, ref ActivityScope scope)
        {
            if (scope.Activity != null)
            {
                ActivitySourceListenerCompat.s_azureClientListener.Write(_stopEventName, scope.Activity);
            }
        }
    }
}
