﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#region region_title
#endregion


namespace SystemIdleMonitor
{

  /// <summary>
  /// 閾値のデフォルト値
  /// </summary>
  static class DefValue
  {
    public const float
      //         %              MiB/sec             Mbps            sec           sec  
      CpuThd = 60f, HddThd = 30.0f, NetworkThd = -1.0f, Durarion = 20, Timeout = 30;
  }


  class Program
  {
    static SystemMonitor systemMonitor;
    static readonly object sync = new object();
    static float duration, timeout;
    static DateTime startTime, lastResumeTime;
    static bool HaveConsole;
    static bool SystemIsSleep;


    static void Main(string[] args)
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
      HaveConsole = true;
      try
      {
        Console.Clear();
      }
      catch
      {
        //ファイル等にリダイレクトされていると例外
        HaveConsole = false;
      }



      //CommandLine
      CommandLine.SetThdValue(new float[] { DefValue.CpuThd, DefValue.HddThd, DefValue.NetworkThd, 
                                             DefValue.Durarion, DefValue.Timeout });
      //実行ファイルの引数
      CommandLine.Parse(args);

      //テキストファイルから引数取得
      var textArgs = new Func<List<string>, string[]>(
        (baseList) =>
        {
          //string "-cpu 30"をスペースで分割してList<string>に変換。
          //List<string>  →  List<List<string>>
          var L1 = baseList.Select(line =>
          {
            return line.Split(new char[] { ' ', '　', '\t' }).ToList();
          });
          //List<List<string>>  →  List<string>
          var L2 = L1.SelectMany(element => element)
                     .Where((line) => string.IsNullOrWhiteSpace(line) == false);         //空白行削除

          return L2.ToArray();
        })(CheckProcess.ProcessList);

      CommandLine.Parse(textArgs);




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
    /// <returns></returns>
    static string GetText_MonitoringState()
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
    static void Exit_timeout()
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
    static void Exit_withIdle()
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
    static class CommandLine
    {
      public static float CpuThd { get; private set; }
      public static float HddThd { get; private set; }
      public static float NetThd { get; private set; }
      public static float Duration { get; private set; }
      public static float Timeout { get; private set; }


      /// <summary>
      /// 指定した値でThdを設定する。
      /// </summary>
      /// <param name="defvalue">指定値</param>
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
      /// <param name="args"></param>
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
    #endregion





    #region PowerModeChangedEvent
    /// <summary>
    /// PowerModeChangedEvent
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks>
    ///  PowerModes.Suspend  →  [windows sleep]  →  PowerModes.Resumeの順で発生するとはかぎらない。
    ///                          [windows sleep]  →  PowerModes.Suspend  →  PowerModes.Resume 又は
    ///                          [windows sleep]  →  PowerModes.Resume   →  PowerModes.Suspend
    ///  の順でイベントが処理されることもある。
    /// </remarks>
    static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
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

          Thread.Sleep(10 * 1000);                         //リジューム直後は処理しない

          startTime = DateTime.Now;
          lastResumeTime = DateTime.Now;
          systemMonitor.TimerStart();
          SystemIsSleep = false;
        }
      }
    }
    #endregion

  }



}
