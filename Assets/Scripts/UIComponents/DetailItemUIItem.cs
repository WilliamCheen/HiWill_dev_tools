using UnityEngine;
using UnityEngine.UI;

public class DetailItemUIItem : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Text description;
    [SerializeField] private Image selIcon;
    
    public void Config(string titleStr, bool isSelected)
    {
        if (title) title.text = titleStr;
        if (selIcon)
        {
            Color iconColor =  selIcon.color;
            iconColor.a = isSelected ? 1 : 0;
            selIcon.color = iconColor;
        }
    }
}
