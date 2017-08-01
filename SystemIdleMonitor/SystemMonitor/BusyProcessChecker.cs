using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{

  /// <summary>
  /// プロセスのＣＰＵ使用率が高いかを判定
  /// </summary>
  public class BusyProcessChecker
  {
    readonly object sync = new object();
    SystemCounter systemCounter;
    SystemCounter.ProcessCPUCounter processCounter;
    int Process_CPU_Max, System__CPU_Max;

    /// <summary>
    /// constructor
    /// </summary>
    public BusyProcessChecker(int sys_CPU_Max, int prc_CPU_Max, int pid)
    {
      //pid = -1 ならProcessのＣＰＵ使用率は評価しない。
      System__CPU_Max = sys_CPU_Max;
      Process_CPU_Max = prc_CPU_Max;
      systemCounter = new SystemCounter();
      processCounter = new SystemCounter.ProcessCPUCounter();
      processCounter.Create(pid);
    }

    /// <summary>
    /// プロセスのＣＰＵ使用率が閾値を越えているか？
    /// </summary>
    public bool IsBusy()
    {
      float prc = processCounter.Usage();
      float system = systemCounter.Processor.Usage();
      if (Process_CPU_Max < prc || System__CPU_Max < system)
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    /// <summary>
    /// 閾値以下か？
    /// </summary>
    public bool NotBusy()
    {
      return !IsBusy();
    }


  }


}












