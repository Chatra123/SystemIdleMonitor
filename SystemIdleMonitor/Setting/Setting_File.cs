using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;


namespace SystemIdleMonitor
{
  public class Setting_File
  {
    public List<string> ProcessList { get; private set; }
    public string[] TextFileArgs { get; private set; }

    //テキストパス
    //    外部プロジェクト Valve2Pipeから呼び出してもAppNameはSystemIdleMonitor.exeのまま。
    static readonly string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
                           AppDir = Path.GetDirectoryName(AppPath),
                           AppName = Path.GetFileNameWithoutExtension(AppPath),
                           Default_XmlName = AppName + ".xml",
                           Default_XmlPath = Path.Combine(AppDir, Default_XmlName);

    readonly string SIM_Default_Path = Path.Combine(AppDir, AppName + ".txt");

    /// <summary>
    /// 設定ファイル読込み
    /// </summary>
    public void Load(string path = null, string Default_Text = null)
    {
      path = path ?? SIM_Default_Path;
      Default_Text = Default_Text ?? Setting_Text_Default.SystemIdleMonitor;

      //読込み
      if (File.Exists(path) == false)
      {
        File.WriteAllText(path, Default_Text, Encoding.UTF8);
      }
      var readfile = File.ReadAllLines(path).ToList();

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


      //ファイルからの引数
      {
        //string "-cpu 30"をスペースで分割してList<string>に変換。
        //List<string>  →  List<List<string>>
        var L1 = readfile.Select(line =>
        {
          return line.Split(new char[] { ' ', '　', '\t' }).ToList();
        });

        //List<List<string>>  →  List<string>
        var L2 = L1.SelectMany(element => element)
                    .Where((line) => string.IsNullOrWhiteSpace(line) == false);  //空白行削除

        TextFileArgs = L2.ToArray();
      }

    }
  }

  //設定テキスト
  public static class Setting_Text_Default
  {
    public const string SystemIdleMonitor =
   @"
//
//### SystemIdleMonitorについて
//
//  * ＣＰＵ使用率が低いかを監視し、指定プロセスが動いていないことも確認します。
//
//  * １０秒間の平均ＣＰＵ使用率が６０％以下、ＨＤＤのＩＯが３０ＭｉＢ/ｓｅｃ以下ならリターンコード０。
//
//  * 負荷が高い又は、指定のプロセスが稼動しているとリターンコード１。
//
//
//
//### プロセスでフィルター
//
//  * プロセスのイメージ名はこのファイルの下部に書いてください。
//    イメージ名はタスクマネージャーを見てください。
//
//  * 大文字小文字の違いは無視する。
//    全角半角、ひらがなカタカナは区別する。
//
//  * 拡張子.exeが付いていたら無視して評価します。
//
//  * ワイルドカードが使えます。
//        ０文字以上：  *        １文字：  +
//
//  * ワイルドカードを正規表現に変換しているのでnotepad++はエラーとなり使えません。
//    notepad++でなくnotepad*と指定してください。
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
//  -cpu      60        閾値        ６０％
//  -hdd      30                    ３０ＭｉＢ/ｓｅｃ     （読書きの合計値）
//  -net      -1                    計測しない  Ｍｂｐｓ  （送受信の合計値）
//  -duration 10        計測時間    １０秒
//  -timeout  20        処理中断    ２０秒
//
//
//
//### 文字コード
//
//  * このテキストの文字コード　UTF-8 bom
//
//
//

-cpu      60
-hdd      30
-net      -1
-duration 10
-timeout  20 





";
  }

}
