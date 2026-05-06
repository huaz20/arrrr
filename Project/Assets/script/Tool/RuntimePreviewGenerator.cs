using Unity.VisualScripting;
using UnityEngine;

namespace script.Tool
{
    /// <summary>
    /// 给模型拍快照，返回2D图像用于UI层的工具类
    /// </summary>
    public class RuntimePreviewGenerator : MonoBehaviour
    {
        /// <summary>
        /// 为指定的 3D物体 生成一个 Sprite快照
        /// </summary>
        /// <param name="_prefab"></param>
        /// <param name="_size">生成贴图的分辨率（默认256x256）</param>
        /// <returns>返回生成的Sprite，如果预制件为null，返回null</returns>
        public static Sprite GenerateIcon(GameObject _prefab, int _size = 256)
        {
            if (_prefab == null) return null;

            // 1.创建一个临时的摄像机
            GameObject camObj = new GameObject("TempCamera");
            Camera cam = camObj.AddComponent<Camera>();
            //设置属性
            cam.backgroundColor = new Color(0, 0, 0, 0);  //全透明
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = true;  //正交视角，防止透视变形
            
            // 2.创建一个临时的灯光
            GameObject lightObj = new GameObject("TempLight");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f,-30f,0f);
            
            // 3.设置相机的Render Texture
            RenderTexture rt = RenderTexture.GetTemporary(_size, _size, 16);
            cam.targetTexture = rt;
            
            // 4.在很远的地方，实例模型的一个对象
            GameObject modelObj = Object.Instantiate(_prefab, new Vector3(0, 1000, 0), Quaternion.Euler(15f, -45f, 0f));  //一个倾斜的角度，以便展示物体的正面、侧面和顶部的细节
            
            // 5.自动计算模型的大小，调整摄像机的视野，确保模型刚好填满图像
            Bounds bounds = GetBounds(modelObj);
            cam.transform.position = bounds.center + new Vector3(0, 0, -5f);
            cam.orthographicSize = bounds.extents.magnitude;
            
            // 6.相机渲染一次（拍照一次）
            cam.Render();

            // 7.将拍到的画面提取为2D纹理
            RenderTexture.active = rt;  //指定像素读取对象
            Texture2D tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0,0,_size,_size),0,0);  //像素读取
            tex.Apply();  //提交像素读取
            
            // 8.销毁生成的摄像机、灯光和模型对象
            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            //销毁生成的临时对象
            Destroy(modelObj);
            Destroy(camObj);
            Destroy(lightObj);

            return Sprite.Create(tex, new Rect(0, 0, _size, _size), new Vector2(0.5f, 0.5f));
        }
        
        /// <summary>
        /// 辅助方法：获取模型所有网格的总体包围盒（算出模型的实际长宽高）
        /// </summary>
        /// <param name="_obj"></param>
        /// <returns></returns>
        private static Bounds GetBounds(GameObject _obj)
        {
            Renderer[] renderers = _obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(_obj.transform.position, Vector3.one);

            //以第一个渲染器的边界作为初始边界
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                //逐步将子渲染器的边界合并进来
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
