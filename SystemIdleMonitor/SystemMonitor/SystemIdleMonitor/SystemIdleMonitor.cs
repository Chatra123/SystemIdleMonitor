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
  public class SystemIdleMonitor
  {
    private readonly object sync = new object();
    private SystemCounter Counter;
    private MonitorQueue queCpu, queHDD, queNet;
    private Timer timer;
    public bool IsWorking { get; private set; }
    private DateTime StartTime;
    public TimeSpan Elapse { get { return DateTime.Now - StartTime; } }


    /// <summary>
    /// Constructor
    /// </summary>
    public SystemIdleMonitor(float thd_cpu, float thd_hdd, float thd_net, int duration_sec)
    {
      //Queue
      int capacity = duration_sec;
      queCpu = new MonitorQueue(thd_cpu, capacity);  //thdがマイナスなら無効状態で作成される。
      queHDD = new MonitorQueue(thd_hdd, capacity);
      queNet = new MonitorQueue(thd_net, capacity);
      Counter = new SystemCounter();
      Counter.HDD.SetPrefix(BytePerSec.MiBps);
      Counter.Network.SetPrefix(bitPerSec.Mibps);

      timer = new Timer(new TimerCallback(timer_Tick));
    }

    /// <summary>
    /// Start
    /// </summary>
    public void Start()
    {
      lock (sync)
      {
        queCpu.Clear();
        queHDD.Clear();
        queNet.Clear();
        timer.Change(0, 1000);
        StartTime = DateTime.Now;
        IsWorking = true;
      }
    }

    /// <summary>
    /// Stop
    /// </summary>
    public void Stop()
    {
      lock (sync)
      {
        IsWorking = false;
        timer.Change(Timeout.Infinite, Timeout.Infinite);
      }
    }

    /// <summary>
    /// timer_Tick
    /// </summary>
    public void timer_Tick(object obj)
    {
      lock (sync)
      {
        if (queCpu.Enable)
          queCpu.Enqueue(Counter.Processor.Usage());
        if (queHDD.Enable)
          queHDD.Enqueue(Counter.HDD.TotalTransfer());
        if (queNet.Enable)
          queNet.Enqueue(Counter.Network.Transfer());
      }
    }

    /// <summary>
    /// SystemIsIdle
    /// </summary>
    public bool SystemIsIdle()
    {
      lock (sync)
      {
        return IsWorking
                && queCpu.IsUnderThreshold
                && queHDD.IsUnderThreshold
                && queNet.IsUnderThreshold;
      }
    }

    /// <summary>
    /// 画面表示用のテキスト作成
    /// </summary>
    /// <returns></returns>
    public string MonitorState()
    {
      lock (sync)
      {
        string cpuformat = "{0,6:##0} %     ", hddformat = "{0,6:###0.0} MiB/s ", netformat = "{0,6:###0.0} Mibps";
        string space = new string(' ', 13);
        string line;
        var state = new StringBuilder();
        state.AppendLine("               CPU         HDD          Network");

        //Threshold
        line = "";
        line += "Threshold :";
        line += queCpu.Enable ? string.Format(cpuformat, queCpu.Threshold) : space;
        line += queHDD.Enable ? string.Format(hddformat, queHDD.Threshold) : space;
        line += queNet.Enable ? string.Format(netformat, queNet.Threshold) : space;
        state.AppendLine(line);
        //Average
        line = "";
        line += "  Average :";
        line += queCpu.Enable ? string.Format(cpuformat, queCpu.Average) : space;
        line += queHDD.Enable ? string.Format(hddformat, queHDD.Average) : space;
        line += queNet.Enable ? string.Format(netformat, queNet.Average) : space;
        state.AppendLine(line);
        //Value
        line = "";
        line += "    Value :";
        line += queCpu.Enable ? string.Format(cpuformat, queCpu.LatestValue) : space;
        line += queHDD.Enable ? string.Format(hddformat, queHDD.LatestValue) : space;
        line += queNet.Enable ? string.Format(netformat, queNet.LatestValue) : space;
        state.AppendLine(line);
        //Fill
        var quelist = new MonitorQueue[] { queCpu, queHDD, queNet };
        quelist = quelist.Where((que) => que.Enable).ToArray();
        if (quelist.Any())
        {
          line = "";
          line += "     Fill :";
          line += string.Format(" {0,3:##0} / {1,3:##0}", quelist[0].Count, quelist[0].Capacity);
          state.AppendLine(line);
        }
        return state.ToString();
      }
    }
  }

}