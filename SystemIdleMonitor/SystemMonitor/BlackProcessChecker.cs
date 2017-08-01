using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SystemIdleMonitor
{

  /// <summary>
  /// 特定のプロセスが稼動しているか判定
  /// </summary>
  public class BlackProcessChecker
  {
    List<string> BlackList;  //Regex pattern

    /// <summary>
    /// constructor
    /// </summary>
    public BlackProcessChecker(List<string> blacklist)
    {
      BlackList = blacklist;
    }

    /// <summary>
    /// ブラックプロセスが稼動していないか？
    /// </summary>
    public bool NotExistBlack()
    {
      var all = Process.GetProcesses();

      foreach (var pattern in BlackList)   //Regex pattern
        foreach (var prc in all)           //全プロセス
        {
          //部分一致ではなく完全一致で検索
          try
          {
            var hit = Regex.IsMatch(prc.ProcessName, "^" + pattern + "$", RegexOptions.IgnoreCase);
            if (hit) return false;               //検出
          }
          catch
          {
            //patternが不正だと例外。notepad++など。
            continue;
          }
        }
      return true;                               //未検出
    }

    /// <summary>
    /// ブラックプロセスが稼動している？
    /// </summary>
    public bool ExistBlack()
    {
      return !NotExistBlack();
    }



  }
}