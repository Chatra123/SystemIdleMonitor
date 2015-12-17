using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{

  /*
   * Queue<float>だと最後尾に値を取得できないのclass MonitorQueueを作成。
   * 
   * ログ表示用のLatestValueは毎回記録する。
   * IsUnderThresholdによってQueue内の値が閾値以下かを判定。
   * 
   */

  internal class SystemIdleMonitorQueue
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
    public SystemIdleMonitorQueue(float new_threshold, int new_capacity)
    {
      Threshold = new_threshold;
      Capacity = new_capacity;

      Enable = (0 <= new_threshold && 0 < new_capacity) ? true : false;
      if (Enable) Que = new Queue<float>(new_capacity);
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



}


