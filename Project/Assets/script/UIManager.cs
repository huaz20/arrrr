using script.Tool;
using System.Collections;
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

        private readonly string FURNITURE_PATH = "Furniture";

        // 用于释放生成的纹理，防止内存泄漏
        private List<Sprite> iconSprites = new List<Sprite>();

        // 独立相机用于图标渲染（解决叠加问题）
        private Camera iconRenderCamera;
        private GameObject tempRenderObject;

        private void Start()
        {
            InitializeUI();
        }

        void InitializeUI()
        {
            if (contentParent == null)
            {
                Debug.LogError("UIManager: ContentParent 未赋值！");
                return;
            }

            // 清空旧UI和纹理缓存
            ClearOldUIItemsAndSprites();

            // 创建独立的图标渲染相机
            CreateIconRenderCamera();

            // 加载家具UI
            LoadFurnitureFromResources();

            if (clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.AddListener(furniturePlacer.ClearAllFurniture);
        }

        void CreateIconRenderCamera()
        {
            // 创建临时相机用于图标渲染
            GameObject camObj = new GameObject("IconRenderCamera");
            camObj.transform.SetParent(transform);
            iconRenderCamera = camObj.AddComponent<Camera>();

            // 保留你原本的正交相机（UI渲染专用）
            iconRenderCamera.orthographic = true;
            iconRenderCamera.orthographicSize = 1;

            iconRenderCamera.clearFlags = CameraClearFlags.SolidColor;
            iconRenderCamera.backgroundColor = new Color(0, 0, 0, 0);
            iconRenderCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            iconRenderCamera.enabled = false; // 不需要每帧渲染，手动调用
        }

        void ClearOldUIItemsAndSprites()
        {
            // 清空旧的UI项
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(contentParent.GetChild(i).gameObject);
            }

            // 释放所有之前生成的纹理
            foreach (var sprite in iconSprites)
            {
                if (sprite != null)
                {
                    if (sprite.texture != null)
                        Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
            iconSprites.Clear();
        }

        void LoadFurnitureFromResources()
        {
            GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>(FURNITURE_PATH);

            if (loadedPrefabs == null || loadedPrefabs.Length == 0)
            {
                Debug.LogError($"UIManager: 在 Resources/{FURNITURE_PATH} 中未找到任何家具预制体！");
                return;
            }

            Debug.Log($"UIManager: 找到 {loadedPrefabs.Length} 个家具预制体，开始创建UI");

            // 使用协程逐个创建，避免图标叠加问题
            StartCoroutine(CreateUIItemsCoroutine(loadedPrefabs));
        }

        IEnumerator CreateUIItemsCoroutine(GameObject[] prefabs)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;

                CreateUIItem(prefabs[i]);

                yield return null;

                if ((i + 1) % 5 == 0)
                {
                    System.GC.Collect();
                    yield return null;
                }
            }

            Debug.Log("UIManager: 所有家具UI创建完成");
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

            Sprite icon = GenerateIconWithIndependentCamera(prefab);
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconSprites.Add(icon);
            }
            else
            {
                iconImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        Sprite GenerateIconWithIndependentCamera(GameObject prefab)
        {
            if (iconRenderCamera == null)
            {
                Debug.LogWarning("图标渲染相机未初始化");
                return null;
            }

            try
            {
                // 临时实例化物体用于拍照
                if (tempRenderObject != null)
                    Destroy(tempRenderObject);

                tempRenderObject = Instantiate(prefab);

                // ===================== 【正确：45° 斜俯视，拍模型上方】 =====================
                // 1. 相机位置：斜后上方（标准俯视角）
                iconRenderCamera.transform.position = new Vector3(3f, 4f, -3f);
                // 2. 看向模型中心点
                iconRenderCamera.transform.LookAt(Vector3.zero);

                // 3. 模型摆正：完全归零，不旋转！！（解决屁股朝上的核心）
                tempRenderObject.transform.position = Vector3.zero;
                tempRenderObject.transform.rotation = Quaternion.identity; // 不旋转

                // 4. 缩放：根据模型大小调整（0.8~1.2之间都可以）
                tempRenderObject.transform.localScale = Vector3.one * 1f;

                // 设置一个临时Layer用于渲染
                int originalLayer = tempRenderObject.layer;
                SetLayerRecursively(tempRenderObject, LayerMask.NameToLayer("UI"));

                // 创建一个临时的光照（如果物体需要光照）
                Light tempLight = null;
                bool needLight = CheckIfNeedsLight(prefab);
                if (needLight)
                {
                    GameObject lightObj = new GameObject("TempLight");
                    lightObj.transform.SetParent(tempRenderObject.transform);
                    tempLight = lightObj.AddComponent<Light>();
                    tempLight.type = LightType.Directional;
                    tempLight.intensity = 1.3f;
                    // 光照从斜上方打下来
                    lightObj.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
                }

                // 创建RenderTexture
                RenderTexture rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
                rt.Create();

                // 设置相机渲染目标
                iconRenderCamera.targetTexture = rt;

                // 手动渲染一帧
                iconRenderCamera.Render();

                // 读取像素到Texture2D
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();

                // 清理
                RenderTexture.active = null;
                iconRenderCamera.targetTexture = null;
                rt.Release();
                Destroy(rt);

                // 清理临时光照
                if (tempLight != null)
                    Destroy(tempLight.gameObject);

                // 恢复Layer
                SetLayerRecursively(tempRenderObject, originalLayer);

                // 创建Sprite
                Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

                return newSprite;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"生成图标失败：{prefab.name} - {e.Message}");
                return null;
            }
        }

        bool CheckIfNeedsLight(GameObject prefab)
        {
            // 检查预制体是否需要光照（如果有MeshRenderer且没有自发光材质）
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null)
                {
                    // 简单判断：如果有标准着色器，可能需要光照
                    string shaderName = renderer.sharedMaterial.shader.name;
                    if (shaderName.Contains("Standard") || shaderName.Contains("Lit"))
                        return true;
                }
            }
            return false;
        }

        void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                    SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            // 清理临时渲染物体
            if (tempRenderObject != null)
                Destroy(tempRenderObject);

            // 清理渲染相机
            if (iconRenderCamera != null)
                Destroy(iconRenderCamera.gameObject);

            // 释放所有纹理资源
            foreach (var sprite in iconSprites)
            {
                if (sprite != null)
                {
                    if (sprite.texture != null)
                        Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
            iconSprites.Clear();

            if (clearBtn != null && furniturePlacer != null)
                clearBtn.onClick.RemoveListener(furniturePlacer.ClearAllFurniture);
        }
    }
}