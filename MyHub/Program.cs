using Microsoft.Win32;
using MyHub;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

#region 初級寫法
//var builder = Host.CreateApplicationBuilder(args);

//// 1. 關鍵設定：加入 Windows Service 支援，並指定服務名稱
//builder.Services.AddWindowsService(options =>
//{
//    options.ServiceName = "MyHUB";
//});


//// 設定內建日誌輸出
//builder.Logging.ClearProviders(); // 清除預設，以便我們手動指定
//builder.Logging.AddConsole();     // 輸出到黑底白字的主控台 (開發除錯用)
//#pragma warning disable CA1416 // Validate platform compatibility
//builder.Logging.AddEventLog(options =>
//{
//    options.LogName = "MyEventLog";   // 1. 必須明確指定你剛剛在 Windows 建立的自訂日誌目錄名稱  
//    options.SourceName = "MyHubService";      // 2. 指定該目錄下的來源名稱
//});
//#pragma warning restore CA1416 // Validate platform compatibility

////手動初始化 Serilog 設定

////Log.Logger = new LoggerConfiguration()
////    .MinimumLevel.Debug()
////    .WriteTo.Console()
////    .WriteTo.EventLog(
////        source: "MyHubService",       // 事件來源名稱（通常用你的應用程式名稱）
////        logName: "MyEventLog",          // 寫入的日誌類別（預設是 Application）
////        manageEventSource: true          // 讓 Serilog 自動幫你建立事件來源（需要系統管理員權限）
////    )
////    .WriteTo.File(
////        path: "logs/service-log-.txt",        // 檔案路徑
////        rollingInterval: RollingInterval.Day, // 每天自動切換新檔案 (如 service-log-20261014.txt)
////        retainedFileCountLimit: 30,           // 檔案最多保留 30 天，過期自動刪除
////        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}" // 自訂格式
////    ).CreateLogger();

////設定檔(appsettings.json) 初始化 Serilog 設定 ,讀取 appsettings.json 裡面的 Serilog 區塊

//Log.Logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(builder.Configuration)
//    .CreateLogger();

////Serilog 與builder.Logging. 同時存在時
////在 ASP.NET Core 中，Serilog 與 builder.Logging 同時存在時，兩者的關係取決於你「如何將 Serilog 接入系統」。
////1. 兩者互不干涉（未整合）
////如果你只在 Program.cs 中初始化了 Log.Logger = new LoggerConfiguration()...，但沒有呼叫 builder.Host.UseSerilog() 或是 builder.Services.AddSerilog()，那這兩套系統是完全獨立運作的：
////• builder.Logging：負責框架內部、微軟預設組件（如 Kestrel、Entity Framework）以及你在依賴注入（DI）中注入 ILogger<T> 的所有日誌。它會輸出到內建的 Console 或 Debug 視窗。
////• Serilog：只有當你在程式中顯式使用靜態類別 Log.Information(...) 時才會觸發，並將日誌送到你為 Serilog 配置的目的地（如指定的 File Sink）。
////• 缺點：微軟框架內建的日誌和第三方套件的日誌無法被 Serilog 收集，且兩者的日誌設定（如 appsettings.json 中的 Logging 與 Serilog 區塊）需要分開維護，非常混亂。

////2.完美的結合：Serilog 接管系統（標準做法）
////當你在 Program.cs 中加上了接軌設定（例如 .NET 6/7/8+ 常見的寫法）：
////builder.Host.UseSerilog(); // 將 Serilog 接入並取代 .NET 內建的 Log 系統
////此時，Serilog 會直接取代（Override）builder.Logging 底下的所有預設日誌提供者（Console, Debug, EventSource 等）。
////• 運作機制：注入到所有 Controller、Service 裡的 ILogger<T> 介面，其底層實作都會被抽換成 Serilog。不論你是用 ILogger 還是靜態的 Log.CloseAndFlush()，全部都會經由 Serilog 的管線處理，並輸出到 Serilog 指定的 Sinks（如 Seq、File、Console）。
////• 行為變化：
////	• builder.Logging.ClearProviders() 會變得非必要，因為 UseSerilog() 預設就會清除微軟內建的 Provider。
////	• 在 appsettings.json 中，微軟原本的 "Logging": { ... }" 區塊會失去對大部分日誌的控制權，改由 "Serilog": { ... }" 區塊來決定過濾層級（MinimumLevel）與輸出格式。

//builder.Services.AddSerilog();



//builder.Services.AddHostedService<Worker>();

////var host = builder.Build();
////host.Run();
//try
//{
//    Log.Information("Windows 服務正在啟動...");
//    var host = builder.Build();
//    await host.RunAsync();
//}
//catch (Exception ex)
//{
//    Log.Fatal(ex, "Windows 服務啟動失敗！");
//}
//finally
//{
//    await Log.CloseAndFlushAsync(); // 確保程式關閉時，緩衝區的 Log 有確實寫入檔案
//}
#endregion

#region 進階寫法
try
{
    // 建立主機建立器
    Log.Information("建立主機建立器");
    var builder = Host.CreateDefaultBuilder(args)
        .UseContentRoot(AppContext.BaseDirectory) //讓背景服務支援 Windows 服務生命週期（校正工作目錄至 .exe 所在目錄）   
    #region JSON 設定檔說明
        //1.環境變數的來源：
        //	• Web 應用程式：預設會讀取系統環境變數 ASPNETCORE_ENVIRONMENT。
        //	• 非 Web 應用程式（如 Console、Worker Service）：預設讀取 DOTNET_ENVIRONMENT。若沒設定，預設值為 Production。
        //2.本地開發設定(launchSettings.json)：
        //	• 在開發環境下，Visual Studio 或 dotnet run 會優先讀取專案中 Properties / launchSettings.json 裡的 environmentVariables 設定，來覆蓋系統的環境變數。

        //方法一：Windows Service(背景服務 / Worker Service)

        //當您把.NET 程式註冊成 Windows 服務時，標準的 sc create 指令在傳遞啟動參數上有些限制。最安全且微軟官方推薦的方法有兩種：

        //1.使用 sc create 的 binPath 夾帶參數（最直接）
        //您可以在註冊服務時，直接把參數寫在執行檔路徑的後面。
        //請以系統管理員身分開啟命令提示字元(cmd) 或 PowerShell 執行：
        //cmd
        //::語法：sc create[服務名稱] binPath = "[執行檔路徑] --[參數名] [參數值]"
        //sc create MyWorkerService binPath = "C:\Publish\MyWorker.exe --environment Staging" start = auto
        //• 注意：binPath = 後面一定要有一個空格。
        //• 程式端如何接收：.NET 內建的 Host.CreateDefaultBuilder(args) 會自動解析命令列中--environment 的值，並將其設定為 EnvironmentName。

        //2.透過註冊表(Registry) 修改（針對已存在的服務）
        //如果服務已經註冊，您可以直接修改登錄表來傳遞參數，這通常用於複雜的啟動引數：
        //1.按下 Win + R 輸入 regedit 打開登錄編輯器。
        //2.移至路徑：HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\[您的服務名稱]
        //    3.找到 ImagePath 數值名稱。
        //4.點兩下修改它，在原本的路徑後面加上空白與您的參數。例如：
        //"C:\Publish\MyWorker.exe"--DOTNET_ENVIRONMENT = Staging


        //.ConfigureAppConfiguration(( hostContext ,config    ) =>
        //{
        //    // 清除前面的預設值（非必要，視需求而定）
        //    config.Sources.Clear();            

        //    // 💡 強制指定讀取 .exe / .dll 檔案所在的最正確目錄
        //    config.SetBasePath(AppContext.BaseDirectory);

        //    // 強制載入標準 json                                            
        //    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        //    // 根據目前的環境（例如 Production）自動載入對應的 json
        //    var env = hostContext.HostingEnvironment.EnvironmentName;           
        //    config.AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: true);

        //    // 補回環境變數
        //    config.AddEnvironmentVariables();
        //})
    #endregion
        .UseWindowsService(options =>
        {
            options.ServiceName = "MyHUB";
        })        
        .UseSerilog((context, services, configuration) =>
        {
            #region  設定全域主 Logger (專門寫 File)       

            // A. 先讀取 appsettings.json 基礎設定 (確保 MinimumLevel 與 Console 正常運作)
            configuration.ReadFrom.Configuration(context.Configuration);

            // B. 從 appsettings.json 讀取自訂的基本目錄，若讀不到則預設為當前目錄下的 "Logs" (註冊為服務時 :C:\Windows\System32)
            string baseDir = context.Configuration["LogFileBaseDir:BaseDirectory"] ?? "Logs";

            // C. 用 C# 代碼處理 Map 邏輯，徹底解決 JSON 轉型 Action 報錯的物理限制
            configuration.WriteTo.Map(
                    // 監聽日誌的來源類別
                    logEvent => logEvent.Properties.TryGetValue("SourceContext", out var value)
                                ? value.ToString().Trim('"').Split('.').Last() // 自動過濾命名空間，只取純類別名
                                : "Service",
                    (sinkKey, wt) =>
                    {
                        wt.File(
                            path: Path.Combine(baseDir, sinkKey, $"{sinkKey}-.log"), // 檔案路徑
                            rollingInterval: RollingInterval.Day,   // 每天自動切換新檔案 (如 service-log-20261014.txt)
                            retainedFileCountLimit: 30,             // 檔案最多保留 30 天，過期自動刪除                      
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}" // 自訂格式                                                                                                                                                  
                            );
                    });
            #endregion
        })
        .ConfigureServices((hostContext, services) =>
        {
            #region 獨立建立第二個 Logger (專門寫 MS SQL)            

            //A.建立第二個 Logger，並將它以單例 (Singleton) 注入到 DI 容器中
            Serilog.ILogger sqlLogger;
            try
            {
                sqlLogger = Serilog.Core.Logger.None;
                sqlLogger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.MSSqlServer
                (
                    connectionString: hostContext.Configuration.GetConnectionString("LoggerDBConnection"),
                    sinkOptions: new MSSqlServerSinkOptions { TableName = "AuditLogs", AutoCreateSqlTable = true }
                ).CreateLogger();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // 當 SQL Logger 建立失敗時, 直接降級為「什麼都不做的空 Logger」
                sqlLogger = Serilog.Core.Logger.None;

            }
            //B.註冊進 DI 容器，並給它一個獨特的介面或別名
            services.AddKeyedSingleton<Serilog.ILogger>("SqlLogger", (sp, key) => sqlLogger);
            #endregion

            //註冊您的背景服務（請替換成您實際的類別名稱）
            services.AddHostedService<Hub>();           
        });
    var host = builder.Build();// 建立並執行主機

    Log.Information("Windows 服務正在啟動...");
    //同步（Synchronous） 阻塞 ,會一直霸佔並阻塞啟動程式的主執行緒，直到服務被關閉。
    //host.Run();

    // 使用非同步方式啟動並等待服務結束 ,允許主執行緒在等待期間被釋放回執行緒池（Thread Pool），提高資源效率
    await host.RunAsync();
    
}
catch (Exception ex)
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory , "error.log")  ,ex.Message);
    Log.Fatal(ex, "Windows 服務因未預期錯誤而終止。");// 最外層防禦，防止 Windows 服務因設定錯誤當掉時完全沒留紀錄
}
finally
{
    Log.Information("Windows 服務停止...");
    Log.CloseAndFlush(); // 確保最後的日誌有被刷入檔案
}
#endregion