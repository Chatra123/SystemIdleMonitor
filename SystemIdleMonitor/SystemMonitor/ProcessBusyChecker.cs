using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{

  /// <summary>
  /// プロセスのＣＰＵ使用率が高いかを判定する。
  /// </summary>
  public class ProcessBusyChecker
  {
    SystemCounter systemCounter;
    SystemCounter.ProcessCPUCounter processCounter;

    int Process_CPU_Max, System__CPU_Max;


    /// <summary>
    /// initialize
    /// </summary>
    public ProcessBusyChecker(int pid, int process_CPU_Max, int system__CPU_Max)
    {
      //pid = -1 ならprocessCounterの作成に失敗して、
      //ProcessのＣＰＵ使用率に関しては評価されない。
      System__CPU_Max = system__CPU_Max;
      Process_CPU_Max = process_CPU_Max;

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












