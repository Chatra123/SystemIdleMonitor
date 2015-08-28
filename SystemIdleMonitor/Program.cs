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
  internal static class DefThreshold
  {
    public const float
      //         %              MiB/sec             Mbps             sec           sec
      CpuThd = 60f, HddThd = 30.0f, NetworkThd = -1.0f, Durarion = 20, Timeout = 30;
  }

  internal class Program
  {
    private static readonly object sync = new object();
    private static SystemMonitor systemMonitor;
    private static float duration, timeout;                 //計測期間、タイムアウト時間
    private static bool HaveConsole;                        //コンソールを持っているか？


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

      SystemEvents.PowerModeChanged += OnPowerModeChanged;                               //Windows Sleep検知

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
      CommandLine.SetDefault(new float[] { DefThreshold.CpuThd, DefThreshold.HddThd, DefThreshold.NetworkThd,
                                             DefThreshold.Durarion, DefThreshold.Timeout });

      //テキストファイルからの引数
      CommandLine.Parse(Setting.TextArgs);

      //実行ファイルの引数
      CommandLine.Parse(appArgs);

      //duration
      duration = CommandLine.Duration;
      timeout = CommandLine.Timeout;
      if (duration <= 0)
      {
        Exit_withIdle(false);          //終了  duration <= 0 
      }

      //ブラックプロセスをチェック
      //systemMonitor作成前に１度チェック。
      if (CheckProcess.NotExistBlack() == false)
      {
        Exit_withIdle(false);          //終了　ブラックプロセス
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
      var startTime = DateTime.Now;
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

          //timeout? 
          //timeout = -1 なら無期限待機
          if (0 < timeout
               && timeout < (DateTime.Now - startTime).TotalSeconds
              )
          {
            Exit_withIdle(false);      //終了 タイムアウト
          }

          //SystemIsIdle?
          if (systemMonitor.SystemIsIdle()
              && CheckProcess.NotExistBlack()
              )
          {
            Exit_withIdle(true);       //終了 アイドル
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
      var text = new StringBuilder();

      var system = (systemMonitor != null) ? systemMonitor.MonitoringState() : "";

      var black = (CheckProcess.NotExistBlack()) ? "○" : "×";

      var idle = (systemMonitor != null
        && systemMonitor.SystemIsIdle()) ? "○" : "×";

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

      if (HaveConsole) Console.Clear();
      Console.WriteLine(text);
      Console.WriteLine(GetText_MonitoringState());

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      Thread.Sleep(2000);
      Environment.Exit(exitcode);
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
      public static void SetDefault(float[] defvalue)
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
    /// PowerModes.Suspend  →  [windows sleep]  →  PowerModes.Resumeの順で発生するとはかぎらない。
    ///                         [windows sleep]  →  PowerModes.Suspend  →  PowerModes.Resume 又は
    ///                         [windows sleep]  →  PowerModes.Resume   →  PowerModes.Suspend
    /// の順でイベントが処理されることもある。
    /// 連続でPowerModes.Resumeだけがくることもあった。
    ///
    /// スリープ処理はデバッグしづらいので、
    /// ”計測の一時停止”から”Exit_withIdle(false)で終了”に処理を変更。
    /// 
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