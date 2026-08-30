using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class SelectPoint : MonoBehaviour
{
    [Header("選択パネルのタグ名")]
    [SerializeField] private string panelTag = "SelectPanel";
    [SerializeField] private string XimageName = "X";
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private GameObject firstSelectButton;
    [SerializeField] public GameObject X;

    [Header("シーン移動用のボタン設定")]
    [SerializeField] private InputAction jumpSceneAction;
    

    [Header("UIの表示位置調整")]
    [SerializeField] private Vector3 uiOffset = new Vector3(-2.0f, 2.0f, 0);

    
    private int activeZoneID = -1;
    private GameObject playerCache;

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
        if (playerCache == null) playerCache = GameObject.FindGameObjectWithTag("Player");
        GameObject player = playerCache;
        if (player != null)
        {
            
            Vector3 localPos = VoxelTerrain.Instance.transform.InverseTransformPoint(player.transform.position);
            float s = VoxelTerrain.Instance.BlockSize;
            int py = Mathf.FloorToInt(localPos.y / s);

            
            
            int boundaryZoneID = VoxelTerrain.Instance.GetBoundaryZoneIndex(py);
            bool nearBoundary = boundaryZoneID != -1;

            
            
            bool suppressedAsAlreadyUsed = false;
            if (nearBoundary && selectPanel != null && !selectPanel.activeSelf)
            {
                int candidateZoneID = boundaryZoneID;
                bool candidateIsAlreadyUsed = CheckpointManager.Instance != null && candidateZoneID == CheckpointManager.Instance.GetUsedCheckpointID();

                if (candidateIsAlreadyUsed)
                {
                    
                    suppressedAsAlreadyUsed = true;
                }
                else
                {
                    activeZoneID = candidateZoneID;

                    
                    VoxelTerrain.Instance.OnPlayerReachRelayPoint(py);
                    ShowButton(); 
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
                    if (X != null && !X.activeSelf) X.SetActive(true);
                }
                else
                {
                    if (KeyUIController.Instance != null)
                    {
                        KeyUIController.Instance.SetWarningActive(false);
                    }
                    if (X != null && X.activeSelf) X.SetActive(false);
                }

                if (jumpSceneAction != null && jumpSceneAction.WasPressedThisFrame())
                {
                    if (hasKeys)
                    {
                        
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

                    
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                    
                    if (screenPos.z > 0)
                    {
                        selectPanel.transform.position = screenPos;
                    }
                }
            }
            else
            {
                
                if (selectPanel != null && selectPanel.activeSelf)
                {
                    selectPanel.SetActive(false);
                }

                if (KeyUIController.Instance != null)
                {
                    KeyUIController.Instance.SetWarningActive(false);
                }

                if (X != null && X.activeSelf)
                {
                    X.SetActive(false);
                }

                
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
        
        GameObject foundPanel = GameObject.FindWithTag(panelTag);
        if (foundPanel != null)
        {
            selectPanel = foundPanel;

            
            if (firstSelectButton == null)
            {
                Transform btnTransform = selectPanel.transform.Find("GoResult");
                if (btnTransform != null)
                {
                    firstSelectButton = btnTransform.gameObject;
                }
            }

            
            if (X == null)
            {
                
                Transform[] children = selectPanel.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    
                    if (t.name == XimageName)
                    {
                        X = t.gameObject;
                        break;
                    }
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

        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る1");
            SoundManager.Instance.StopBGM();
        }

        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; 

        SceneManager.LoadScene("Result"); 

    }
}
