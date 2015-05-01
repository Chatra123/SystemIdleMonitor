using System;
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

  static class DefValue
  {
    public const float
      //           %               MiB/sec             Mbps            sec           sec  
      CpuThd = 60f, HddThd = 30.0f, NetworkThd = -1.0f, Durarion = 20, Timeout = 30;
  }


  class Program
  {
    static SystemMonitor systemMonitor;
    static readonly object sync = new object();
    static float duration, timeout;                       //timeout = -1 なら無期限待機
    static DateTime startTime, lastResumeTime;
    static bool HaveConsole;
    static bool SystemIsSleep;


    static void Main(string[] args)
    {
      //テスト引数
      //var testArgs = new List<string>();
      //testArgs.AddRange(new string[] { "-cputhd", "40" });
      //testArgs.AddRange(new string[] { "-hddthd", "6" });
      //testArgs.AddRange(new string[] { "-netthd", "10" });
      //testArgs.AddRange(new string[] { "-duration", "20" });
      //testArgs.AddRange(new string[] { "-timeout", "30" });
      //args = testArgs.ToArray();


      //Initialize
      AppDomain.CurrentDomain.UnhandledException += ExceptionInfo.OnUnhandledException;  //例外を捕捉する

      SystemEvents.PowerModeChanged += OnPowerModeChanged;                     //suspend検知

      Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;   //PerformanceCounter呼び出しの負荷が大きいのでIdle

      //コンソールウィンドウを持っているか？
      HaveConsole = true;
      try { Console.Clear(); }
      catch { HaveConsole = false; }   //ファイル等にリダイレクトされていると例外

      //Args    
      float[] systhd = ParseCommandLine(args);
      duration = systhd[3];
      timeout = systhd[4];
      if (duration <= 0)
      {
        Exit_withIdle();               //終了
      }

      //SystemMonitor                      cpu       hdd      network         duration
      systemMonitor = new SystemMonitor(systhd[0], systhd[1], systhd[2], (int)duration);



      //
      //main loop
      //
      Thread.Sleep(1500);              //systemMonitorと更新タイミングをずらす
      startTime = DateTime.Now;
      while (true)
      {
        lock (sync)
        {
          //画面表示更新
          if (HaveConsole)
          {
            string text = GetText_MonitorState();
            Console.Clear();
            Console.Error.WriteLine(text);
          }


          //timeout?
          if (0 < timeout
                && SystemIsSleep == false
                && timeout < (DateTime.Now - startTime).TotalSeconds
              )
          {
            Exit_timeout();            //終了
          }


          //SystemIsIdle?
          if (systemMonitor.SystemIsIdle()
                && SystemIsSleep == false
                && CheckProcess.NotExistBlack()
              )
          {
            Exit_withIdle();           //終了
          }
        }

        Thread.Sleep(3 * 1000);

      }//while
    }//func




    //
    //GetTextMonitorState
    //
    static string GetText_MonitorState()
    {
      var state = new StringBuilder();
      var black = (CheckProcess.NotExistBlack()) ? "○" : "×";
      var idle = (systemMonitor.SystemIsIdle()) ? "○" : "×";

      state.AppendLine("duration = " + duration + "    timeout = " + timeout);
      state.AppendLine(systemMonitor.GetMonitorState());
      state.AppendLine("NotExistBlack = " + black);
      state.AppendLine("SysteIsIdle   = " + idle);
      return state.ToString();
    }


    //
    //終了
    //
    //Exit_timeout
    static void Exit_timeout()
    {
      if (HaveConsole) Console.Clear();
      Console.WriteLine("false");
      Console.WriteLine(GetText_MonitorState());

      Thread.Sleep(2000);

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      Environment.Exit(1);             //ExitCode: 1
    }

    //Exit_withIdle
    static void Exit_withIdle()
    {
      if (HaveConsole) Console.Clear();
      Console.Write("true");

      Thread.Sleep(2000);

      SystemEvents.PowerModeChanged -= OnPowerModeChanged;
      Environment.Exit(0);             //ExitCode: 0
    }




    //
    //ParseCommandLine
    //
    #region ParseCommandLine
    static float[] ParseCommandLine(string[] args)
    {

      float cputhd = DefValue.CpuThd, hddthd = DefValue.HddThd, netthd = DefValue.NetworkThd,
            duration = DefValue.Durarion, timeout = DefValue.Timeout;

      if (args.Count() == 0)
      {
        return new float[] { cputhd, hddthd, netthd, duration, timeout };
      }
      else
      {
        string name, param = "";
        bool canparse = false;
        float result = -1;


        for (int i = 0; i < args.Length; i++)
        {
          name = args[i].ToLower();

          if (name.IndexOf("-") == 0 || name.IndexOf("/") == 0)
            name = name.Substring(1, name.Length - 1);     //  - / をはずす
          else
            continue;


          if (i < args.Length - 1)
          {
            param = args[i + 1];
            canparse = float.TryParse(param, out result);

            switch (name)
            {
              case "cputhd":
                cputhd = DefValue.CpuThd;
                if (canparse) cputhd = result;
                break;

              case "hddthd":
                hddthd = DefValue.HddThd;
                if (canparse) hddthd = result;
                break;

              case "netthd":
                netthd = DefValue.NetworkThd;
                if (canparse) netthd = result;
                break;

              case "dur":
              case "duration":
                duration = DefValue.Durarion;
                if (canparse) duration = result;
                break;

              case "timeout":
                timeout = DefValue.Timeout;
                if (canparse) timeout = result;
                break;

            }//switch
          }//if

        }//for
      }//if

      return new float[] { cputhd, hddthd, netthd, duration, timeout };

    }//function
    #endregion


    //
    //PowerModeChangedEvent
    //
    #region PowerModeChangedEvent
    //  PowerModes.Suspend　→　[windows sleep]　→　PowerModes.Resumeの順で発生するとはかぎらない。
    //　　　　　　　　　　　　　[windows sleep]　→　PowerModes.Suspend　→　PowerModes.Resume 又は
    //　                        [windows sleep]　→　PowerModes.Resume　 →　PowerModes.Suspend
    //の順でイベントがくることもある。
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
