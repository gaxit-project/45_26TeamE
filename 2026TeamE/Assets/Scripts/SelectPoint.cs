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
    [SerializeField] public GameObject X;

    [Header("UIの表示位置調整")]
    [SerializeField] private Vector3 uiOffset = new Vector3(-2.0f, 2.0f, 0);

    // パネルを表示した瞬間に確定させるゾーンID。押下時などはこれを使い回し、毎フレーム再計算しない。
    private int activeZoneID = -1;

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

            // 岩盤（中継地点）の近く3行以内にいるかどうか。一致した場合はそのままゾーンIDも得られる
            // （GetRelayIDだと同じ許容範囲内でも1行のズレで隣のゾーンを指してしまうため使わない）
            int boundaryZoneID = VoxelTerrain.Instance.GetBoundaryZoneIndex(py);
            bool nearBoundary = boundaryZoneID != -1;

            // パネルがまだ出ていない＝この中継地点に新しく到達した瞬間にのみゾーンIDと使用済み判定を確定させる。
            // 以降、この帯域にいる間は毎フレーム再計算せず、確定した値を使い回す。
            bool suppressedAsAlreadyUsed = false;
            if (nearBoundary && selectPanel != null && !selectPanel.activeSelf)
            {
                int candidateZoneID = boundaryZoneID;
                bool candidateIsAlreadyUsed = CheckpointManager.Instance != null && candidateZoneID == CheckpointManager.Instance.GetUsedCheckpointID();

                if (candidateIsAlreadyUsed)
                {
                    // 直前にリスポーンした地点と同じ中継地点なので、何も表示せず素通りさせる
                    suppressedAsAlreadyUsed = true;
                }
                else
                {
                    activeZoneID = candidateZoneID;

                    // チェックポイント保存などの処理
                    VoxelTerrain.Instance.OnPlayerReachRelayPoint(py);
                    ShowButton(); // UI表示
                }
            }

            if (nearBoundary && !suppressedAsAlreadyUsed && activeZoneID != -1)
            {
                bool hasKeys = VoxelTerrain.Instance.IsZoneCleared(activeZoneID);

                if (!hasKeys)
                {
                    if (KeyUIController.Instance != null)
                    {
                        KeyUIController.Instance.SetWarningActive(true);
                    }
                    if (X != null) X.SetActive(true);
                }
                else
                {
                    if (KeyUIController.Instance != null)
                    {
                        KeyUIController.Instance.SetWarningActive(false);
                    }
                    if (X != null) X.SetActive(false);
                }

                if (jumpSceneAction != null && jumpSceneAction.WasPressedThisFrame())
                {
                    if (hasKeys)
                    {
                        // 鍵が足りていればリザルトへ！
                        GoToResult();
                    }
                    else
                    {
                        SoundManager.Instance.PlaySE("つるはしで掘る3");
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

                if (KeyUIController.Instance != null)
                {
                    KeyUIController.Instance.SetWarningActive(false);
                }

                // 帯域を離れたら確定ゾーンIDをクリアし、次に入った時に改めて確定させる
                activeZoneID = -1;
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
