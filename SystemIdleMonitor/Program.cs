using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{
  /// <summary>
  /// 閾値のデフォルト値
  /// </summary>
  internal static class DefValue
  {
    public const float
      //         %              MiB/sec             Mbps            sec           sec
      CpuThd = 60f, HddThd = 30.0f, NetworkThd = -1.0f, Durarion = 20, Timeout = 30;
  }

  internal class Program
  {
    private static SystemMonitor systemMonitor;
    private static readonly object sync = new object();
    private static float duration, timeout;
    private static DateTime startTime, lastResumeTime;
    private static bool HaveConsole;
    private static bool SystemIsSleep;

    private static void Main(string[] appArgs)
    {
      //テスト引数
      //var testArgs = new List<string>();
      //testArgs.AddRange(new string[] { "-cputhd", "40" });         //%
      //testArgs.AddRange(new string[] { "-hddthd", "6" });          //MiB/sec
      //testArgs.AddRange(new string[] { "-netthd", "10" });         //Mbps
      //testArgs.AddRange(new string[] { "-duration", "20" });       //sec
      //testArgs.AddRange(new string[] { "-timeout", "30" });        //sec
      //args = testArgs.ToArray();

      //Initialize
      AppDomain.CurrentDomain.UnhandledException += ExceptionInfo.OnUnhandledException;  //例外を捕捉する

      SystemEvents.PowerModeChanged += OnPowerModeChanged;                               //suspend検知

      //コンソールウィンドウを持っているか？
      try
      {
        Console.Clear();
        HaveConsole = true;
      }
      catch
      {
        //ファイル等にリダイレクトされていると例外
        HaveConsole = false;
      }

      //CommandLine
      //初期値
      CommandLine.SetThdValue(new float[] { DefValue.CpuThd, DefValue.HddThd, DefValue.NetworkThd,
                                             DefValue.Durarion, DefValue.Timeout });

      //テキストファイルからの引数
      CommandLine.Parse(Setting.TextArgs);

      //実行ファイルの引数
      CommandLine.Parse(appArgs);

      //duration
      duration = CommandLine.Duration;
      timeout = CommandLine.Timeout;
      if (duration <= 0)
      {
        Exit_withIdle();               //終了
      }

      //SystemMonitor
      //  PerformanceCounterの作成は初回のみ数秒かかる。ＣＰＵ負荷も高い。
      systemMonitor = new SystemMonitor(CommandLine.CpuThd,
                                        CommandLine.HddThd,
                                        CommandLine.NetThd,
                                        (int)duration);
      systemMonitor.TimerStart();

      //
      //main loop
      //
      Thread.Sleep(500);              //systemMonitorと更新タイミングをずらす
      startTime = DateTime.Now;
      while (true)
      {
        lock (sync)
        {
          //画面表示更新
          if (HaveConsole)
          {
            string text = GetText_MonitoringState();
            Console.Clear();
            Console.Error.WriteLine(text);
          }

          //timeout?                  timeout = -1 なら無期限待機
          if (SystemIsSleep == false
                 && 0 < timeout
                 && timeout < (DateTime.Now - startTime).TotalSeconds
              )
          {
            Exit_timeout();            //終了
          }

          //SystemIsIdle?
          if (SystemIsSleep == false
                 && systemMonitor.SystemIsIdle()
                 && CheckProcess.NotExistBlack()
              )
          {
            Exit_withIdle();           //終了
          }
        }

        Thread.Sleep(1 * 1000);
      }//while
    }//func

    /// <summary>
    /// 画面表示用のテキスト取得
    /// </summary>
    private static string GetText_MonitoringState()
    {
      var state = new StringBuilder();
      var black = (CheckProcess.NotExistBlack()) ? "○" : "×";
      var idle = (systemMonitor.SystemIsIdle()) ? "○" : "×";

      state.AppendLine("duration = " + duration + "    timeout = " + timeout);
      state.AppendLine(systemMonitor.MonitoringState());
      state.AppendLine("NotExistBlack = " + black);
      state.AppendLine("SysteIsIdle   = " + idle);
      return state.ToString();
    }

    /// <summary>
    /// 終了処理　タイムアウト
    /// </summary>
    private static void Exit_timeout()
    {
      if (HaveConsole) Console.Clear();
      Console.WriteLine("false");
      Console.WriteLine(GetText_MonitoringState());
      Thread.Sleep(2000);

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      Environment.Exit(1);             //ExitCode: 1
    }

    /// <summary>
    /// 終了処理　アイドル
    /// </summary>
    private static void Exit_withIdle()
    {
      if (HaveConsole) Console.Clear();
      Console.Write("true");
      Thread.Sleep(2000);

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      Environment.Exit(0);             //ExitCode: 0
    }

    #region コマンドライン

    /// <summary>
    /// コマンドライン
    /// </summary>
    private static class CommandLine
    {
      public static float CpuThd { get; private set; }
      public static float HddThd { get; private set; }
      public static float NetThd { get; private set; }
      public static float Duration { get; private set; }
      public static float Timeout { get; private set; }

      /// <summary>
      /// Thdの初期値を設定する。
      /// </summary>
      public static void SetThdValue(float[] defvalue)
      {
        CpuThd = defvalue[0];
        HddThd = defvalue[1];
        NetThd = defvalue[2];
        Duration = defvalue[3];
        Timeout = defvalue[4];
      }

      /// <summary>
      /// コマンドライン解析
      /// </summary>
      public static void Parse(string[] args)
      {
        for (int i = 0; i < args.Count(); i++)
        {
          string key, sValue;
          bool canParse;
          float fValue;

          key = args[i].ToLower();
          sValue = (i + 1 < args.Count()) ? args[i + 1] : "";
          canParse = float.TryParse(sValue, out fValue);

          //  - / をはずす
          if (key.IndexOf("-") == 0 || key.IndexOf("/") == 0)
            key = key.Substring(1, key.Length - 1);
          else
            continue;

          //小文字で比較
          switch (key)
          {
            case "cpu":
            case "cputhd":
              if (canParse) CpuThd = fValue;
              break;

            case "hdd":
            case "hddthd":
              if (canParse) HddThd = fValue;
              break;

            case "net":
            case "netthd":
              if (canParse) NetThd = fValue;
              break;

            case "dur":
            case "duration":
              if (canParse) Duration = fValue;
              break;

            case "timeout":
              if (canParse) Timeout = fValue;
              break;
          }
        }
      }//function
    }//class

    #endregion コマンドライン

    #region PowerModeChangedEvent

    /// <summary>
    /// PowerModeChangedEvent
    /// </summary>
    /// <remarks>
    ///  PowerModes.Suspend  →  [windows sleep]  →  PowerModes.Resumeの順で発生するとはかぎらない。
    ///                          [windows sleep]  →  PowerModes.Suspend  →  PowerModes.Resume 又は
    ///                          [windows sleep]  →  PowerModes.Resume   →  PowerModes.Suspend
    ///  の順でイベントが処理されることもある。
    /// </remarks>
    private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
      lock (sync)
      {
        //Suspend
        if (e.Mode == PowerModes.Suspend
          && 10 < (DateTime.Now - lastResumeTime).TotalSeconds)      //前回のリジュームから１０秒たっている？
        {
          SystemIsSleep = true;
          systemMonitor.TimerStop();
        }
        //Resume
        else if (e.Mode == PowerModes.Resume)
        {
          SystemIsSleep = true;
          systemMonitor.TimerStop();

          Thread.Sleep(12 * 1000);                         //リジューム直後は処理しない

          startTime = DateTime.Now;
          lastResumeTime = DateTime.Now;
          systemMonitor.TimerStart();
          SystemIsSleep = false;
        }
      }
    }

    #endregion PowerModeChangedEvent
  }
}