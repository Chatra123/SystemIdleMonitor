using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace SystemIdleMonitor.Sample
{
  class SystemCounter_SampleCode
  {
    /// <summary>
    /// ＣＰＵ使用率、ＨＤＤ転送速度、ネットワーク転送速度を表示
    /// </summary>
    public static void Run_SystemCounter_1()
    {
      //起動に数秒かかる
      var systemCounter = new SystemCounter();
      systemCounter.HDD.SetPrefix(BytePerSec.MiBps);
      systemCounter.Network.SetPrefix(bitPerSec.Mibps);

      while (true)
      {
        float CPU = systemCounter.Processor.Usage();
        float HDD_read = systemCounter.HDD.Total.Read();
        float Net_down = systemCounter.Network.Receive();

        string line = string.Format(
          "  {0,6:f2} %    {1,6:f2} KiB/s    {2,6:f2} Kibps",
          CPU, HDD_read, Net_down);
        Console.WriteLine(line);
        Thread.Sleep(1000);
      }
    }

    /// <summary>
    /// "mpc-be64"のプロセスＣＰＵ使用率を表示
    /// </summary>
    public static void Run_SystemCounter_2()
    {
      //mpc-be64.exeを２つ起動してから実行する
      const string MediaPlayer = "mpc-be64";

      //プロセス名からＰＩＤ取得
      var mplayer_list = Process.GetProcessesByName(MediaPlayer);
      if (mplayer_list.Count() <= 1) throw new Exception();  //"mpc-be64"が２つ起動していない
      int pid0 = mplayer_list[0].Id;
      int pid1 = mplayer_list[1].Id;

      //起動に数秒かかる
      var systemCounter = new SystemCounter();
      var counter0 = new SystemCounter.ProcessCPUCounter();
      var counter1 = new SystemCounter.ProcessCPUCounter();
      counter0.Create(pid0);
      counter1.Create(pid1);

      while (true)
      {
        float mplayer_0 = counter0.Usage();
        float mplayer_1 = counter1.Usage();
        float cpu_idle = systemCounter.IdleProcess.Usage();

        string line = string.Format(
          "  {0,6:f2} %    {1,6:f2} %        {2,6:f2} %",
          mplayer_0, mplayer_1, cpu_idle);
        Console.WriteLine(line);
        Thread.Sleep(1000);
      }

    }
  }










}




