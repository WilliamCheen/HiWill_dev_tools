using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Scenes.Scripts.UIComponents
{
    public class MacAddressController : MonoBehaviour
    {
        [SerializeField] private Text sectionTitle;
        [SerializeField] private InputField addressField;
        [SerializeField] private Button copyBtn;
        [SerializeField] private Button refetchBtn;
        [SerializeField] private Button moreBtn;
        [SerializeField] private RectTransform moreIconTrans;
        [SerializeField] private RectTransform moreContentTrans;
        [SerializeField] private RectTransform moreContainer;
        [SerializeField] private GameObject morePrefab;
        private readonly List<DetailItemUIItem> uiItems = new();

        private void Awake()
        {
            if (copyBtn)
            {
                copyBtn.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = addressField.text; });
            }

            if (refetchBtn)
            {
                refetchBtn.onClick.AddListener(() => FetchMacAddress().Forget());
            }

            if (moreBtn)
            {
                moreBtn.onClick.AddListener(() =>
                {
                    bool show = moreContainer.gameObject.activeSelf;
                    SetNeedShowMore(!show);
                });
            }

            FetchMacAddress().Forget();
        }

        private void SetNeedShowMore(bool show)
        {
            if (moreContainer) moreContainer.gameObject.SetActive(show);
            var list = Utils.DebugNumberItems;
            if (morePrefab && moreContentTrans && list != null && list.Length > 0)
            {
                ClearUIItems();
                foreach (var numberItem in list)
                {
                    GameObject obj = Instantiate(morePrefab, moreContentTrans);
                    if (obj && obj.TryGetComponent(out DetailItemUIItem item))
                    {
                        item.Config($"{numberItem.name}：{numberItem.number}", numberItem.isSelected);
                        obj.SetActive(true);
                        uiItems.Add(item);
                    }
                }
            }

            if (moreIconTrans)
            {
                moreIconTrans.localRotation = Quaternion.Euler(0, 0, show ? 180f : 0f);
            }
        }

        private async UniTaskVoid FetchMacAddress()
        {
            string macAddress = await Utils.DeviceUniqueIdentifier();
            string str = !string.IsNullOrEmpty(macAddress) ? macAddress : "获取失败，请重试...";
            if (addressField) addressField.text = str;
        }

        private void ClearUIItems()
        {
            foreach (var detailUI in uiItems)
            {
                Destroy(detailUI.gameObject);
            }
            uiItems.Clear();
        }
    }
}