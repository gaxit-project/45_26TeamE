using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        FixFile(@"Assets\Scripts\Manager\FinalResultManager.cs");
        FixFile(@"Assets\Scripts\Player\GoalJewelry.cs");
    }

    static void FixFile(string path) {
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Contains("[Header(") && !lines[i].Contains(")]")) {
                lines[i] = Regex.Replace(lines[i], @"\[Header\("".*", @"[Header(""Score UI"")]");
            }
            if (lines[i].Contains("[Header(") && lines[i].Contains(")]") && !lines[i].Contains(@""")]")) {
                lines[i] = Regex.Replace(lines[i], @"\[Header\(""(.*)\)\]", @"[Header("""")]");
            }
            if (lines[i].Contains("[Tooltip(") && lines[i].Contains(")]") && !lines[i].Contains(@""")]")) {
                lines[i] = Regex.Replace(lines[i], @"\[Tooltip\(""(.*)\)\]", @"[Tooltip("""")]");
            }
            if (lines[i].Contains("PlaySE") && lines[i].Contains("着水") && !lines[i].Contains(@""");")) {
                lines[i] = lines[i].Replace("着水EE", "着水音\");");
            }
        }
        File.WriteAllLines(path, lines);
    }
}
