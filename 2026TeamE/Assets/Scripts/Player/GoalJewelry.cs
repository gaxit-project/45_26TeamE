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
    IEnumerator GetAnime()
    {
        // 最初の位置を記録（Xのみ5に変更して画面手前に出す）
        Vector3 startPos = new Vector3(5, transform.position.y, transform.position.z);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        float animDuration = 0.5f;
        float flashInterval = 0.05f;
        float popHeight = 1.5f;
        float elapsedTime = 0f;

        while (elapsedTime < animDuration)
        {
            float t = elapsedTime / animDuration;

            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = startPos.y + (popHeight * easeOut);

            transform.position = new Vector3(startPos.x, currentY, startPos.z);

            bool isVisible = (elapsedTime % (flashInterval * 2)) < flashInterval;
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = isVisible;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石獲得");
        }
        SceneManager.LoadScene(SceneName);
        Destroy(gameObject);
        
    }
}

