using ActivitiesExample.PerfTests;
using ActivitiesExample.Usage.OTel;
using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;

namespace ActivitiesExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<FilterPerf>();


            //SimplePerfTest();
        }

        static void SimplePerfTest()
        {
            ActivityListener l = new ActivityListener(NameFilter, SampleFilter);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 10_000_000; i++)
            {
                FourActivityWorkItem();
            }
            sw.Stop();
            Console.WriteLine("Elapsed: " + sw.Elapsed);
        }

        static bool NameFilter(string name) => true;
        static bool SampleFilter(ref ActivityScope scope)
        {
            if (scope.TraceId.ToString().EndsWith("00", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        static string incomingActivityId = "00-12345678901234567890123456789012-1234567890123456-00";
        static ActivitySource _httpIn = new ActivitySource("HttpServer.HttpIn");
        static ActivitySource _fooBar = new ActivitySource("Azure.Sdk.FooBar.Internal");
        static ActivitySource _clientRequest1 = new ActivitySource("Azure.Sdk.ClientReq1");
        static ActivitySource _clientRequest2 = new ActivitySource("Azure.Sdk.ClientReq2");

        static void FourActivityWorkItem()
        {
            using (var scope = new ActivityScope(_httpIn, ParseActivityTraceId, incomingActivityId))
            {
                scope.AddTag("route", "/foo/bar");
                scope.AddTag("usedid", "1239");
                using (var scope2 = new ActivityScope(_fooBar))
                {
                    scope2.AddTag("one", "1");
                    scope2.AddTag("two", "2");
                    using (var scope3 = new ActivityScope(_clientRequest1))
                    {
                        scope3.AddTag("one", "1");
                        scope3.AddTag("two", "2");
                    }
                    using (var scope4 = new ActivityScope(_clientRequest2))
                    {
                        scope4.AddTag("one", "1");
                        scope4.AddTag("two", "2");
                    }
                }
            }
        }


        static void ParseActivityTraceId(object param, ref ActivityContext parsedId)
        {
            string id = (string)param;
            parsedId = new ActivityContext(id, null);
        }

        static byte HexByteFromChars(char char1, char char2)
        {
            return (byte)(HexDigitToBinary(char1) * 16 + HexDigitToBinary(char2));
        }

        static byte HexDigitToBinary(char c)
        {
            if ('0' <= c && c <= '9')
                return (byte)(c - '0');
            if ('a' <= c && c <= 'f')
                return (byte)(c - ('a' - 10));
            throw new ArgumentOutOfRangeException("idData");

        }
    }
}
