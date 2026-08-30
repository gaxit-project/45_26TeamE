using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalJewelry : MonoBehaviour
{
    [SerializeField] private string SceneName;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    public static bool isGoalReached = false;
    private bool isProcessed = false; 

    void OnTriggerEnter(Collider other)
    {
        
        if (other.gameObject.CompareTag("Player") && !isProcessed)
        {
            isProcessed = true;

            
            Animator playerAnim = other.GetComponent<Animator>();
            if (playerAnim != null)
            {
                playerAnim.SetTrigger("Clear");
            }

            
            StartCoroutine(ClearSequence());
        }
    }

    IEnumerator ClearSequence()
    {
        
        yield return PickupAnimationUtil.PopAndFlash(transform);

        
        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("着水１");
        }

        
        
        yield return new WaitForSeconds(6.0f);

        
        isGoalReached = true;

        
        string targetScene = string.IsNullOrEmpty(SceneName) ? "Result" : SceneName;
        SceneManager.LoadScene(targetScene);

        
        Destroy(gameObject);
    }
}
