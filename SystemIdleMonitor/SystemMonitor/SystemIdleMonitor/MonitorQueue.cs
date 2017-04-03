using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{

  /*
   * Queue<float>だと最後尾の値を取得できないのclass Queue_SIMを作成。
   * ログ表示用のLatestValueを記録し、
   * IsUnderThresholdでQueue内の値が閾値以下かを判定する。
   */
  internal class MonitorQueue
  {
    private Queue<float> Que;
    public bool Enable;
    public float Threshold { get; private set; }
    public int Capacity { get; private set; }
    public int Count { get { return Que.Count; } }
    public bool IsFilled { get { return Capacity <= Que.Count; } }
    public float Average { get { return Que.Any() ? Que.Average() : 0; } }

    //Que最後尾の値
    private float latest;
    public float LatestValue
    {
      get { return latest; }
      private set { if (Enable) latest = value; }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public MonitorQueue(float _threshold, int _capacity)
    {
      Threshold = _threshold;
      Capacity = _capacity;
      Enable = 0 <= _threshold ? true : false;
      if (Enable) Que = new Queue<float>(_capacity);
    }

    /// <summary>
    ///  Clear
    /// </summary>
    public void Clear()
    {
      if (Enable == false) return;
      LatestValue = 0;
      Que = new Queue<float>(Capacity);
    }

    /// <summary>
    ///  Enqueue
    /// </summary>
    public void Enqueue(float value)
    {
      if (Enable == false) return;
      if (IsFilled) Dequeue();
      LatestValue = value;
      Que.Enqueue(value);
    }

    /// <summary>
    ///  Dequeue
    /// </summary>
    public void Dequeue()
    {
      if (Enable == false) return;
      if (Que.Any()) Que.Dequeue();
      if (Que.Any() == false) LatestValue = 0;
    }

    /// <summary>
    ///  IsUnderThreshold
    /// </summary>
    public bool IsUnderThreshold
    {
      get
      {
        if (Enable == false) return true;        //無効なら常にtrueを返す
        if (IsFilled == false) return false;

        //Averageは完全な０．００にならないのでThresholdに０．０１加える。（特にＨＤＤ）
        if (Average < Threshold + 0.01)
          return true;
        else
          return false;
      }
    }
  }



}


