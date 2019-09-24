using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ActivitiesExample
{
    public struct ActivityContext
    {
        public ActivityContext(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags flags, ActivityTraceState traceState)
        {
            TraceId = traceId;
            SpanId = spanId;
            Flags = flags;
            TraceState = traceState;
        }
        public ActivityContext(string traceParent, string traceState)
        {
            SpanId = ActivitySpanId.CreateFromString(traceParent.AsSpan(36, 16));
            TraceId = ActivityTraceId.CreateFromString(traceParent.AsSpan(3, 32));
            Flags = (ActivityTraceFlags)HexByteFromChars(traceParent[53], traceParent[54]);
            TraceState = new ActivityTraceState(traceState);
        }
        public ActivityTraceId TraceId { get; private set; }
        public ActivitySpanId SpanId { get; private set; }
        public ActivityTraceFlags Flags { get; private set; }
        public ActivityTraceState TraceState { get; private set; }


        private static byte HexByteFromChars(char char1, char char2)
        {
            return (byte)(HexDigitToBinary(char1) * 16 + HexDigitToBinary(char2));
        }

        private static byte HexDigitToBinary(char c)
        {
            if ('0' <= c && c <= '9')
                return (byte)(c - '0');
            if ('a' <= c && c <= 'f')
                return (byte)(c - ('a' - 10));
            throw new ArgumentOutOfRangeException("idData");
        }
    }
}
