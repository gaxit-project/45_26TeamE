using UnityEngine;
using UnityEngine.UIElements;

public class Close : MonoBehaviour
{
    [Header("選択パネル")]
    [SerializeField] private GameObject selectpanel;

    public void OnClose()
    {
        selectpanel.SetActive(false);
    }
}
