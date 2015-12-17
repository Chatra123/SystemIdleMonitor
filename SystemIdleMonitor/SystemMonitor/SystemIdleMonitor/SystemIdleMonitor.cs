using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{

  /// <summary>
  /// システム使用率が低いかを監視
  /// </summary>
  internal class SystemIdleMonitor
  {
    private SystemCounter systemCounter;
    private SystemIdleMonitorQueue queCpu, queHDD, queNetwork;

    private readonly object sync = new object();
    private Timer MonitoringTimer;
    private bool TimerIsWorking;

    //Constructor
    public SystemIdleMonitor(float thd_cpu, float thd_hdd, float thd_net, int duration_sec)
    {
      lock (sync)
      {
        //Queue
        int queCapacity = duration_sec;
        queCpu = new SystemIdleMonitorQueue(thd_cpu, queCapacity);        //thd or queCapacityがマイナスなら無効状態で作成される。
        queHDD = new SystemIdleMonitorQueue(thd_hdd, queCapacity);
        queNetwork = new SystemIdleMonitorQueue(thd_net, queCapacity);

        //SystemCounter
        systemCounter = new SystemCounter();

        //timer
        MonitoringTimer = new Timer(new TimerCallback(timer_Tick));
      }
    }

    /// <summary>
    /// TimerStart
    /// </summary>
    public void TimerStart()
    {
      lock (sync)
      {
        MonitoringTimer.Change(0, 1000);         //１秒間隔で処理
        TimerIsWorking = true;
      }
    }

    /// <summary>
    /// TimerStop
    /// </summary>
    public void TimerStop()
    {
      lock (sync)
      {
        TimerIsWorking = false;
        MonitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);

        //Reset
        queCpu.Reset();
        queHDD.Reset();
        queNetwork.Reset();
      }
    }

    /// <summary>
    /// timer_Tick
    /// </summary>
    public void timer_Tick(object obj)
    {
      lock (sync)
      {
        //値を取得する前にEnableでフィルター、０．１％ぐらいは負荷が下がる。
        if (queCpu.Enable)
          queCpu.Enqueue(systemCounter.Processor.Usage());

        if (queHDD.Enable)
          queHDD.Enqueue(systemCounter.HDD.TotalTransfer(BytePerSec.MiBps));

        if (queNetwork.Enable)
          queNetwork.Enqueue(systemCounter.Network.Transfer(bitPerSec.Mibps));
      }
    }

    /// <summary>
    /// SystemIsIdle
    /// </summary>
    public bool SystemIsIdle()
    {
      lock (sync)
      {
        return TimerIsWorking
                && queCpu.IsUnderThreshold
                && queHDD.IsUnderThreshold
                && queNetwork.IsUnderThreshold;
      }
    }

    /// <summary>
    /// 画面表示用のテキスト作成
    /// </summary>
    /// <returns></returns>
    public string MonitoringState()
    {
      lock (sync)
      {
        string cpuformat = "{0,6:##0} %     ", hddformat = "{0,6:###0.0} MiB/s ", netformat = "{0,6:###0.0} Mibps";
        string empty = "             ";
        string line;

        var state = new StringBuilder();
        state.AppendLine("               CPU         HDD          Network");


        //Threshold
        line = "";
        line += "Threshold :";
        line += (queCpu.Enable) ? String.Format(cpuformat, queCpu.Threshold) : empty;
        line += (queHDD.Enable) ? String.Format(hddformat, queHDD.Threshold) : empty;
        line += (queNetwork.Enable) ? String.Format(netformat, queNetwork.Threshold) : empty;
        state.AppendLine(line);

        //Average
        line = "";
        line += "  Average :";
        line += (queCpu.Enable) ? String.Format(cpuformat, queCpu.Average) : empty;
        line += (queHDD.Enable) ? String.Format(hddformat, queHDD.Average) : empty;
        line += (queNetwork.Enable) ? String.Format(netformat, queNetwork.Average) : empty;
        state.AppendLine(line);

        //Value
        line = "";
        line += "    Value :";
        line += (queCpu.Enable) ? String.Format(cpuformat, queCpu.LatestValue) : empty;
        line += (queHDD.Enable) ? String.Format(hddformat, queHDD.LatestValue) : empty;
        line += (queNetwork.Enable) ? String.Format(netformat, queNetwork.LatestValue) : empty;
        state.AppendLine(line);

        //Fill
        var quelist = new SystemIdleMonitorQueue[] { queCpu, queHDD, queNetwork };
        quelist = quelist.Where((que) => que.Enable).ToArray();
        //Enableなqueがある？
        if (0 < quelist.Count())
        {
          line = "";
          line += "     Fill :";
          line += String.Format(" {0,3:##0} / {1,3:##0}", quelist[0].Count, quelist[0].Capacity);
          state.AppendLine(line);
        }

        return state.ToString();
      }
    }
  }

}