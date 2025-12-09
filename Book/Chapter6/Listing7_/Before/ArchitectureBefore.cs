using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Book.Chapter6.Listing7_.Before
{
    public class AuditManager
    {
        private readonly int _maxEntriesPerFile;
        private readonly string _directoryName;

        public AuditManager(int maxEntriesPerFile, string directoryName)
        {
            _maxEntriesPerFile = maxEntriesPerFile;
            _directoryName = directoryName;
        }

        public void AddRecord(string visitorName, DateTime timeOfVisit)
        {
            // 訪問者記録ファイルががあるディレクトリからすべてのファイルパスを取得
            string[] filePaths = Directory.GetFiles(_directoryName);
            // 取得したパスをファイル名に含まれているインデックスをもとに並び替える
            // 全てのファイル名はaudit_{インデックス}.txtの形式になっている
            (int index, string path)[] sorted = SortByIndex(filePaths);

            // 訪問者の名前と訪問時刻を1行の文字列にまとめる
            // ("s") は フォーマット指定子、日時の形を指定。
            // s は「Sortable（並べ替え可能な）」形式＝ISO 8601形式。
            string newRecord = visitorName + ';' + timeOfVisit.ToString("s");

            // もし、訪問者記録ファイルが作られていなければ、新しいファイルを作成し、そこに最初の訪問者の記録を追加する
            if (sorted.Length == 0)
            {
                // Path.Combine() は、複数のパスをOSに合った区切り文字で結合する
                string newFile = Path.Combine(_directoryName, "audit_1.txt");
                // File は “実際にファイルを触るクラス”
                // 指定したファイルにテキストを書き込む（上書き）
                File.WriteAllText(newFile, newRecord);
                return;
            }

            // もし、既に訪問者記録ファイルが存在しているのであれば、
            // 最新のファイルを取得し、そのファイルに記された訪問者の記録の数が上限に達しているのかどうかを検証する
            (int currentFileIndex, string currentFilePath) = sorted.Last(); // タプルの分解代入
            // ファイルを行ごとの文字列配列として読み込む
            List<string> lines = File.ReadAllLines(currentFilePath).ToList();

            // もし、記録の数が上限に達していなければ、そのファイルに新しい訪問者の記録を追加する
            if (lines.Count < _maxEntriesPerFile)
            {
                lines.Add(newRecord);
                string newContent = string.Join("\r\n", lines);
                File.WriteAllText(currentFilePath, newContent);
            }
            // もし、記録の数が上限に達しているのであれば、新しいファイルを作成し、そこに新しい訪問者の記録を追加する
            else
            {
                int newIndex = currentFileIndex + 1;
                string newName = $"audit_{newIndex}.txt";
                string newFile = Path.Combine(_directoryName, newName);
                File.WriteAllText(newFile, newRecord);
            }
        }

        private (int index, string path)[] SortByIndex(string[] files)
        {
            return files
                .Select(path => (index: GetIndex(path), path))
                .OrderBy(x => x.index)
                .ToArray();
        }

        private int GetIndex(string filePath)
        {
            // File name example: audit_1.txt
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            return int.Parse(fileName.Split('_')[1]);
        }
    }
}
