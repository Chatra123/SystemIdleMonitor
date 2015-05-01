using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;

namespace SystemIdleMonitor
{
  static class CheckProcess
  {
    static List<string> BlackList;
    static CheckProcess()
    {
      //パス
      string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
      string AppDir = Path.GetDirectoryName(AppPath);
      string AppName = Path.GetFileNameWithoutExtension(AppPath);
      string txtpath = Path.Combine(AppDir, AppName + ".txt");


      //initialize
      BlackList = new List<string>();

      //テキスト読込み
      if (File.Exists(txtpath) == false) return;
      var textList = File.ReadAllLines(txtpath).ToList();


      //BlackList作成、登録
      BlackList = textList;
      BlackList = BlackList.Select(
       (line) =>
       {  //コメント削除
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
       .Distinct()                                                   //重複排除
       .ToList();

    }



    //BlackListのプロセスが稼動していない？
    public static bool NotExistBlack()
    {
      var prclist = Process.GetProcesses();

      foreach (var pattern in BlackList)
        foreach (var prc in prclist)
        {
          //部分一致ではなく完全一致で検索
          try
          {
            var hitBlack = Regex.IsMatch(prc.ProcessName, "^" + pattern + "$", RegexOptions.IgnoreCase);
            if (hitBlack) return false;
          }
          catch
          {
            //patternが不正だと例外。notepad++など。
            continue;
          }
        }

      return true;
    }




  }
}
