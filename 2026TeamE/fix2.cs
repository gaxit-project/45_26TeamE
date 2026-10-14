using System.IO;

class Program {
    static void Main() {
        var path = @"Assets\Scripts\Player\GoalJewelry.cs";
        var lines = File.ReadAllLines(path);
        lines[62] = "            SoundManager.Instance.PlaySE(\"着水音\");";
        File.WriteAllLines(path, lines);
    }
}
