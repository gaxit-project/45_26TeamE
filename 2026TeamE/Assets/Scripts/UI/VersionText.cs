using UnityEngine;
using TMPro;

/// <summary>
/// プロジェクト設定のバージョン（Player Settings の Version）をテキストに表示する。
/// バージョンを上げるときは Project Settings > Player > Version だけを変更すればよい。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VersionText : MonoBehaviour
{
    [Tooltip("{0} にバージョン番号が入ります")]
    [SerializeField] private string format = "Ver.{0}";

    private void Awake()
    {
        GetComponent<TMP_Text>().text = string.Format(format, Application.version);
    }
}
