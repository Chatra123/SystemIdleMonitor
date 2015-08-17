using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SystemIdleMonitor
{
  internal static class CheckProcess
  {
    static List<string> BlackList;

    static CheckProcess()
    {
      BlackList = BlackList ?? Setting.ProcessList;
    }

    /// <summary>
    /// 特定のプロセスが稼動していないか？
    /// </summary>
    public static bool NotExistBlack()
    {
      var activePrcs = Process.GetProcesses();

      foreach (var pattern in BlackList)         //監視するプロセス名のパターン
        foreach (var prc in activePrcs)          //稼動中のプロセス
        {
          //部分一致ではなく完全一致で検索
          try
          {
            var hit = Regex.IsMatch(prc.ProcessName, "^" + pattern + "$", RegexOptions.IgnoreCase);
            if (hit) return false;              //特定のプロセスが稼動中
          }
          catch
          {
            //patternが不正だと例外。notepad++など。
            continue;
          }
        }

      return true;                               //特定のプロセスが稼動していない
    }
  }
}