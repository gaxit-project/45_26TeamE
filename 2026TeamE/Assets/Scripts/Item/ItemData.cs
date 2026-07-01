using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite uiIcon;
    public int moneyValue;
    public GameObject effectPrefab;
    public string seName;
}
