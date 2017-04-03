using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SystemIdleMonitor
{


  #region CounterFactory

  public enum CounterList
  {
    Processor,
    ProcessCPU,
    HDD_Read,
    HDD_Write,
    HDD_Transfer,
    Network_Sent,
    Network_Recive,
    Network_Transfer,
  }

  public static class CategoryList
  {
    public const string Processor = "Processor", HDD = "PhysicalDisk", Network = "Network Interface";
  }

  public static class InstanceList
  {
    public const string Total = "_Total";
  }

  internal static class CounterFactory
  {
    /// <summary>
    /// PerformanceCounterを作成
    /// </summary>
    public static PerformanceCounter Create(CounterList counterName)
    {
      string[] nameset = GetCounterNameSet(counterName);
      // ex.   perfcnt = new PerformanceCounter(categoryName, counterName, instanceName);
      return new PerformanceCounter(nameset[0], nameset[1], nameset[2]);
    }

    /// <summary>
    /// PerformanceCounterを作成
    /// </summary>
    public static PerformanceCounter Create(CounterList counterName, string insName)
    {
      string[] nameset = GetCounterNameSet(counterName);
      return new PerformanceCounter(nameset[0], nameset[1], insName);
    }

    /// <summary>
    /// CounterListから  categoryName	 counterName  instanceName  を取得
    /// </summary>
    private static string[] GetCounterNameSet(CounterList counter)
    {
      string[] nameset = null;

      switch (counter)
      {
        case CounterList.Processor:
          nameset = new string[] { "Processor", "% Processor Time", "_Total" };
          break;

        case CounterList.ProcessCPU:
          nameset = new string[] { "Process", "% Processor Time", "" };
          break;

        case CounterList.HDD_Read:
          nameset = new string[] { "PhysicalDisk", "Disk Read Bytes/sec", "" };
          break;

        case CounterList.HDD_Write:
          nameset = new string[] { "PhysicalDisk", "Disk Write Bytes/sec", "" };
          break;

        case CounterList.HDD_Transfer:
          nameset = new string[] { "PhysicalDisk", "Disk Transfers/sec", "" };
          break;

        case CounterList.Network_Sent:
          nameset = new string[] { "Network Interface", "Bytes Sent/sec", "" };
          break;

        case CounterList.Network_Recive:
          nameset = new string[] { "Network Interface", "Bytes Received/sec", "" };
          break;

        case CounterList.Network_Transfer:
          nameset = new string[] { "Network Interface", "Bytes Total/sec", "" };
          break;

        default:
          break;
      }

      return nameset;
    }

    public static List<string> GetInstanceTable(string categoryName)
    {
      return new PerformanceCounterCategory(categoryName).GetInstanceNames().ToList();
    }

    /// <summary>
    /// ＰＩＤからプロセスのインスタンス名取得
    /// 複数のインスタンスがあると  notepad   notepad#1  notepad#2  になる。  
    /// </summary>
    public static String GetInstanceName_ById(int pid)
    {
      string prcName;
      try
      {
        prcName = Process.GetProcessById(pid).ProcessName;
      }
      catch (ArgumentException)  //プロセスが稼動していない。
      {
        return null;
      }

      var category = new PerformanceCounterCategory("Process");
      if (category.CounterExists("ID Process") == false) return null;

      var inslist = category.GetInstanceNames();
      inslist = inslist.Where(name => name.Contains(prcName)).ToArray();

      foreach (string instance in inslist)
      {
        using (var counter = new PerformanceCounter("Process", "ID Process", instance))
        {
          if (pid == counter.RawValue)
          {
            return instance;
          }
        }
      }

      return null;
    }
  }

  #endregion CounterFactory




  internal class SystemCounter
  {

    #region PreLoad
    /// <summary>
    /// PerformanceCounterの作成をバックグラウンドで処理しておく。
    /// </summary>
    /// <param name="delay">遅延時間　ms</param>
    /// <remarks> PerformanceCounterの作成は初回のみ数秒かかる。</remarks>
    public static void PreLoad(int delay = 0)
    {
      System.Threading.Tasks.Task.Factory.StartNew(() =>
      {
        System.Threading.Thread.Sleep(delay);
        CounterFactory.Create(CounterList.Processor);
      });
    }
    #endregion

    public ProcessorCounter Processor;
    public ProcessCPUCounter IdleProcess;
    public HddCounterSet HDD;
    public NetworkCounterSet Network;

    /// <summary>
    /// constructor
    /// </summary>
    public SystemCounter()
    {
      Processor = new ProcessorCounter();
      IdleProcess = new ProcessCPUCounter();
      IdleProcess.Create(0, "Idle");
      HDD = new HddCounterSet();
      Network = new NetworkCounterSet();
    }


    #region Processor
    /// <summary>
    /// ＣＰＵ使用率を取得する。
    /// </summary>
    public class ProcessorCounter
    {
      private PerformanceCounter Processor;

      /// <summary>
      /// constructor
      /// </summary>
      public ProcessorCounter()
      {
        Processor = CounterFactory.Create(CounterList.Processor);
        Usage();             //１回目のNextValueは０が返されるのでここで実行する。
      }

      /// <summary>
      /// PC全体のＣＰＵ使用率を取得する。
      /// </summary>
      public float Usage()
      {
        try { return Processor.NextValue(); }
        catch { return 0; }
      }
    }

    #endregion


    #region ProcessCPU
    /// <summary>
    /// プロセス単体のＣＰＵ使用率を取得する。
    /// </summary>
    public class ProcessCPUCounter
    {
      public int Id { get; private set; }                  //PID
      public string InsName { get; private set; }          //インスタンス名
      public bool IsAlive { get; private set; }            //プロセスの生存
      private readonly int cpu_count = Environment.ProcessorCount;  //ＣＰＵコア数
      private PerformanceCounter PrcCpuCounter;

      /// <summary>
      /// ＰＩＤからカウンター作成
      /// </summary>
      public bool Create(int pid)
      {
        string name = CounterFactory.GetInstanceName_ById(pid);
        if (string.IsNullOrEmpty(name) == false)
        {
          Create(pid, name);
        }
        return IsAlive;
      }

      /// <summary>
      /// インスタンス名からカウンター作成
      /// </summary>
      public bool Create(int pid, string insname)
      {
        Id = pid;
        InsName = insname;
        IsAlive = true;

        PrcCpuCounter = CounterFactory.Create(CounterList.ProcessCPU, InsName);
        Usage();          //１回目のNextValueは０が返されるのでここで実行する。
        return IsAlive;
      }

      /// <summary>
      /// ＣＰＵ使用率を取得する。
      /// </summary>
      public float Usage()
      {
        if (IsAlive == false) return 0;

        try { return PrcCpuCounter.NextValue() / cpu_count; }
        catch { IsAlive = false; return 0; }
      }
    }
    #endregion


    #region HDD
    /// <summary>
    /// ＨＤＤの転送量を取得する。
    /// </summary>
    public class HddCounterSet
    {
      public List<HDDCounter> List;
      public HDDCounter Total;
      public HDDCounter this[string driveLetter]
      {
        get { return GetCounterByName(driveLetter); }
      }
      public BytePerSec BytePerSec { get; private set; } = BytePerSec.Bps;


      /// <summary>
      /// HDDCounter
      /// </summary>
      public HddCounterSet()
      {
        Total = new HDDCounter(InstanceList.Total);
        List = new List<HDDCounter>();
        var table = CounterFactory.GetInstanceTable(CategoryList.HDD);
        foreach (var insName in table)
          if (insName.ToLower().Contains(InstanceList.Total.ToLower()) == false)
            List.Add(new HDDCounter(insName));          //_Totalでないなら追加
      }

      /// <summary>
      /// Set BytePerSec
      /// </summary>
      public void SetPrefix(BytePerSec prefix)
      {
        BytePerSec = prefix;
        foreach (var hdd in List)
          hdd.SetPrefix(prefix);
      }

      /// <summary>
      /// ドライブ名からHDDCounter取得
      /// </summary>
      private HDDCounter GetCounterByName(string driveLetter)
      {
        driveLetter = driveLetter.ToLower();
        if (driveLetter.Contains("total")) return Total;

        driveLetter = System.Text.RegularExpressions.Regex.Match(driveLetter, "[A-Za-z]").Value;
        foreach (HDDCounter hdd in List)
          if (hdd.InsName.ToLower().Contains(driveLetter))
            return hdd;
        return null;
      }


      /// <summary>
      ///  FixedDriveを検索
      /// </summary>
      public List<string> GetFixedDrive()
      {
        var fixedDrives = new List<string>();
        foreach (var drive in Environment.GetLogicalDrives())
        {
          DriveInfo di = new DriveInfo(drive);
          if (di.DriveType == DriveType.Fixed)
            fixedDrives.Add(di.ToString());
        }
        return fixedDrives;
      }

      /// <summary>
      /// 全ＨＤＤの読込み速度を取得
      /// </summary>
      public float TotalRead()
      {
        return Total.Read();
      }

      /// <summary>
      /// 全ＨＤＤの書込み速度を取得
      /// </summary>
      public float TotalWrite()
      {
        return Total.Write();
      }

      /// <summary>
      /// 全ＨＤＤの転送速度を取得
      /// </summary>
      public float TotalTransfer()
      {
        return Total.Transfer();
      }
    }

    /// <summary>
    /// ドライブ単体の転送速度を取得する。
    /// </summary>
    public class HDDCounter
    {
      public string InsName { get; private set; }     //ＨＤＤのインスタンス名　( _Total, C:\, D:\ )
      public PerformanceCounter readCounter, writeCounter, transferCounter;
      public BytePerSec BytePerSec { get; private set; } = BytePerSec.Bps;

      /// <summary>
      /// constructor
      /// </summary>
      public HDDCounter(string insName)
      {
        InsName = insName;
        readCounter = CounterFactory.Create(CounterList.HDD_Read, insName);
        writeCounter = CounterFactory.Create(CounterList.HDD_Write, insName);
        transferCounter = CounterFactory.Create(CounterList.HDD_Transfer, insName);
        Read();     //１回目のNextValueは０が返されるのでここで実行する。
        Write();
        Transfer();
      }

      /// <summary>
      /// Set BytePerSec
      /// </summary>
      public void SetPrefix(BytePerSec prefix)
      {
        BytePerSec = prefix;
      }

      /// <summary>
      /// HDD単体の読込み速度を取得
      /// </summary>
      public float Read()
      {
        try { return Prefix.Convert(readCounter.NextValue(), (SIPrefix)BytePerSec); }
        catch { return 0; }
      }

      /// <summary>
      /// HDD単体の書込み速度を取得
      /// </summary>
      public float Write()
      {
        try { return Prefix.Convert(writeCounter.NextValue(), (SIPrefix)BytePerSec); }
        catch { return 0; }
      }

      /// <summary>
      /// HDD単体の転送速度を取得
      /// </summary>
      public float Transfer()
      {
        try { return Prefix.Convert(transferCounter.NextValue(), (SIPrefix)BytePerSec); }
        catch { return 0; }
      }
    }
    #endregion HDD


    #region Network
    /// <summary>
    /// ネットワークの転送速度を取得
    /// </summary>
    public class NetworkCounterSet
    {
      public List<NetworkCounter> List;
      public bitPerSec bitPerSec { get; private set; } = bitPerSec.bps;
      public NetworkCounter this[string nicName]
      {
        get { return GetCounterByName(nicName); }
      }

      /// <summary>
      /// constructor
      /// </summary>
      public NetworkCounterSet()
      {
        var table = CounterFactory.GetInstanceTable(CategoryList.Network);
        List = new List<NetworkCounter>();
        foreach (string insName in table)
          List.Add(new NetworkCounter(insName));
      }

      /// <summary>
      /// Set bitPerSec
      /// </summary>
      public void SetPrefix(bitPerSec prefix)
      {
        bitPerSec = prefix;
        foreach (var nic in List)
          nic.SetPrefix(prefix);
      }

      /// <summary>
      /// nic名からNetworkCounter取得
      /// </summary>
      private NetworkCounter GetCounterByName(string nicName)
      {
        foreach (NetworkCounter counter in List)
          if (counter.InsName.ToLower().Contains(nicName.ToLower()))
            return counter;
        return null;
      }

      /// <summary>
      /// Nicカード名取得
      /// </summary>
      public List<string> GetNicList()
      {
        var list = List.Select((counter) => counter.InsName).ToList();
        return list;
      }

      /// <summary>
      /// 全ネットワークカードの受信速度を取得
      /// </summary>
      public float Receive()
      {
        return List.Select((counter) => counter.Receive()).Sum();
      }

      /// <summary>
      /// 全ネットワークカードの送信速度を取得
      /// </summary>
      public float Sent()
      {
        return List.Select((counter) => counter.Sent()).Sum();
      }

      /// <summary>
      /// 全ネットワークカードの転送速度を取得
      /// </summary>
      public float Transfer()
      {
        return List.Select((counter) => counter.Transfer()).Sum();
      }
    }

    /// <summary>
    /// ネットワークカード単体の転送速度を取得する。
    /// </summary>
    public class NetworkCounter
    {
      public string InsName { get; private set; }
      private PerformanceCounter receiveCounter, sentCounter, transferCounter;
      public bitPerSec bitPerSec { get; private set; } = bitPerSec.bps;

      /// <summary>
      /// constructor
      /// </summary>
      public NetworkCounter(string insName)
      {
        InsName = insName;
        receiveCounter = CounterFactory.Create(CounterList.Network_Recive, insName);
        sentCounter = CounterFactory.Create(CounterList.Network_Sent, insName);
        transferCounter = CounterFactory.Create(CounterList.Network_Transfer, insName);
        Receive();        //１回目のNextValueは０が返されるのでここで実行する。
        Sent();
        Transfer();
      }

      /// <summary>
      /// Set bitPerSec
      /// </summary>
      public void SetPrefix(bitPerSec prefix)
      {
        bitPerSec = prefix;
      }

      /// <summary>
      /// ネットワークカード単体の受信速度を取得
      /// </summary>
      /// <returns>受信速度</returns>
      public float Receive()
      {
        // Byte/secをbit/secに変換するために * 8
        try { return Prefix.Convert(receiveCounter.NextValue() * 8, (SIPrefix)bitPerSec); }
        catch { return 0; }
      }

      /// <summary>
      /// ネットワークカード単体の送信速度を取得
      /// </summary>
      /// <returns>送信速度</returns>
      public float Sent()
      {
        try { return Prefix.Convert(sentCounter.NextValue() * 8, (SIPrefix)bitPerSec); }
        catch { return 0; }
      }

      /// <summary>
      /// ネットワークカード単体の転送速度を取得
      /// </summary>
      /// <returns>転送速度</returns>
      public float Transfer()
      {
        try { return Prefix.Convert(transferCounter.NextValue() * 8, (SIPrefix)bitPerSec); }
        catch { return 0; }
      }
    }
    #endregion Network

  }//class






  #region Prefix
  internal enum Byte { B = 0, KiB = 1, MiB = 2, }
  internal enum bit { b = 0, Kib = 1, Mib = 2, }
  internal enum BytePerSec { Bps = 0, KiBps = 1, MiBps = 2, }
  internal enum bitPerSec { bps = 0, Kibps = 1, Mibps = 2, }
  internal enum SIPrefix { none = 0, K = 1, M = 2, }

  internal static class Prefix
  {
    /// <summary>
    /// 指定のPrefixで丸める
    /// </summary>
    /// <param name="value">対象の値</param>
    /// <param name="prefix">Prefix値で丸められる</param>
    public static float Convert(float value, SIPrefix prefix)
    {
      for (int i = 0; i < (int)prefix; i++)
        value /= 1024;
      return value;
    }

    /// <summary>
    /// 適切なPrefixで丸める
    /// </summary>
    /// <param name="value">対象の値</param>
    /// <param name="prefix">使用されたPrefix値</param>
    private static float AutoOptimizePrefix(float value, out SIPrefix prefix)
    {
      int iprefix;
      for (iprefix = 0; iprefix < 2; iprefix++)
      {
        if (value < 1024) break;
        value /= 1024;
      }
      prefix = (SIPrefix)iprefix;
      return value;
    }
  }

  #endregion Prefix


}