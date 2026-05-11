using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace script
{
    // 1. 将 IBeginDragHandler 改为 IPointerDownHandler
    public class ARModelItem : MonoBehaviour, IPointerDownHandler
    {
        [Header("UI上显示的图像（可为空）")]
        public Image icon; 
        [HideInInspector] public GameObject prefab; 
        [HideInInspector] public FurniturePlacer placer; 
        
        // 2. 改用 OnPointerDown 方法
        public void OnPointerDown(PointerEventData eventData)
        {
            // 只要鼠标或手指点到这个图标，立刻把真实的家具模型传递给生成器
            placer.furniturePrefab = prefab;
            
            // 加句日志，看看是不是成功拿到了正确的名字
            Debug.Log($"📦 成功交接家具数据: {prefab.name}");
        }
    }
}