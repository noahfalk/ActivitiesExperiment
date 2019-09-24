# ActivitiesExperiment
Exploring potential changes to System.Diagnostics.Activity

I've been playing around with some potential APIs that could help us improve Activity. The goal of this repo is to get feedback and provide
a simple place to play around. For reference here is Pavel's very nice original write-up and set of goals:
https://gist.github.com/pakrym/649bc2d9af5a16cac03f0b8fb5666dce

```
    Requirements:
    1. Easier to use then current activity
    2. Equal or better performance to current way of creating activities
    3. Ability to support subscription using wildcards
    4. Backwards compatibility with existing activities
    5. Using diagnostic source as subscription mechanism
    6. Cheap and granular IsEnabled check
```

I want to poke a bit at goals (5) and (6):
- For (6) I'd propose in the new world of OpenTelemetry there isn't a singular IsEnabled check but rather a variety of points at which 
Activities need to be filtered/sampled out. Given the desire to have Activities on-by-default and require minimal end-user configuration
that means most activities will only be eliminated by a probabilistic sampling algorithm and that algorithm needs access to the TraceId and
parent trace flags to make decisions at minimum. Black-lists hard-coded into the OTel library or provided by users remain possible, but
they aren't the primary way we expect to filter out Activities. I am expecting the cost of sampled-out activities to dominate the user
perception of the telemetry overhead.
- For (5) I'm not sure what aspect of the current diagnostic source API was deemed important. In order to address perf goals in (2) and the
revised (6) something needs to change in the way that potential Activity producers interact with the sampler in OTel. Even if we preserve
the API and only change the convention about what names/objects get passed as Activities that would still require changes in both producer
and consumer code in order to get any benefits. I think we are pre-supposing that consumers and producers are willing to change (within
limits) so if there is any specific aspect of the DiagnosticSource that we think is off-limits we should clarify what that is.


I also think there are some additional goals we've discussed but aren't written there:

7. OpenTelemetry needs to be able to determine a set of Activities that library authors intend to be on-by-default
8. Activities need to be more expressive so that library authors can represent OTel telemetry concepts without creating direct dependencies
on the OTel assembly.

The current idea I am exploring has a few key parts:
1. We try to keep usage nicer by using an IDisposable structure that represents the scope where an Activity might be logged from:
```
    using(var scope = new ActivityScope(...))
    {
        // do some work
    }
```
    
2. I am using a new ActivitySource and IActivityListener to convey notifications from Activity producers to consumers, NOT DiagnosticListener.
Although I hesitated to cross this bridge, it comes with a variety of benefits:
- We can be explicit about the eventing contract without relying on all parties to understand and correctly implement conventions.
- It eliminates the two tiered naming scheme of listener + activity name. From a user perspective activities have a single name and
the listener name is purely an implementation detail that adds complexity for little benefit.
- DiagnosticListener APIs force notifications using string/object heavy patterns. String matching has inherent perf overheads and passing
objects requires allocations or thread-static caches.
- Modifying the diagnostic listener API in other ways is a bit awkward because the existing pattern is that the subscriber provides delegates
to back each method.

```
    static ActivitySource s_activitySource = new ActivitySource("Azure.Clients.SomeOperation");
    using(var scope = new ActivityScope(s_activitySource))
    {
        // do some work
    }
```

On the listener side you get these callbacks for each ActivitySource you want to listen to:
```
    public interface IActivityListener
    {
        void ActivityScopeStarted(ActivitySource source, ref ActivityScope scope);
        void ActivityScopeStopped(ActivitySource source, ref ActivityScope scope);
    }
```

It doesn't take too much code to author an IActivityListener that can forward to a DiagnosticListener if you want to preserve compat
with existing tools.

3. Together the ActivityScope and ActivitySource go to some lengths to make Activity creation lazy. In particular OTel can
implement a sampling API that can access the data it needs to sample out an Activity without ever creating/starting/stopping it. For our
hypothetical high speed web-service each request completes in 70us and with aggresive sampling developers will expect that the cost of
the telemetry trends towards zero. If we allow ourselves 1% overhead that gives us only 700ns total to propagate the trace context and
sample out every encountered activity. Even with an unrealistically efficient sampling algorithm and optimized listener the baseline
DiagnosticListener case spends the entire 700ns performance budget sampling out a single activity. This represents the theoretical limit
of what OTel could do assuming it was optimized considerably from its current state.


BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.737 (1809/October2018Update/Redstone5)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET Core SDK=3.0.100-preview7-012821
  [Host]     : .NET Core 3.0.0-preview7-27912-14 (CoreCLR 5.0.19.37801, CoreFX 4.700.19.36209), 64bit RyuJIT
  DefaultJob : .NET Core 3.0.0-preview7-27912-14 (CoreCLR 5.0.19.37801, CoreFX 4.700.19.36209), 64bit RyuJIT


```
|                                    Method |              Filter |        Mean |     Error |    StdDev | Rank |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------------------------------ |-------------------- |------------:|----------:|----------:|-----:|-------:|------:|------:|----------:|
|                       **OneActivityWorkItem** | **FilterOutByListener** |  **12.8056 ns** | **0.0878 ns** | **0.0821 ns** |    **2** |      **-** |     **-** |     **-** |         **-** |
|               OneActivityWorkItemWithTags | FilterOutByListener |  12.9812 ns | 0.0658 ns | 0.0616 ns |    2 |      - |     - |     - |         - |
|     OneActivityWorkItemWithExternalParent | FilterOutByListener |  85.9226 ns | 0.5903 ns | 0.5521 ns |   11 | 0.0229 |     - |     - |     144 B |
| OneActivityWorkItemWithExternalParentLazy | FilterOutByListener |  14.6205 ns | 0.0653 ns | 0.0545 ns |    5 |      - |     - |     - |         - |
|          BaselineActivityWithDiagListener | FilterOutByListener |   0.2602 ns | 0.0083 ns | 0.0078 ns |    1 |      - |     - |     - |         - |
|                       **OneActivityWorkItem** |     **FilterOutByName** |  **12.8007 ns** | **0.1589 ns** | **0.1486 ns** |    **2** |      **-** |     **-** |     **-** |         **-** |
|               OneActivityWorkItemWithTags |     FilterOutByName |  13.6259 ns | 0.1092 ns | 0.1022 ns |    3 |      - |     - |     - |         - |
|     OneActivityWorkItemWithExternalParent |     FilterOutByName |  83.3052 ns | 0.5402 ns | 0.5053 ns |   10 | 0.0229 |     - |     - |     144 B |
| OneActivityWorkItemWithExternalParentLazy |     FilterOutByName |  14.3758 ns | 0.0790 ns | 0.0739 ns |    4 |      - |     - |     - |         - |
|          BaselineActivityWithDiagListener |     FilterOutByName |  16.7045 ns | 0.3436 ns | 0.3677 ns |    6 |      - |     - |     - |         - |
|                       **OneActivityWorkItem** |         **SampleNever** |  **17.5177 ns** | **0.1467 ns** | **0.1373 ns** |    **7** |      **-** |     **-** |     **-** |         **-** |
|               OneActivityWorkItemWithTags |         SampleNever |  18.7483 ns | 0.1054 ns | 0.0986 ns |    8 |      - |     - |     - |         - |
|     OneActivityWorkItemWithExternalParent |         SampleNever |  89.4614 ns | 0.6920 ns | 0.6135 ns |   12 | 0.0229 |     - |     - |     144 B |
| OneActivityWorkItemWithExternalParentLazy |         SampleNever |  18.8722 ns | 0.1136 ns | 0.1063 ns |    8 |      - |     - |     - |         - |
|          BaselineActivityWithDiagListener |         SampleNever | 693.9540 ns | 3.4651 ns | 3.2413 ns |   20 | 0.1106 |     - |     - |     696 B |
|                       **OneActivityWorkItem** |     **SampleOutByName** |  **19.0252 ns** | **0.1231 ns** | **0.1152 ns** |    **8** |      **-** |     **-** |     **-** |         **-** |
|               OneActivityWorkItemWithTags |     SampleOutByName |  19.0383 ns | 0.0969 ns | 0.0906 ns |    8 |      - |     - |     - |         - |
|     OneActivityWorkItemWithExternalParent |     SampleOutByName |  91.1077 ns | 0.8599 ns | 0.8043 ns |   13 | 0.0229 |     - |     - |     144 B |
| OneActivityWorkItemWithExternalParentLazy |     SampleOutByName |  19.7931 ns | 0.0666 ns | 0.0623 ns |    9 |      - |     - |     - |         - |
|          BaselineActivityWithDiagListener |     SampleOutByName | 694.0701 ns | 4.2966 ns | 4.0190 ns |   20 | 0.1106 |     - |     - |     696 B |
|                       **OneActivityWorkItem** |  **SampleOutByTraceId** | **168.9891 ns** | **2.5074 ns** | **2.3454 ns** |   **16** | **0.0138** |     **-** |     **-** |      **88 B** |
|               OneActivityWorkItemWithTags |  SampleOutByTraceId | 168.4964 ns | 1.7735 ns | 1.6590 ns |   16 | 0.0138 |     - |     - |      88 B |
|     OneActivityWorkItemWithExternalParent |  SampleOutByTraceId |  99.8790 ns | 0.8378 ns | 0.7427 ns |   14 | 0.0229 |     - |     - |     144 B |
| OneActivityWorkItemWithExternalParentLazy |  SampleOutByTraceId | 107.5449 ns | 0.7605 ns | 0.7114 ns |   15 | 0.0229 |     - |     - |     144 B |
|          BaselineActivityWithDiagListener |  SampleOutByTraceId | 699.6704 ns | 6.9102 ns | 6.4638 ns |   20 | 0.1106 |     - |     - |     696 B |
|                       **OneActivityWorkItem** |        **SampleAlways** | **635.0006 ns** | **3.6582 ns** | **3.4219 ns** |   **17** | **0.0925** |     **-** |     **-** |     **584 B** |
|               OneActivityWorkItemWithTags |        SampleAlways | 673.7080 ns | 4.4399 ns | 4.1531 ns |   19 | 0.1049 |     - |     - |     664 B |
|     OneActivityWorkItemWithExternalParent |        SampleAlways | 715.9785 ns | 5.2032 ns | 4.8671 ns |   21 | 0.1154 |     - |     - |     728 B |
| OneActivityWorkItemWithExternalParentLazy |        SampleAlways | 645.7555 ns | 1.9421 ns | 1.7216 ns |   18 | 0.0925 |     - |     - |     584 B |
|          BaselineActivityWithDiagListener |        SampleAlways | 705.4108 ns | 5.0746 ns | 4.4985 ns |   20 | 0.1106 |     - |     - |     696 B |
```

There are of course other ways to get similar speed ups without this particular design. The primary condition is that the implementation
can not call Activity.Start() before the sampling decision is made. It is preferable not to allocate the Activity object at all, but
that appears less substantial than the cost of Start(). The current implementation in this repo does it by using the ActivityScope to
precompute and store all the sampling info on-demand and then passing the struct by ref to listeners that will decide if an Activity
needs to be started.


