using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;  // SystemEvents.PowerModeChanged 

namespace SystemIdleMonitor
{
  /// <summary>
  /// 閾値のデフォルト値
  /// </summary>
  internal static class DefaultValue
  {
    public const float
      //      %            MiB/sec          Mbps            sec           sec
      Cpu = 60f, Hdd = 30.0f, Network = -1.0f, Durarion = 10, Timeout = 20;
  }

  internal class Program
  {
    private static readonly object sync = new object();
    private static SystemIdleMonitor monitor;
    private static BlackProcessChecker blackChecker;
    private static float duration, timeout;
    private static bool HasConsole;          //コンソールウィンドウを持っているか？

    private static void Main(string[] appArgs)
    {
      ////テスト引数
      //var testArgs = new List<string>();
      //testArgs.AddRange(new string[] { "-cputhd", "40" });         //%
      //testArgs.AddRange(new string[] { "-hddthd", "6" });          //MiB/sec
      //testArgs.AddRange(new string[] { "-netthd", "10" });         //Mbps
      //testArgs.AddRange(new string[] { "-duration", "10" });       //sec
      //testArgs.AddRange(new string[] { "-timeout", "20" });        //sec
      //appArgs = testArgs.ToArray();

      //Initialize
      AppDomain.CurrentDomain.UnhandledException += OctNov.Excp.ExceptionInfo.OnUnhandledException;
      SystemEvents.PowerModeChanged += OnPowerModeChanged;                               //Windows Sleep検知

      //コンソールウィンドウを持っているか？
      try
      {
        Console.Clear();
        HasConsole = true;
      }
      catch
      {
        //ファイル等にリダイレクトされていると例外
        HasConsole = false;
      }

      //CommandLine
      //初期値
      CommandLine.SetDefault(new float[] { DefaultValue.Cpu, DefaultValue.Hdd, DefaultValue.Network,
                                           DefaultValue.Durarion, DefaultValue.Timeout });
      //設定ファイル
      var setting_file = new Setting_File();
      setting_file.Load();
      CommandLine.Parse(setting_file.TextFileArgs);
      CommandLine.Parse(appArgs);

      //ブラックプロセス
      blackChecker = new BlackProcessChecker(setting_file.ProcessList);
      if (blackChecker.NotExistBlack() == false)
      {
        Exit_withIdle(false);          //終了　ブラックプロセス
      }

      //SystemIdleMonitor
      //  PerformanceCounterの作成は初回のみ数秒かかる。ＣＰＵ負荷も高い。
      duration = CommandLine.Duration;
      timeout = CommandLine.Timeout;
      monitor = new SystemIdleMonitor(CommandLine.CpuThd,
                                      CommandLine.HddThd,
                                      CommandLine.NetThd,
                                      (int)duration);
      monitor.Start();

      //
      //main loop
      //
      Thread.Sleep(500);              //systemMonitorと更新タイミングをずらす
      while (true)
      {
        lock (sync)
        {
          //画面表示
          if (HasConsole)
          {
            string text = GetText_MonitoringState();
            Console.Clear();
            Console.Error.WriteLine(text);
          }

          //timeout ? 
          //  timeout = -1 なら無期限
          if (0 < timeout
               && timeout < monitor.Elapse.TotalSeconds)
          {
            Exit_withIdle(false);      //終了 タイムアウト
          }
          //System Is Idle ?
          if (monitor.SystemIsIdle()
            && blackChecker.NotExistBlack())
          {
            Exit_withIdle(true);       //終了 アイドル
          }
        }
        Thread.Sleep(1 * 1000);
      }//while
    }//func


    /// <summary>
    /// 画面表示用のテキスト作成
    /// </summary>
    private static string GetText_MonitoringState()
    {
      var text = new StringBuilder();
      var system = (monitor != null) ? monitor.MonitorState() : "";
      var black = (blackChecker.NotExistBlack()) ? "○" : "×";
      var idle = (monitor != null
        && monitor.SystemIsIdle()) ? "○" : "×";

      text.AppendLine("duration = " + duration + "    timeout = " + timeout);
      text.AppendLine(system);
      text.AppendLine("NotExistBlack = " + black);
      text.AppendLine("SysteIsIdle   = " + idle);
      return text.ToString();
    }


    /// <summary>
    /// 終了処理
    /// </summary>
    private static void Exit_withIdle(bool isIdleExit)
    {
      string text = isIdleExit ? "true" : "false";
      int exitcode = isIdleExit ? 0 : 1;

      if (HasConsole)
        Console.Clear();
      Console.WriteLine(text);
      Console.WriteLine(GetText_MonitoringState());

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      if (HasConsole)
        Thread.Sleep(2000);
      Environment.Exit(exitcode);
    }




    #region PowerModeChangedEvent
    /// <summary>
    /// PowerModeChangedEvent
    /// </summary>
    /// <remarks>
    /// PowerModes.Suspend  →  [windows sleep]  →  PowerModes.Resume    の順で発生するとはかぎらない。
    ///                         [windows sleep]  →  PowerModes.Suspend  →  PowerModes.Resume 又は
    ///                         [windows sleep]  →  PowerModes.Resume   →  PowerModes.Suspend
    /// の順でイベントが処理されることもある。
    /// </remarks>
    private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
      lock (sync)
      {
        Exit_withIdle(false);
      }
    }

    #endregion PowerModeChangedEvent
  }
}