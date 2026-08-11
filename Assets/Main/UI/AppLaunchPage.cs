using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Main.UI
{
    public class AppLaunchPage : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private RectTransform descWrap;
        [SerializeField] private Text descCol;
        [SerializeField] private Text description;
        [SerializeField] private Image progressBar;
        [SerializeField] private Text progressText;
        [SerializeField] private RectTransform[] loadingCircles;

        private Sequence animateSequence;

        private void Start()
        {
            StartCircleAnimation();
        }

        public void UpdateContent(string titleStr, string desc)
        {
            if (title) title.text = titleStr;
            if (description) description.text = desc;
            if (descWrap) descWrap.gameObject.SetActive(!string.IsNullOrEmpty(desc));
        }

        public void UpdateProgress(bool show, long currentDownload, long total)
        {
            float progress = (float)currentDownload / Math.Max(total, 1);
            if (!show) return;

            string titleStr = $"{(int)(progress * 100)} %";
            const float oneM = 1024 * 1024f;
            if (descCol) descCol.text = "正在下载资源：";
            UpdateContent(titleStr, $"{currentDownload / oneM:0.0}M / {(int)Math.Ceiling(total / oneM)}M");
            // if (progressBar)
            // {
            //     progressBar.fillAmount = progress;
            //     progressBar.gameObject.SetActive(show);
            // }
            //
            // if (progressText)
            // {
            //     progressText.text = $"{(progress) * 100}";
            //     progressText.gameObject.SetActive(show);
            // }
        }

        private void StartCircleAnimation()
        {
            Sequence mainSequence = DOTween.Sequence();
            //sequence.AppendInterval(0.6f);

            float minScale = 0.6f;
            for (int i = 0; i < loadingCircles.Length; i++)
            {
                var rect = loadingCircles[i];
                rect.localScale = new Vector3(minScale, minScale, 1);

                Sequence sequence = DOTween.Sequence();
                Tween tween = rect.DOScale(1, 0.4f);
                sequence.Append(tween);
                sequence.Append(rect.DOScale(minScale, 0.4f));

                mainSequence.Insert(i * 0.2f, sequence);
            }

            mainSequence.SetLoops(-1, LoopType.Restart);

            animateSequence = mainSequence;
        }

        private void OnDisable()
        {
            if (animateSequence != null)
            {
                animateSequence.Kill();
                animateSequence = null;
            }
        }
    }
}