using MyHub.Model;
using MySDK;
using MyTask;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace MyHub
{
    public class Hub : BackgroundService
    {
        private readonly ILogger<Hub> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        // 改用介面型別的 List 來管理動態物件
        private readonly List<IMyInterface> _activeProcessors = new();

        public Hub(ILogger<Hub> logger , ILoggerFactory LoggerFactory,IConfiguration configuration, IServiceProvider serviceProvider)
        {
            #region print configuration content
            //if (configuration == null)
            //{
            //    throw new ArgumentNullException(nameof(configuration), "系統的 IConfiguration 本體居然是 null！");
            //}

            //// 印出有沒有抓到任何 Key，如果 Count 是 0，代表 JSON 根本沒載入進來
            //var keyCount = configuration.AsEnumerable().Count();
            //File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "config_check.txt"), $"總共載入了 {keyCount} 個設定項目");

            //// 💡 取得所有 39 個項目的 Key 名稱
            //List<string> keys = configuration.AsEnumerable()
            //                                 .Select(pair => pair.Key)
            //                                 .ToList();

            //// 您可以把 keys 序列化成 JSON 或是直接用 string.Join 輸出看結果
            //string allKeysText = string.Join(", ", keys);

            //File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "config_check_DefaultConnection.txt"), $"All Key  {allKeysText}");
            #endregion


            _logger = logger;
            _loggerFactory = LoggerFactory;
            _configuration = configuration;
            _serviceProvider = serviceProvider;           
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("主服務啟動，開始透過介面動態長出物件...");
            #region 產生物件

            var str = _configuration.GetConnectionString("DefaultConnection");
            _logger.LogInformation("讀取設定檔 DefaultConnection {str}", str);

            //動態產生
            // 1. 讀取並轉換成 List<ProcessorModel> 陣列
            // 如果 JSON 內找不到該區塊，預設給一個空的 List 避免 null 報錯
            var ProcessorList = _configuration.GetSection("ProcessorList").Get<List<ProcessorModel>>()?? new List<ProcessorModel>();
            _logger.LogInformation("讀取設定檔 找到{count}",ProcessorList.Count);
            // 2. 使用 foreach 迴圈巡覽陣列
            foreach (var P in ProcessorList)
            {
                _logger.LogInformation("動態載入Processor ,型別: {ClassName}, 路徑: {Path}, 啟用狀態: {IsEnabled}", P.ClassName , P.Path, P.IsEnabled);

                try
                {
                    var Processor = LoadPlugin(P.Path, P.ClassName, P.ClassName, 5);
                    Processor.Start(stoppingToken);
                    _activeProcessors.Add(Processor);
                    _logger.LogInformation("動態載入Processor, 型別: { ClassName}" , P.ClassName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"載入外掛{P.ClassName}時發生錯誤");
                }
            }

            #endregion

            // 彙整所有介面物件內部的 RunningTask
            var allTasks = _activeProcessors.Select(p => p.RunningTask).ToArray();

            // 卡在這裡等待，直到 Windows 服務停止
            await Task.WhenAll(allTasks);
            _logger.LogInformation("所有介面物件的執行緒皆已結束。");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Windows 服務已完全停止。");
            await base.StopAsync(cancellationToken);
        }

        // 🌟 新增 targetClassName 參數
        private IMyInterface LoadPlugin(string pluginPath, string targetClassName, string deviceId, int intervalSeconds )
        {
            // 手動撈出 SqlLogger ,讓衍生類別都可以使用
            //var sqlLogger = _serviceProvider.GetKeyedService<Serilog.ILogger>("SqlLogger");


            #region 方法一
            // 1. 先用資料流把檔案讀成 byte[] 陣列
            byte[] assemblyBytes = File.ReadAllBytes(pluginPath);

            var loadContext = new AssemblyLoadContext("PluginContext", isCollectible: false);

            // 1.1. 改用傳入 Stream / Byte 陣列的方式載入
            using (var ms = new MemoryStream(assemblyBytes))
            {
                //這樣 .NET 是從「記憶體」載入組件，實體 DLL 檔案在讀完 ReadAllBytes 後就立刻解鎖了！
                Assembly assembly = loadContext.LoadFromStream(ms);

                // 2. 直接精準取出，不需要 GetTypes() 也不需要寫一堆條件
                Type? pluginType = assembly.GetType(targetClassName);

                // 3.1 進行條件篩選與檢查 (Filter & Guard)
                if (pluginType == null)
                {
                    throw new Exception($"在 DLL 中找不到此型別：{targetClassName}");
                }

                // 3.2 核心篩選條件：驗證此類別是否「實作了 IMyInterface」
                if (!typeof(IMyInterface).IsAssignableFrom(pluginType))
                {
                    throw new Exception($"型別 {targetClassName} 錯誤：它並沒有實作 {nameof(IMyInterface)} 介面！");
                }

                // 3.3 進階防禦篩選：確保它不是介面也不是抽象類別
                if (pluginType.IsInterface || pluginType.IsAbstract)
                {
                    throw new Exception($"型別 {targetClassName} 錯誤：它是一個介面或抽象類別，無法被實例化！");
                }

                // 利用 ActivatorUtilities 自動注入 ILogger/IConfiguration 並帶入自訂參數
                var instance = ActivatorUtilities.CreateInstance(
                    _serviceProvider,
                    pluginType,
                    deviceId,
                    intervalSeconds
                //intervalSeconds,
                //sqlLogger
                ) as IMyInterface;

                return instance ?? throw new Exception($"無法建立外掛 '{targetClassName}' 的實例。");
            }


            #endregion


            #region 方法二
            //var loadContext = new AssemblyLoadContext("PluginContext", isCollectible: false);

            //// 1. 假設這是您動態載入進來的 Assembly 物件
            //Assembly assembly = loadContext.LoadFromAssemblyPath(pluginPath);

            //// 2. 直接精準取出，不需要 GetTypes() 也不需要寫一堆條件
            //Type? pluginType = assembly.GetType(targetClassName);

            //// 3.1 進行條件篩選與檢查 (Filter & Guard)
            //if (pluginType == null)
            //{
            //    throw new Exception($"在 DLL 中找不到此型別：{targetClassName}");
            //}

            //// 3.2 核心篩選條件：驗證此類別是否「實作了 IMyInterface」
            //if (!typeof(IMyInterface).IsAssignableFrom(pluginType))
            //{
            //    throw new Exception($"型別 {targetClassName} 錯誤：它並沒有實作 {nameof(IMyInterface)} 介面！");
            //}

            //// 3.3 進階防禦篩選：確保它不是介面也不是抽象類別
            //if (pluginType.IsInterface || pluginType.IsAbstract)
            //{
            //    throw new Exception($"型別 {targetClassName} 錯誤：它是一個介面或抽象類別，無法被實例化！");
            //}

            //// 利用 ActivatorUtilities 自動注入 ILogger/IConfiguration 並帶入自訂參數
            //var instance = ActivatorUtilities.CreateInstance(
            //    _serviceProvider,
            //    pluginType,
            //    deviceId,
            //    intervalSeconds
            //) as IMyInterface;

            //return instance ?? throw new Exception($"無法建立外掛 '{targetClassName}' 的實例。");

            #endregion
        }
    }
}
