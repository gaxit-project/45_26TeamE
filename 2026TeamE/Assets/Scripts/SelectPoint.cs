using UnityEngine;
using UnityEngine.SceneManagement;

public class SelectPoint : MonoBehaviour
{
    [Header("選択パネルのタグ名")]
    [SerializeField] private string panelTag = "SelectPanel";
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private GameObject firstSelectButton;

    private void Start()
    {
        FindPanelInScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            if (firstSelectButton != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectButton);
            }
        }
        else
        {
            Debug.LogError($"新しいシーンでタグ '{panelTag}' が設定されたオブジェクトが見つかりませんでした。");
        }
    }

    public void ClosePanel()
    {
        if (selectPanel != null) selectPanel.SetActive(false);
    }
}
