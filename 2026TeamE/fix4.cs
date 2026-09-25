using System.IO;

class Program {
    static void Main() {
        {
            var path = @"Assets\Scripts\Manager\FinalResultManager.cs";
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].Contains("totalOxygen = 0f;")) {
                    lines[i] = "        float totalOxygen = 0f;";
                }
            }
            File.WriteAllLines(path, lines);
        }
        {
            var path = @"Assets\Scripts\Player\GoalJewelry.cs";
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].Contains("PlayerController player = other.GetComponent<PlayerController>();")) {
                    lines[i] = "            PlayerController player = other.GetComponent<PlayerController>();";
                }
                if (lines[i].Contains("if (TimerManager.Instance != null)") && lines[i].Contains("E")) {
                    lines[i] = "            if (TimerManager.Instance != null)";
                }
            }
            File.WriteAllLines(path, lines);
        }
    }
}
