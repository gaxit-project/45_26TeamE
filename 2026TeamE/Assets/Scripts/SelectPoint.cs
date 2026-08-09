using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class SelectPoint : MonoBehaviour
{
    [Header("選択パネルのタグ名")]
    [SerializeField] private string panelTag = "SelectPanel";
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private GameObject firstSelectButton;

    [Header("シーン移動用のボタン設定")]
    [SerializeField] private InputAction jumpSceneAction;

    [Header("UIの表示位置調整")]
    [SerializeField] private Vector3 uiOffset = new Vector3(-2.0f, 2.0f, 0);
    private void Start()
    {
        FindPanelInScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (jumpSceneAction != null) jumpSceneAction.Enable();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (jumpSceneAction != null) jumpSceneAction.Disable();
    }

    private void Update()
    {
        if (VoxelTerrain.Instance == null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // プレイヤーの足元のY座標を取得
            Vector3 localPos = VoxelTerrain.Instance.transform.InverseTransformPoint(player.transform.position);
            float s = VoxelTerrain.Instance.BlockSize;
            int py = Mathf.FloorToInt(localPos.y / s);
            // ★変更点：新しい岩盤判定メソッドを直接使う
            bool isTouchingBedrock = VoxelTerrain.Instance.IsRelayZoneBottom(py) || VoxelTerrain.Instance.IsRelayZoneBottom(py - 1);
            if (isTouchingBedrock)
            {
                // 岩盤にいる間、UIが出ていなければ出す
                if (selectPanel != null && !selectPanel.activeSelf)
                {
                    // チェックポイント保存などの処理
                    VoxelTerrain.Instance.OnPlayerReachRelayPoint(py);
                    ShowButton(); // UI表示
                }
                if (jumpSceneAction != null && jumpSceneAction.WasPressedThisFrame())
                {
                    // プレイヤーのいる深さから、今のゾーンのIDを取得
                    int currentID = VoxelTerrain.Instance.GetRelayID(py);
                    // 鍵が3つ集まっているか（クリアしているか）確認
                    bool hasKeys = VoxelTerrain.Instance.IsZoneCleared(currentID);
                    if (hasKeys)
                    {
                        // 鍵が足りていればリザルトへ！
                        GoToResult();
                    }
                    else
                    {
                        // 鍵が足りない場合は、さっき作った警告アニメーションを呼ぶ
                        if (KeyUIController.Instance != null)
                        {
                            KeyUIController.Instance.ShowWarning();
                        }
                    }
                }
                if (selectPanel != null && selectPanel.activeSelf && Camera.main != null)
                {
                    Vector3 worldPos = player.transform.position + uiOffset;

                    // 3D空間の座標を、カメラから見た画面上の2D座標に変換
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                    // UIパネルの位置を更新（※画面の裏側にいる時のバグを防ぐため、zが0以上の時だけ移動）
                    if (screenPos.z > 0)
                    {
                        selectPanel.transform.position = screenPos;
                    }
                }
            }
            else
            {
                // 岩盤から離れたらUIを隠す
                if (selectPanel != null && selectPanel.activeSelf)
                {
                    selectPanel.SetActive(false);
                }
            }
            
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindPanelInScene();
    }

    private void FindPanelInScene()
    {
        // シーン内の指定したタグ（SelectPanel）を持つオブジェクトを検索
        GameObject foundPanel = GameObject.FindWithTag(panelTag);
        if (foundPanel != null)
        {
            selectPanel = foundPanel;

            // 最初に選択するボタンとして、子要素にある「GoResult」を自動設定
            if (firstSelectButton == null)
            {
                Transform btnTransform = selectPanel.transform.Find("GoResult");
                if (btnTransform != null)
                {
                    firstSelectButton = btnTransform.gameObject;
                }
            }

            selectPanel.SetActive(false);
        }
    }

    public void ShowButton()
    {
        if (selectPanel == null) FindPanelInScene();

        if (selectPanel != null)
        {
            
            selectPanel.SetActive(true);
            //ボタンを選べる必要はないのでコメントアウト
            /*
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            if (firstSelectButton != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectButton);
            }
            */
        }
        else
        {
            Debug.LogError($"新しいシーンでタグ '{panelTag}' が設定されたオブジェクトが見つかりませんでした。");
        }
    }

    public void GoToResult()
    {
        StartCoroutine(GoToResultCoroutine());
    }

    private IEnumerator GoToResultCoroutine()
    {

        // BGMを止める（リザルト画面での重複再生を防ぐため）
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る1");
            SoundManager.Instance.StopBGM();
        }

        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; // 時間の進行を元に戻す

        SceneManager.LoadScene("Result"); // リザルトシーンを読み込む

    }
}
