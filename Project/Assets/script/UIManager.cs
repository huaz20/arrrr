using script.Tool;
using System.Collections.Generic;
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

        [Header("清空按钮")]
        public Button clearBtn;

        // 手机自动加载的文件夹 👇 必须在 Resources/Furniture 下面
        private readonly string FURNITURE_PATH = "Furniture";

        private void Start()
        {
            InitializeUI();
        }

        void InitializeUI()
        {
            #region 滑动窗口
            if (contentParent == null)
            {
                Debug.LogError("UIManager: ContentParent 未赋值！");
                return;
            }

            // 修复：手机安全加载方式
            LoadFurnitureFromResources();
            #endregion

            #region 清空按钮
            if (clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.AddListener(furniturePlacer.ClearAllFurniture);
            #endregion
        }

        void LoadFurnitureFromResources()
        {
            // ✅ 修复 1：使用固定子文件夹路径，手机必加载
            GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>(FURNITURE_PATH);

            if (loadedPrefabs == null || loadedPrefabs.Length == 0)
            {
                Debug.LogError($"UIManager: 在 Resources/{FURNITURE_PATH} 中未找到任何家具预制体！");
                Debug.LogError("请把所有家具拖入：Assets/Resources/Furniture/ 文件夹");
                return;
            }

            Debug.Log($"UIManager: 成功加载 {loadedPrefabs.Length} 个家具");

            foreach (GameObject prefab in loadedPrefabs)
            {
                // ✅ 修复 2：过滤掉损坏/空对象
                if (prefab == null) continue;
                CreateUIItem(prefab);
            }
        }

        void CreateUIItem(GameObject prefab)
        {
            GameObject go = new GameObject("Item_" + prefab.name, typeof(RectTransform), typeof(Image), typeof(ARModelItem));
            go.transform.SetParent(contentParent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 100);

            ARModelItem itemScript = go.GetComponent<ARModelItem>();
            Image iconImage = go.GetComponent<Image>();

            itemScript.icon = iconImage;
            itemScript.prefab = prefab;
            itemScript.placer = furniturePlacer;

            // 生成图标
            Sprite icon = GenerateIcon(prefab);
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
            }
            else
            {
                // 图标失败时显示灰色方块
                iconImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                iconImage.enabled = true;
            }
        }

        Sprite GenerateIcon(GameObject prefab)
        {
            try
            {
                return RuntimePreviewGenerator.GenerateIcon(prefab);
            }
            catch
            {
                Debug.LogWarning($"生成图标失败：{prefab.name}");
                return null;
            }
        }

        private void OnDestroy()
        {
            if (clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.RemoveListener(furniturePlacer.ClearAllFurniture);
        }
    }
}