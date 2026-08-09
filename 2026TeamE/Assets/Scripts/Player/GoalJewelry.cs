using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalJewelry : MonoBehaviour
{

    [SerializeField] private string SceneName;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Get();
        }
    }

    void Get()
    {
        StartCoroutine(GetAnime());
    }
    public static bool isGoalReached = false;

    IEnumerator GetAnime()
    {
        yield return PickupAnimationUtil.PopAndFlash(transform);

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("着水１");
        }

        isGoalReached = true;
        SceneManager.LoadScene("Result");
        Destroy(gameObject);
    }
}
