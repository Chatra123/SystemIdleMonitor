using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{
  #region smQueue

  /*
   * ログ表示用のLatestValueの取得といくつかのプロパティが欲しいのでclass smQueueを作成。
   * Que[Que.Count-1]で値を取得できないのでLatestValueに毎回記録する。
   */

  internal class smQueue
  {
    private Queue<float> Que;
    public bool Enable;
    public float Threshold { get; private set; }
    public int Capacity { get; private set; }

    private readonly object sync = new object();
    public int Count { get { return Que.Count; } }
    public bool HasValue { get { return 0 < Que.Count; } }
    public bool IsFilled { get { return Capacity <= Que.Count; } }
    public float Average { get { lock (sync) { return (Enable && HasValue) ? Que.Average() : 0; } } }

    //Que最後尾の値
    private float latestValue;

    public float LatestValue
    {
      get { lock (sync) { return (Enable && HasValue) ? latestValue : 0; } }
      private set { lock (sync) { if (Enable)latestValue = value; } }
    }

    //Constructor
    public smQueue(float newThreshold, int newCapacity)
    {
      Threshold = newThreshold;
      Capacity = newCapacity;

      Enable = (0 <= newThreshold && 0 < newCapacity) ? true : false;
      if (Enable) Que = new Queue<float>(newCapacity);
    }

    /// <summary>
    ///  Reset
    /// </summary>
    public void Reset()
    {
      if (Enable == false) return;
      lock (sync)
      {
        LatestValue = 0;
        Que = new Queue<float>(Capacity);
      }
    }

    /// <summary>
    ///  Enqueue
    /// </summary>
    public void Enqueue(float value)
    {
      if (Enable == false) return;
      lock (sync)
      {
        if (IsFilled) Dequeue();
        LatestValue = value;
        Que.Enqueue(value);
      }
    }

    /// <summary>
    ///  Dequeue
    /// </summary>
    public void Dequeue()
    {
      if (Enable == false) return;
      lock (sync)
      {
        if (HasValue) Que.Dequeue();
        if (HasValue == false) LatestValue = 0;
      }
    }

    /// <summary>
    ///  IsUnderThreshold
    /// </summary>
    public bool IsUnderThreshold
    {
      get
      {
        if (Enable == false) return true;        //無効なら常にtrueを返す。
        if (IsFilled == false) return false;
        lock (sync)
        {
          //Averageは完全な０．００にならないのでThresholdに０．０１加える。（特にＨＤＤ）
          if (Average < Threshold + 0.01) return true;
          else return false;
        }
      }
    }
  }

  #endregion smQueue

  #region SystemMonitor

  internal class SystemMonitor
  {
    private SystemCounter systemCounter;
    private smQueue queCpu, queHDD, queNetwork;

    private readonly object sync = new object();
    private Timer MonitoringTimer;
    private bool TimerIsWorking;

    //Constructor
    public SystemMonitor(float thd_cpu, float thd_hdd, float thd_net, int duration_sec)
    {
      lock (sync)
      {
        //Queue
        int queCapacity = duration_sec;
        queCpu = new smQueue(thd_cpu, queCapacity);        //thd or queCapacityがマイナスなら無効状態で作成される。
        queHDD = new smQueue(thd_hdd, queCapacity);
        queNetwork = new smQueue(thd_net, queCapacity);

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
        //値を取得する前にEnableでフィルター
        if (queCpu.Enable) queCpu.Enqueue(systemCounter.Processor.Usage());
        if (queHDD.Enable) queHDD.Enqueue(systemCounter.HDD.Transfer(BytePerSec.MiBps));
        if (queNetwork.Enable) queNetwork.Enqueue(systemCounter.Network.Transfer(bitPerSec.Mibps));
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
        string line, empty = "             ";

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
        var quelist = new smQueue[] { queCpu, queHDD, queNetwork };
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

  #endregion SystemMonitor
}