using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySDK;

namespace MyTask
{
    public class SqlDatabaseProcessor : MyBaseClass
    {
        public SqlDatabaseProcessor(string deviceId, int intervalSeconds, ILogger<SqlDatabaseProcessor> logger, [FromKeyedServices("SqlLogger")] Serilog.ILogger Sqllogger, IConfiguration configuration)
            : base(deviceId, intervalSeconds, logger, Sqllogger, configuration)
        {
            // 1. 先選定特定的區塊
            var myBlock = configuration.GetSection(_ClassName);

            // 2. 直接讀取該區塊底下的特定值
            string connectionStr = myBlock["connectionStr"];
            int timeout = myBlock.GetValue<int>("TimeoutSeconds");

            Logger.LogInformation($"[SQL 執行緒] SQL Server: 連線字串 {connectionStr}");
        }

        protected override Task processData(string data, CancellationToken token)
        {
            Logger.LogInformation($"[SQL 執行緒] 收到來自基底的資料，開始寫入 SQL Server: {data}");

            // 模擬異步資料庫寫入工作
            // await Task.Delay(200, token);
            int i = 0;
            while (i < 5000000)
            {
                i++;
            }
            Logger.LogWarning($"[SQL 執行緒] 結束");
            return Task.CompletedTask;
        }
    }
}
