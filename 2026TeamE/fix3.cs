using System.IO;

class Program {
    static void Main() {
        {
            var path = @"Assets\Scripts\Manager\FinalResultManager.cs";
            var text = File.ReadAllText(path);
            text = text.Replace("// 3つの要素のスコア計箁E        float totalOxygen = 0f;", "// Score calculation\n        float totalOxygen = 0f;");
            File.WriteAllText(path, text);
        }
        {
            var path = @"Assets\Scripts\Player\GoalJewelry.cs";
            var text = File.ReadAllText(path);
            text = text.Replace("// プレイヤーの操作を無効匁E            PlayerController player = other.GetComponent<PlayerController>();", "// Disable player input\n            PlayerController player = other.GetComponent<PlayerController>();");
            text = text.Replace("// タイマEを停止Eクリア演E中に時間刁EになるEを防ぐ！E            if (TimerManager.Instance != null)", "// Stop timer\n            if (TimerManager.Instance != null)");
            File.WriteAllText(path, text);
        }
    }
}
