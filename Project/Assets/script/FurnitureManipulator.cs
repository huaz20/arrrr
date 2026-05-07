using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class FurnitureManipulator : MonoBehaviour
{
    [Header("AR 组件")]
    public ARRaycastManager raycastManager;
    public Camera arCamera; // 指向 AR Camera

    [Header("操作参数")]
    public float rotationSpeed = 0.5f;
    public float scaleSpeed = 0.005f;
    public Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 maxScale = new Vector3(3f, 3f, 3f);

    private GameObject targetFurniture;
    private bool isDragging = false;

    // 记录双指上一帧的位置，用于计算缩放和旋转
    private Vector2 touch0PrevPos, touch1PrevPos;
    private List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    void Start()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
    }

    void Update()
    {
        // 确保触摸屏存在
        if (Touchscreen.current == null) return;

        // 获取当前活跃的触摸点
        var activeTouches = GetActiveTouches();
        int touchCount = activeTouches.Count;

        if (touchCount == 0)
        {
            isDragging = false;
            return;
        }

        // --- 单指操作：选中与平移 ---
        if (touchCount == 1)
        {
            var touch = activeTouches[0];
            Vector2 touchPos = touch.position.ReadValue();
            var phase = touch.phase.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                // 如果点在UI上，不进行3D交互
                if (IsPointerOverUI(touchPos)) return;
                
                SelectFurniture(touchPos);
            }
            else if (phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging && targetFurniture != null)
            {
                MoveFurniture(touchPos);
            }
        }
        // --- 双指操作：缩放与旋转 ---
        else if (touchCount >= 2)
        {
            var touch0 = activeTouches[0];
            var touch1 = activeTouches[1];

            Vector2 touch0Pos = touch0.position.ReadValue();
            Vector2 touch1Pos = touch1.position.ReadValue();
            var phase0 = touch0.phase.ReadValue();
            var phase1 = touch1.phase.ReadValue();

            // 如果有新的手指刚按下，重置初始位置记录，防止模型瞬间跳动
            if (phase0 == UnityEngine.InputSystem.TouchPhase.Began || phase1 == UnityEngine.InputSystem.TouchPhase.Began)
            {
                touch0PrevPos = touch0Pos;
                touch1PrevPos = touch1Pos;
            }
            else if (phase0 == UnityEngine.InputSystem.TouchPhase.Moved || phase1 == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                if (targetFurniture != null)
                {
                    ScaleAndRotate(touch0Pos, touch1Pos);
                }
            }
        }
    }

    /// <summary>
    /// 获取当前所有非结束状态的触摸点
    /// </summary>
    private List<UnityEngine.InputSystem.Controls.TouchControl> GetActiveTouches()
    {
        List<UnityEngine.InputSystem.Controls.TouchControl> activeTouches = new List<UnityEngine.InputSystem.Controls.TouchControl>();
        foreach (var t in Touchscreen.current.touches)
        {
            var phase = t.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                activeTouches.Add(t);
            }
        }
        return activeTouches;
    }

    /// <summary>
    /// 射线检测 3D 物体以选中家具
    /// </summary>
    void SelectFurniture(Vector2 screenPos)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // 获取被击中物体的根节点（假设你的预制体结构有层级）
            targetFurniture = hit.collider.transform.root.gameObject;
            isDragging = true;
            Debug.Log($"🎯 选中家具: {targetFurniture.name}");
        }
    }

    /// <summary>
    /// AR 射线检测平面，实现平移
    /// </summary>
    void MoveFurniture(Vector2 screenPos)
    {
        if (raycastManager.Raycast(screenPos, arHits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = arHits[0].pose;
            // 仅更新位置，保留之前的旋转和缩放
            targetFurniture.transform.position = hitPose.position;
        }
    }

    /// <summary>
    /// 计算双指操作的缩放和旋转
    /// </summary>
    void ScaleAndRotate(Vector2 touch0Pos, Vector2 touch1Pos)
    {
        // ---- 1. 处理缩放 ----
        float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
        float currentMagnitude = (touch0Pos - touch1Pos).magnitude;
        float scaleDelta = (currentMagnitude - prevMagnitude) * scaleSpeed;

        Vector3 newScale = targetFurniture.transform.localScale + Vector3.one * scaleDelta;
        
        // 限制缩放范围
        newScale.x = Mathf.Clamp(newScale.x, minScale.x, maxScale.x);
        newScale.y = Mathf.Clamp(newScale.y, minScale.y, maxScale.y);
        newScale.z = Mathf.Clamp(newScale.z, minScale.z, maxScale.z);
        targetFurniture.transform.localScale = newScale;

        // ---- 2. 处理旋转 ----
        float angle = Vector2.SignedAngle(touch1Pos - touch0Pos, touch1PrevPos - touch0PrevPos);
        // Space.World 确保它是绕着世界 Y 轴转，而不是自身的局部 Y 轴
        targetFurniture.transform.Rotate(Vector3.up, -angle * rotationSpeed, Space.World);

        // ---- 3. 更新上一帧位置 ----
        touch0PrevPos = touch0Pos;
        touch1PrevPos = touch1Pos;
    }

    /// <summary>
    /// 防止点穿 UI
    /// </summary>
    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        return results.Count > 0;
    }

    /// <summary>
    /// 供外部 (如 FurniturePlacer) 调用的接口，生成后立即选中该物体
    /// </summary>
    public void SetTargetFurniture(GameObject furniture)
    {
        targetFurniture = furniture;
        isDragging = false; // 刚生成时不需要立即跟手平移，等待玩家下一次触控
    }
}