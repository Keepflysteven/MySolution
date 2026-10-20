using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySDK
{
    public abstract class MyBaseClass : IMyInterface
    {
        protected readonly string _ClassName;
        protected readonly string DeviceId;
        protected readonly ILogger Logger;
        private readonly Serilog.ILogger _Sqllogger;
        private readonly int _intervalSeconds;
        private Task? _runningTask;
        private CancellationTokenSource? _cts;

        // 建構子：接收基本參數與定時頻率
        protected MyBaseClass(string deviceId, int intervalSeconds, ILogger logger ,[FromKeyedServices("SqlLogger")] Serilog.ILogger Sqllogger, IConfiguration configuration)
        {
            _ClassName=this.GetType().Name;
            DeviceId = deviceId;
            _intervalSeconds = intervalSeconds;
            Logger = logger;
            _Sqllogger = Sqllogger;
        }

        // 實作介面：提供外部統一拿取 Task 的管道
        public Task RunningTask => _runningTask ?? Task.CompletedTask;
        //• => (運算式本文屬性)： 這是 C# 的語法糖，當屬性只有 get 且只有一行程式碼時，可以用它來取代大括號與 return。
        //• ?? (空值結合運算子)： 它的邏輯是：「如果左邊不是 null，就用左邊；如果左邊是 null，就用右邊。」

        // 實作介面方法：由 Worker Service 觸發啟動背景執行緒
        public void Start(CancellationToken serviceStoppingToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken);
            _runningTask = ExecuteProcessorAsync(_cts.Token);
        }

        // 💡 基底類別親自實作背景 Task 迴圈
        private async Task ExecuteProcessorAsync(CancellationToken token)
        {
            Logger.LogInformation($"【設備 {DeviceId}】多執行緒長駐任務已啟動。");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. 模擬各設備相同的資料收集/產生行為 (這裡產生一串資料字串)
                    string rawDataString = $"Device={DeviceId};Timestamp={DateTime.Now:yyyyMMddHHmmss};Status=OK";

                    _Sqllogger.Information(rawDataString);
                    Logger.LogInformation($"【設備 {DeviceId}】已採集到原始資料，準備傳出...");

                    // 2.核心關鍵：呼叫由子類別強制實作的 processData 方法，將字串傳出去
                    // 這裡使用 await，代表子類別如果是做非同步處理（如寫資料庫、打 API），基底類別也會同步等待
                    await processData(rawDataString, token);

                    // 3. 依據設定的時間非同步延遲
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    // Windows 服務停止時的正常行為，跳出迴圈                    
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"【設備 {DeviceId}】執行緒處理資料時發生未捕捉異常！");
                    // 視需求決定是否要 await Task.Delay 一小段時間防潰堤，或直接 break 讓 Task 終止
                    await Task.Delay(2000, token);
                }
            }

            Logger.LogInformation($"【設備 {DeviceId}】多執行緒長駐任務已安全退出。");
        }

        // ✨ 強制子類別必須實作的抽象方法
        // 子類別不寫這個方法，編譯器就不會讓程式過。
        protected abstract Task processData(string data, CancellationToken token);       
        //1. 這裡的 Task 相當於非同步世界中的 void
        //在傳統的同步程式碼中，如果一個方法不需要回傳任何資料（如整數、字串），我們會宣告為 void。
        //但是在 async/await 的非同步世界裡：
        //• Task 就代表非同步的 void（只執行動作，不回傳實體資料）。
        //• Task<string> 才代表非同步且需要回傳字串。

    }
}
