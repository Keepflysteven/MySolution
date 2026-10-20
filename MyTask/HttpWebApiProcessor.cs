//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySDK;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace MyTask
{
    public class HttpWebApiProcessor : MyBaseClass
    {
        public HttpWebApiProcessor(string deviceId, int intervalSeconds, ILogger<HttpWebApiProcessor> logger, [FromKeyedServices("SqlLogger")] Serilog.ILogger Sqllogger, IConfiguration configuration) 
            : base(deviceId, intervalSeconds, logger, Sqllogger, configuration)
        {
        }

        protected override Task processData(string data, CancellationToken token)
        {
            Logger.LogWarning($"[HTTP 執行緒] 收到來自基底的資料，開始發送 WebAPI: {data}");

            // 模擬網路請求傳輸
            //await Task.Delay(400, token);
            int i = 0;
            while ( i < 5000000)
            {
                i++;
            
            }
            Logger.LogWarning($"[HTTP 執行緒] 結束");
            return Task.CompletedTask;
        }
    }
}
