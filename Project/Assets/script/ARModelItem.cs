using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace script
{
    public class ARModelItem : MonoBehaviour,IBeginDragHandler
    {
        [Header("UI上显示的图像（可为空）")]
        public Image icon; 
        [HideInInspector] public GameObject prefab; 
        [HideInInspector] public FurniturePlacer placer; 
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            placer.furniturePrefab = prefab;
        }
        
        //OnDrag、OnDragEnd 逻辑在 FurniturePlacer 中有写
    }
}