using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; 

public class ScaleOnSelect : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    
    [SerializeField] private float selectScale = 1.1f;

    
    private Vector3 initialScale;

    private void Awake()
    {
        
        initialScale = transform.localScale;
    }

    
    public void OnSelect(BaseEventData eventData)
    {
        transform.localScale = initialScale * selectScale;
    }

    
    public void OnDeselect(BaseEventData eventData)
    {
        transform.localScale = initialScale;
    }

    
    private void OnDisable()
    {
        transform.localScale = initialScale;
    }

    public void GoExchange()
    {

    }
}
