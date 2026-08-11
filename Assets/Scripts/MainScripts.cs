using System;
using Scenes.Scripts.UIComponents;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;

public class MainScripts : MonoBehaviour
{
    [SerializeField] private MacAddressController _macAddressController;
    [SerializeField] private VerticalLayoutGroup _mainVerticalLayoutGroup;
    [SerializeField] private RectTransform _currentTrans;
    private const float MinWidth = 700;
    private const float MaxWidth = 900;

    private void Awake()
    {
        this.OnRectTransformDimensionsChangeAsObservable()
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x => OnMainContentSizeChanged())
            .AddTo(this);
        
        AddLogger().Forget();
    }

    private async UniTaskVoid AddLogger()
    {
        var cToken = this.GetCancellationTokenOnDestroy();
        await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cToken);
        Utils.AddScreenLoggerListener(this);
    }

    private void OnMainContentSizeChanged()
    {
        if (!_mainVerticalLayoutGroup || !_currentTrans) return;
        float width = _currentTrans.rect.size.x;
        double left = width < MaxWidth ? (width - MinWidth) / 2.0 : (width - MaxWidth) / 2.0;
        int paddingLeft = (int)left;
        _mainVerticalLayoutGroup.padding.left = paddingLeft;
        _mainVerticalLayoutGroup.padding.right = paddingLeft;
    }
}