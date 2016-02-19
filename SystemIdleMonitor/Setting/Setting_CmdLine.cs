using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SystemIdleMonitor
{
  using Mono.Options;

  /// <summary>
  /// コマンドライン
  /// </summary>
  static class CommandLine
  {
    public static float CpuThd { get; private set; }
    public static float HddThd { get; private set; }
    public static float NetThd { get; private set; }
    public static float Duration { get; private set; }
    public static float Timeout { get; private set; }

    /// <summary>
    /// 初期値を設定する。
    /// </summary>
    public static void SetDefault(float[] defvalue)
    {
      CpuThd = defvalue[0];
      HddThd = defvalue[1];
      NetThd = defvalue[2];
      Duration = defvalue[3];
      Timeout = defvalue[4];
    }

    /// <summary>
    /// コマンドライン解析
    /// </summary>
    public static void Parse(string[] args)
    {
      //    /*  Mono.Options  */
      //オプションと説明、そのオプションの引数に対するアクションを定義する
      //OptionSet_icaseに渡すオプションは小文字にすること。
      //オプションの最後に=をつける。 bool型ならつけない。
      //判定は case insensitive

      var optionset = new OptionSet_icase();
      optionset
        .Add("cpu=", "CPU threashold", (float v) => CpuThd = v)
        .Add("cputhd=", "CPU threashold", (float v) => CpuThd = v)
        .Add("hdd=", "HDD  threashold", (float v) => HddThd = v)
        .Add("hddthd=", "HDD  threashold", (float v) => HddThd = v)
        .Add("net=", "Network threashold", (float v) => NetThd = v)
        .Add("netthd=", "Network threashold", (float v) => NetThd = v)

        .Add("dur=", "duration sec", (float v) => Duration = v)
        .Add("duration=", "duration sec", (float v) => Duration = v)
        .Add("timeout=", "timeout sec", (float v) => Timeout = v)
        .Add("and_more", "help mes", (v) => { });

      try
      {
        //パース仕切れなかったコマンドラインはList<string>で返される。
        var extra = optionset.Parse(args);
      }
      catch (OptionException)
      {
        //パース失敗
        return;
      }
    }
    

  }
}