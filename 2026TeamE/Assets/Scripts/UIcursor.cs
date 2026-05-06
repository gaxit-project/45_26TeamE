using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UICursor : MonoBehaviour
{
    [SerializeField] float scrollSpeed = 10f;
    [SerializeField] public GameObject cursor;

    [Header("SE")]
    [SerializeField] private string moveSE = "Select";
    private GameObject lastSelected;

    void Start()
    {
        lastSelected = EventSystem.current.currentSelectedGameObject;
    }

    void Update()
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
        {
            return;
        }

        // ポーズ中（Time.timeScale <= 0）でもカーソルが動くように制限を解除

        if (selected != lastSelected)
        {
            // if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(moveSE);
            lastSelected = selected;
        }

        Transform target = selected.GetComponent<Transform>();

        // X軸・Y軸の両方を追従させ、ポーズ中も動くように unscaledDeltaTime を使用する
        Vector3 newPos = cursor.transform.position;
        newPos.x = Mathf.Lerp(newPos.x, target.position.x, Time.unscaledDeltaTime * scrollSpeed);
        newPos.y = Mathf.Lerp(newPos.y, target.position.y, Time.unscaledDeltaTime * scrollSpeed);
        cursor.transform.position = newPos;
    }
}
