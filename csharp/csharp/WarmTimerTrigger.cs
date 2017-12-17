using System;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace csharp
{
    public static class WarmTimerTrigger
    {
        [FunctionName(nameof(WarmTimerTrigger))]
        public static void Run([TimerTrigger("0 */4 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
