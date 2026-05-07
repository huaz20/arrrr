using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI; // 需要引入UI命名空间

public class FixedGizmoController : MonoBehaviour
{
    [Header("核心设置")]
    public Camera mainCamera;
    public Transform gizmoRoot;      // 右下角的操作轴(摇杆)
    public LayerMask furnitureLayer; // 家具层
    public LayerMask gizmoLayer;     // 操作轴层

    [Header("UI 绑定")]
    public GameObject confirmButtonObj; // 拖入你的“对勾”UI按钮物体

    [Header("遥控灵敏度")]
    public float moveSpeed = 0.002f;
    public float rotateSpeed = 0.3f;
    public float scaleSpeed = 0.002f;

    private Transform targetFurniture;
    private bool isEditing = false; // 核心：是否处于锁定编辑状态

    private enum GizmoPart { None, MoveX, MoveY, MoveZ, RotateX, RotateY, RotateZ, ScaleCenter }
    private GizmoPart currentPart = GizmoPart.None;

    private Vector2 lastScreenPos;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        
        // 初始隐藏摇杆和对勾按钮
        if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(false);
        if (confirmButtonObj != null) confirmButtonObj.SetActive(false);
    }

    void Update()
    {
        Vector2 screenPos = Vector2.zero;
        bool isPointerDown = false;
        bool isPointerPressed = false;
        bool isPointerUp = false;

        // 1. 兼容获取输入
        if (Application.isEditor || Touchscreen.current == null)
        {
            if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
                isPointerDown = Mouse.current.leftButton.wasPressedThisFrame;
                isPointerPressed = Mouse.current.leftButton.isPressed;
                isPointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            }
        }
        else
        {
            if (Touchscreen.current.touches.Count > 0)
            {
                var touch = Touchscreen.current.touches[0];
                screenPos = touch.position.ReadValue();
                isPointerDown = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
                isPointerPressed = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Stationary;
                isPointerUp = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
            }
        }

        // 2. 状态机重置
        if (isPointerUp)
        {
            currentPart = GizmoPart.None;
        }

        // 拦截UI点击 (点对勾按钮时不会触发下面的3D射线)
        if (IsPointerOverUI(screenPos) && currentPart == GizmoPart.None) return;

        // 3. 执行点击或拖拽
        if (isPointerDown)
        {
            HandlePointerDown(screenPos);
        }
        else if (isPointerPressed && currentPart != GizmoPart.None && targetFurniture != null && isEditing)
        {
            HandleDrag(screenPos);
        }
    }

    void HandlePointerDown(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // 1. 优先检测：是否点到了右下角的“遥控器”
        if (Physics.Raycast(ray, out RaycastHit gizmoHit, 100f, gizmoLayer))
        {
            string partName = gizmoHit.collider.gameObject.name;
            ParseGizmoPart(partName);
            lastScreenPos = screenPos; 
            return; 
        }

        // 2. 核心修改：如果当前处于锁定编辑状态，则屏蔽一切对3D场景的点选！
        if (isEditing)
        {
            // 直接 return，不管点到空地还是其他家具，都不会有任何反应
            return; 
        }

        // 3. 如果没在编辑，检测是否点到了新的家具
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, furnitureLayer))
        {
            targetFurniture = hit.collider.transform.root;
            isEditing = true; // 锁定状态

            // 唤醒摇杆和对勾按钮
            if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(true); 
            if (confirmButtonObj != null) confirmButtonObj.SetActive(true);

            Debug.Log($"🎯 选中物体: {targetFurniture.name}，进入锁定编辑模式");
        }
    }

    void ParseGizmoPart(string partName)
    {
        if (partName.Contains("MoveX")) currentPart = GizmoPart.MoveX;
        else if (partName.Contains("MoveY")) currentPart = GizmoPart.MoveY;
        else if (partName.Contains("MoveZ")) currentPart = GizmoPart.MoveZ;
        else if (partName.Contains("RotateX")) currentPart = GizmoPart.RotateX;
        else if (partName.Contains("RotateY")) currentPart = GizmoPart.RotateY;
        else if (partName.Contains("RotateZ")) currentPart = GizmoPart.RotateZ;
        else if (partName.Contains("Scale")) currentPart = GizmoPart.ScaleCenter;
    }

    void HandleDrag(Vector2 screenPos)
    {
        Vector2 delta = screenPos - lastScreenPos;

        switch (currentPart)
        {
            case GizmoPart.MoveX:
                targetFurniture.Translate(Vector3.right * delta.x * moveSpeed, Space.World);
                break;
            case GizmoPart.MoveY:
                targetFurniture.Translate(Vector3.up * delta.y * moveSpeed, Space.World);
                break;
            case GizmoPart.MoveZ:
                targetFurniture.Translate(Vector3.forward * (delta.x + delta.y) * moveSpeed, Space.World);
                break;
            case GizmoPart.RotateX:
                targetFurniture.Rotate(Vector3.right, delta.y * rotateSpeed, Space.World);
                break;
            case GizmoPart.RotateY:
                targetFurniture.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
                break;
            case GizmoPart.RotateZ:
                targetFurniture.Rotate(Vector3.forward, -delta.x * rotateSpeed, Space.World);
                break;
            case GizmoPart.ScaleCenter:
                float scaleFactor = (delta.x + delta.y) * scaleSpeed;
                Vector3 newScale = targetFurniture.localScale + Vector3.one * scaleFactor;
                if (newScale.x > 0.05f) targetFurniture.localScale = newScale;
                break;
        }

        lastScreenPos = screenPos;
    }

    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // ==========================================
    // 给 UI 按钮调用的接口
    // ==========================================

    /// <summary>
    /// 点击 UI 对勾按钮时调用此方法
    /// </summary>
    public void OnConfirmEditButtonClicked()
    {
        if (!isEditing) return;

        // 1. 解除锁定状态
        isEditing = false;
        targetFurniture = null;

        // 2. 隐藏摇杆和对勾按钮
        if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(false);
        if (confirmButtonObj != null) confirmButtonObj.SetActive(false);

        Debug.Log("✅ 已点击确认，退出编辑模式");
    }
}