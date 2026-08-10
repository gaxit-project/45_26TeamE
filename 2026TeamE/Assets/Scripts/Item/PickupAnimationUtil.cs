using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 「アイテム取得時にXを5へずらしてポップ＆点滅させる」演出の共通処理。
/// </summary>
public static class PickupAnimationUtil
{
    /// <summary>
    /// target をポップさせながら点滅させる。
    /// </summary>
    public static IEnumerator PopAndFlash(Transform target, Action onComplete = null)
    {
        // 最初の位置を記録
        Vector3 startPos = new Vector3(5, target.position.y, target.position.z);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        float animDuration = 0.5f;
        float flashInterval = 0.05f;
        float popHeight = 1.5f;
        float elapsedTime = 0f;

        while (elapsedTime < animDuration)
        {
            float t = elapsedTime / animDuration;

            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = startPos.y + (popHeight * easeOut);

            target.position = new Vector3(startPos.x, currentY, startPos.z);

            bool isVisible = (elapsedTime % (flashInterval * 2)) < flashInterval;
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = isVisible;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        onComplete?.Invoke();
    }
}
