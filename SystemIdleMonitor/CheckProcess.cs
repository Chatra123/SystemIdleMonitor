using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SystemIdleMonitor
{
  /// <summary>
  /// 特定のプロセスが動いているかチェックする。
  /// </summary>
  internal static class CheckProcess
  {
    public static List<string> ProcessList { get; private set; }

    static CheckProcess()
    {
      ProcessList = new List<string>();

      //ファイルパス
      string txtpath = new Func<string>
        (() =>
        {
          string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
          string AppDir = Path.GetDirectoryName(AppPath);
          string AppName = Path.GetFileNameWithoutExtension(AppPath);
          return Path.Combine(AppDir, AppName + ".txt");
        })();

      //読込み
      if (File.Exists(txtpath) == false) return;
      var textList = File.ReadAllLines(txtpath).ToList();

      //ProcessList作成、登録
      ProcessList = textList;
      ProcessList = ProcessList.Select(
       (line) =>
       {
         //コメント削除
         int found = line.IndexOf("//");
         line = (-1 < found) ? line.Substring(0, found) : line;
         line = line.Trim();

         //.exe削除
         bool haveExe = (Path.GetExtension(line).ToLower() == ".exe");
         line = (haveExe) ? Path.GetFileNameWithoutExtension(line) : line;

         //ワイルドカードを正規表現に変換
         line = Regex.Replace(line, @"\?", ".");
         line = Regex.Replace(line, @"\*", ".*");

         return line;
       })
       .Where((line) => string.IsNullOrWhiteSpace(line) == false)    //空白行削除
       .Distinct()                                                   //重複削除
       .ToList();
    }

    /// <summary>
    /// 特定のプロセスが稼動していない？
    /// </summary>
    /// <returns>プロセスが稼動していないか</returns>
    public static bool NotExistBlack()
    {
      var curPrcList = Process.GetProcesses();

      foreach (var pattern in ProcessList)       //監視するプロセス名のパターン
        foreach (var prc in curPrcList)          //稼動中のプロセス
        {
          //部分一致ではなく完全一致で検索
          try
          {
            var hitBlack = Regex.IsMatch(prc.ProcessName, "^" + pattern + "$", RegexOptions.IgnoreCase);
            if (hitBlack) return false;          //特定のプロセスが稼動中
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