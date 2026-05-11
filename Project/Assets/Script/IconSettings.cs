using UnityEngine;

namespace script.Tool // 跟你之前的命名空间保持一致
{
    public class IconSettings : MonoBehaviour
    {
        [Header("UI 拍照专用设置")]
        [Tooltip("调整这个角度，让模型在 UI 里呈现最好的姿态")]
        public Vector3 customRotation = new Vector3(-90, -45, 0); // 给个默认的好看角度

        [Tooltip("如果模型在 UI 里太大或太小，调整这个缩放")]
        public float customScale = 1f;
    }
}