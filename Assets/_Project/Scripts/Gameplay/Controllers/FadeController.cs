using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class FadeController : MonoBehaviour
    {
        [SerializeField] private Image _fadeImage;
        
        public void FadeOut()
        {
            StartCoroutine(FadeRoutine(0f, 1f));
        }
        
        public void FadeIn()
        {
            StartCoroutine(FadeRoutine(1f, 1f));
        }

        public IEnumerator FadeRoutine(float targetAlpha, float fadeDuration)
        {
            Color currentColor = _fadeImage.color;
            float startAlpha = currentColor.a;
            float elapsedTime = 0f;
            _fadeImage.gameObject.SetActive(true);

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += UnityEngine.Time.unscaledDeltaTime;
                float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            
                _fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);
                yield return null;
            }
            
            _fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);
            
            if (Mathf.Approximately(targetAlpha, 0f))
            {
                _fadeImage.gameObject.SetActive(false);
            }
        }
    }
}