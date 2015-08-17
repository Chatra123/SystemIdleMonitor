using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;


namespace SystemIdleMonitor
{
  static class Setting
  {
    public static List<string> ProcessList { get; private set; }
    public static string[] TextArgs { get; private set; }


    static Setting()
    {
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
      if (File.Exists(txtpath) == false)
      {
        File.WriteAllText(txtpath, SettingText.Default, Encoding.UTF8);
      }

      var readfile = File.ReadAllLines(txtpath).ToList();


      //前処理
      readfile = readfile.Select(
       (line) =>
       {
         //コメント削除
         int found = line.IndexOf("//");
         line = (0 <= found) ? line.Substring(0, found) : line;
         line = line.Trim();
         return line;
       })
       .Where((line) => string.IsNullOrWhiteSpace(line) == false)    //空白行削除
       .Distinct()                                                   //重複削除
       .ToList();


      //ProcessList
      ProcessList = readfile.Select(
       (line) =>
       {
         //.exe削除
         bool haveExe = (Path.GetExtension(line).ToLower() == ".exe");
         line = (haveExe) ? Path.GetFileNameWithoutExtension(line) : line;

         //ワイルドカード　→　正規表現
         line = Regex.Replace(line, @"\?", ".");
         line = Regex.Replace(line, @"\*", ".*");
         return line;
       })
       .ToList();


      //テキストからの引数
      TextArgs = new Func<List<string>, string[]>(
       (text) =>
       {
         //string "-cpu 30"をスペースで分割してList<string>に変換。
         //List<string>  →  List<List<string>>
         var T1 = text.Select(line =>
         {
           return line.Split(new char[] { ' ', '　', '\t' }).ToList();
         });

         //List<List<string>>  →  List<string>
         var T2 = T1.SelectMany(element => element)
                    .Where((line) => string.IsNullOrWhiteSpace(line) == false);  //空白行削除

         return T2.ToArray();
       })(readfile);
    }
  }

  //設定テキスト
  static class SettingText
  {
    public const string Default =
   @"
//
//### SystemIdleMonitorについて
//
//
//  * ＣＰＵ使用率が低いかを監視し、指定プロセスが動いていないことを確認します。
//
//  * ２０秒間、ＣＰＵ使用率６０％以下、ＨＤＤのＩＯが３０ＭｉＢ/ｓｅｃ以下ならリターンコード０。
//
//  * 負荷が高い又は、指定のプロセスが稼動しているとリターンコード１。
//
//
//
//### プロセスでフィルター
//
//  * プロセスのイメージ名はこのファイルの下部に書いてください。
//
//  * イメージ名はタスクマネージャーを見てください。
//
//  * 大文字小文字の違いは無視する。
//
//  * .exeがあればはずして評価する。
//
//  * ワイルドカードが使えます。
//　　　　０文字以上：　*　　　　１文字：　+
//
//  * ワイルドカードを正規表現に変換しているのでnotepad++はエラーとなり使えません。
//    ++を使わずnotepad*と指定してください。
//    他にも正規表現でエラーとなる文字列は使えません。
//
//
//
//### コマンドライン
//
//  * コマンドライン引数で閾値を変更できます。
//
//  * 実行ファイルの引数とは別に、このファイルにプロセス名と一緒に書くことができます。
//    ”実行ファイル”、”テキストファイル”両方で指定された引数は実行ファイルの引数が優先されます。
//
//　-cpu -1             ＣＰＵ使用率は計測しない。
//  -hdd 10             閾値          １０ＭｉＢ/ｓｅｃ    （読み書きの合計値）
//  -net 10                           １０Ｍｂｐｓ         （送信受信の合計値）
//  -duration 10        計測時間      １０秒
//  -timeout  60        処理中断      ６０秒
//
//
//
//### 文字コード
//
//  * UTF-8 bom
//
//
//

-cpu 60
-hdd 30
-net -1
-duration 20
-timeout  30 


ffmpeg
x264









";
  }

}
