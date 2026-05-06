using System.Collections.Generic;
using script.Tool;
using UnityEngine;
using UnityEngine.UI;

namespace script
{
    public class UIManager : MonoBehaviour
    {
        [Header("核心引用")]
        public FurniturePlacer furniturePlacer; 

        [Header("滑动窗口配置")] 
        public RectTransform contentParent; 
        public List<GameObject> prefabs;   

        [Header("清空按钮")] 
        public Button clearBtn;

        private void Start()
        {
            InitializeUI();
        }

        /// <summary>
        /// UI层初始化
        /// </summary>
        void InitializeUI()
        {
            #region 滑动窗口

            //判空
            if (contentParent == null)
            {
                Debug.LogError("UIManager: ContentParent 未赋值！");
                return;
            }
            
            foreach (var p in prefabs)
            {
                if (p == null) continue;

                // 1.新建实例
                GameObject go = new GameObject("Item_" + p.name, typeof(RectTransform), typeof(Image), typeof(ARModelItem));
                
                // 2.设置父级，父物体上的GridLayoutGroup会自动排列
                go.transform.SetParent(contentParent, false);

                // 3.处理ARModelItem逻辑
                ARModelItem itemScript = go.GetComponent<ARModelItem>();
                
                itemScript.icon = go.GetComponent<Image>();
                itemScript.prefab = p;
                itemScript.placer = furniturePlacer;

                // 4.如果图标为空，调用快照工具自动生成一张
                if (itemScript.icon != null)
                    itemScript.icon.sprite = RuntimePreviewGenerator.GenerateIcon(p);
                
                Debug.Log($"已动态生成 UI 项: {go.name}");
            }

            #endregion

            #region 清空按钮

            if (clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.AddListener(furniturePlacer.ClearAllFurniture);

            #endregion
        }

        private void OnDestroy()
        {
            //解绑事件
            if(clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.RemoveListener(furniturePlacer.ClearAllFurniture);
        }
    }
}