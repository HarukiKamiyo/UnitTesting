using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Book.Chapter6.Listing7_.Functional
{
    // 関数的核
    public class AuditManager
    {
        private readonly int _maxEntriesPerFile;

        public AuditManager(int maxEntriesPerFile)
        {
            _maxEntriesPerFile = maxEntriesPerFile;
        }

        public FileUpdate AddRecord(
            FileContent[] files,
            string visitorName,
            DateTime timeOfVisit)
        {
            (int index, FileContent file)[] sorted = SortByIndex(files);

            string newRecord = visitorName + ';' + timeOfVisit.ToString("s");

            if (sorted.Length == 0)
            {
                // ファイルの更新に関する決定を返す
                return new FileUpdate("audit_1.txt", newRecord);
            }

            (int currentFileIndex, FileContent currentFile) = sorted.Last();
            List<string> lines = currentFile.Lines.ToList();

            if (lines.Count < _maxEntriesPerFile)
            {
                lines.Add(newRecord);
                string newContent = string.Join("\r\n", lines);
                // ファイルの更新に関する決定を返す
                return new FileUpdate(currentFile.FileName, newContent);
            }
            else
            {
                int newIndex = currentFileIndex + 1;
                string newName = $"audit_{newIndex}.txt";
                // ファイルの更新に関する決定を返す
                return new FileUpdate(newName, newRecord);
            }
        }

        private (int index, FileContent file)[] SortByIndex(
            FileContent[] files)
        {
            return files
                .Select(file => (index: GetIndex(file.FileName), file))
                .OrderBy(x => x.index)
                .ToArray();
        }

        private int GetIndex(string fileName)
        {
            // File name example: audit_1.txt
            string name = Path.GetFileNameWithoutExtension(fileName);
            return int.Parse(name.Split('_')[1]);
        }
    }

    // 「どのファイルに、どんな内容を書けばいいか」を表すデータ。
	// AuditManager が返す「決定結果」。
    //
    // 単なるデータの塊で呼び出し側で変更されないため struct にしてある。
    public struct FileUpdate
    {
        public readonly string FileName;
        public readonly string NewContent;

        public FileUpdate(string fileName, string newContent)
        {
            FileName = fileName;
            NewContent = newContent;
        }
    }

    // 「今あるファイル」の状態を表すクラス。
	// FileName と Lines（各行の文字列）を持つ。
	// これは Persister がファイルシステムから読み取って AuditManager に渡す。
    //
    // ファイル内容(可変な配列 Lines)を含み、複数の箇所で共有されるため class にしてある。
    public class FileContent
    {
        public readonly string FileName;
        public readonly string[] Lines;

        public FileContent(string fileName, string[] lines)
        {
            FileName = fileName;
            Lines = lines;
        }
    }

    // FileUpdate を受け、その決定に基づいた処理を行う(副作用を起こす)
    // つまり、可変殻
    //
    // 実際にファイルシステムにアクセスして読む／書く。
	// Directory.GetFiles や File.WriteAllText を使う。
    // つまり副作用（I/O）がここに閉じ込められてる。
    public class Persister
    {
        // 指定されたディレクトリからすべての訪問者記録ファイルを取得してそのファイルの内容を返す
        public FileContent[] ReadDirectory(string directoryName)
        {
            // Directory.GetFiles(directoryName)
            // → 指定したディレクトリの中にある「すべてのファイルのパス」を取得する
            //    例: ["C:\\logs\\audit_1.txt", "C:\\logs\\audit_2.txt"]

            return Directory
                .GetFiles(directoryName)

                // .Select(...) は LINQ(Language Integrated Query（言語統合クエリ）) のメソッドで、
                // 各ファイルパスに対して処理を行い、新しい形式に変換する（map 的なもの）
                .Select(x =>
                    // new FileContent(...) でファイル1つ分の内容をオブジェクト化
                    new FileContent(
                        // Path.GetFileName(x)
                        // → パスからファイル名だけを取り出す
                        //    "C:\\logs\\audit_1.txt" → "audit_1.txt"
                        Path.GetFileName(x),

                        // File.ReadAllLines(x)
                        // → ファイルを1行ずつ読み込んで string[] にする
                        //    例: ["Alice;2019-04-06T18:00:00", "Bob;2019-04-06T19:00:00"]
                        File.ReadAllLines(x)
                    )
                )

                // .ToArray()
                // → IEnumerable<FileContent> を FileContent[] に変換（最終的な配列にする）
                .ToArray();
        }

        // 決定に基づいたファイルの更新を対象のディレクトリで行う
        public void ApplyUpdate(string directoryName, FileUpdate update)
        {
            string filePath = Path.Combine(directoryName, update.FileName);
            File.WriteAllText(filePath, update.NewContent);
        }
    }

    // 全体をまとめて呼び出すエントリーポイント
    public class ApplicationService
    {
        private readonly string _directoryName;
        private readonly AuditManager _auditManager;
        private readonly Persister _persister;

        public ApplicationService(string directoryName, int maxEntriesPerFile)
        {
            _directoryName = directoryName;
            _auditManager = new AuditManager(maxEntriesPerFile);
            _persister = new Persister();
        }

        public void AddRecord(string visitorName, DateTime timeOfVisit)
        {
            // この時点でファイルの内容を全て読み込むため負荷がかかる
            FileContent[] files = _persister.ReadDirectory(_directoryName);
            FileUpdate update = _auditManager.AddRecord(
                files, visitorName, timeOfVisit);
            _persister.ApplyUpdate(_directoryName, update);
        }
    }

    public class Tests
    {
         // 現時点でのファイルが上限に達したときに、新しいファイルが作成される
        [Fact]
        public void A_new_file_is_created_when_the_current_file_overflows()
        {
            var sut = new AuditManager(3);
            var files = new FileContent[]
            {
                new FileContent("audit_1.txt", new string[0]),
                new FileContent("audit_2.txt", new string[]
                {
                    "Peter; 2019-04-06T16:30:00",
                    "Jane; 2019-04-06T16:40:00",
                    "Jack; 2019-04-06T17:00:00"
                })
            };

            FileUpdate update = sut.AddRecord(
                files, "Alice", DateTime.Parse("2019-04-06T18:00:00"));

            Assert.Equal("audit_3.txt", update.FileName);
            Assert.Equal("Alice;2019-04-06T18:00:00", update.NewContent);
            Assert.Equal(
                new FileUpdate("audit_3.txt", "Alice;2019-04-06T18:00:00"),
                update);
            update.Should().Be(
                new FileUpdate("audit_3.txt", "Alice;2019-04-06T18:00:00"));
        }
    }
}
