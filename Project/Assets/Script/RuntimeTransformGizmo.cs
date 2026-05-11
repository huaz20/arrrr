using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class FixedGizmoController : MonoBehaviour
{
    [Header("核心设置")]
    public Camera mainCamera;
    public LayerMask furnitureLayer;

    [Header("地面检测")]
    public float minYOffset = 0.01f;

    [Header("UI 面板 (选中时显示)")]
    public GameObject editUIPanel; 

    [Header("操作灵敏度")]
    public float rotateSpeed = 180f;
    public float scaleSpeed = 2f;
    public float heightSpeed = 1f; // 【新增】控制升降的速度

    private Transform targetFurniture;
    private bool isEditing = false;
    private Collider furnitureCollider;
    private float furnitureHeight = 0.5f;

    // --- UI 长按状态标志 ---
    private bool isScalingUp = false;
    private bool isScalingDown = false;
    private bool isRotatingLeft = false;
    private bool isRotatingRight = false;
    private bool isMovingUp = false;   // 【新增】上升状态
    private bool isMovingDown = false; // 【新增】下降状态

    // --- 拖拽与高度变量 ---
    private bool isDraggingFurniture = false;
    private Vector3 dragOffset;        // 【新增】记录手指点下去的偏移量，防止瞬移
    private float manualHeightOffset = 0f; // 【新增】记录玩家手动悬空的高度

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (editUIPanel != null) editUIPanel.SetActive(false);
    }

    void Update()
    {
        if (!isEditing)
        {
            CheckSelectionInput();
        }
        else
        {
            HandleContinuousEditing();
            HandleDraggingFurniture();
        }
    }

    // ================== 点击选取逻辑 ==================

    void CheckSelectionInput()
    {
        bool isPointerDown = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            isPointerDown = true;
            screenPos = Mouse.current.position.ReadValue();
        }
#else
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            if (Touchscreen.current.touches[0].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                isPointerDown = true;
                screenPos = Touchscreen.current.touches[0].position.ReadValue();
            }
        }
#endif

        if (isPointerDown)
        {
            if (IsPointerOverUI(screenPos)) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, furnitureLayer))
            {
                targetFurniture = hit.collider.transform.root;
                furnitureCollider = targetFurniture.GetComponent<Collider>();
                if (furnitureCollider == null) furnitureCollider = targetFurniture.GetComponentInChildren<Collider>();

                CalculateFurnitureHeight();
                
                // 【新增】选中时，计算它当前是不是已经悬空了
                UpdateManualHeightOffset(); 
                SnapToGround();

                isEditing = true;
                if (editUIPanel != null) editUIPanel.SetActive(true);
            }
        }
    }

    // ================== 选中后按住拖拽逻辑 ==================

    void HandleDraggingFurniture()
    {
        if (targetFurniture == null) return;

        bool isPointerDown = false;
        bool isPointerPressed = false;
        bool isPointerUp = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_EDITOR
        if (Mouse.current != null)
        {
            isPointerDown = Mouse.current.leftButton.wasPressedThisFrame;
            isPointerPressed = Mouse.current.leftButton.isPressed;
            isPointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            screenPos = Mouse.current.position.ReadValue();
        }
#else
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            var phase = touch.phase.ReadValue();
            isPointerDown = phase == UnityEngine.InputSystem.TouchPhase.Began;
            isPointerPressed = phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
            isPointerUp = phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
            screenPos = touch.position.ReadValue();
        }
#endif

        if (isPointerDown && !IsPointerOverUI(screenPos))
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, furnitureLayer))
            {
                if (hit.collider.transform.root == targetFurniture)
                {
                    isDraggingFurniture = true;

                    // 【核心修复：防瞬移】记录点击位置与家具中心的偏移量
                    Plane virtualGroundPlane = new Plane(Vector3.up, targetFurniture.position);
                    if (virtualGroundPlane.Raycast(ray, out float distance))
                    {
                        Vector3 hitPoint = ray.GetPoint(distance);
                        dragOffset = targetFurniture.position - hitPoint;
                    }
                }
            }
        }

        if (isPointerPressed && isDraggingFurniture)
        {
            Plane virtualGroundPlane = new Plane(Vector3.up, targetFurniture.position);
            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (virtualGroundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                // 【核心修复】在移动时加上偏移量
                targetFurniture.position = hitPoint + dragOffset; 
                SnapToGround();
            }
        }

        if (isPointerUp && isDraggingFurniture)
        {
            isDraggingFurniture = false;
            SnapToGround();
        }
    }

    // ================== 持续编辑逻辑 ==================

    void HandleContinuousEditing()
    {
        if (targetFurniture == null) return;

        bool scaleChanged = false;

        // 旋转
        if (isRotatingLeft) targetFurniture.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        if (isRotatingRight) targetFurniture.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime, Space.World);

        // 缩放
        if (isScalingUp)
        {
            targetFurniture.localScale += Vector3.one * scaleSpeed * Time.deltaTime;
            scaleChanged = true;
        }
        if (isScalingDown)
        {
            Vector3 newScale = targetFurniture.localScale - Vector3.one * scaleSpeed * Time.deltaTime;
            if (newScale.x > 0.05f) 
            {
                targetFurniture.localScale = newScale;
                scaleChanged = true;
            }
        }

        if (scaleChanged)
        {
            CalculateFurnitureHeight();
            SnapToGround();
        }

        // 【新增：处理升降】
        if (isMovingUp)
        {
            manualHeightOffset += heightSpeed * Time.deltaTime;
            SnapToGround(); // 触发贴地计算以应用新的悬空高度
        }
        if (isMovingDown)
        {
            manualHeightOffset -= heightSpeed * Time.deltaTime;
            
            SnapToGround();
        }
    }

    // ================== 给外部 UI 按钮调用的接口 ==================

    public void OnPointerDown_ScaleUp() { isScalingUp = true; }
    public void OnPointerUp_ScaleUp() { isScalingUp = false; }

    public void OnPointerDown_ScaleDown() { isScalingDown = true; }
    public void OnPointerUp_ScaleDown() { isScalingDown = false; }

    public void OnPointerDown_RotateLeft() { isRotatingLeft = true; }
    public void OnPointerUp_RotateLeft() { isRotatingLeft = false; }

    public void OnPointerDown_RotateRight() { isRotatingRight = true; }
    public void OnPointerUp_RotateRight() { isRotatingRight = false; }

    // 【新增】上升与下降接口
    public void OnPointerDown_MoveUp() { isMovingUp = true; }
    public void OnPointerUp_MoveUp() { isMovingUp = false; }

    public void OnPointerDown_MoveDown() { isMovingDown = true; }
    public void OnPointerUp_MoveDown() { isMovingDown = false; }

    public void OnConfirmEditButtonClicked()
    {
        isEditing = false;
        targetFurniture = null;
        furnitureCollider = null;

        // 重置所有状态
        isScalingUp = false; isScalingDown = false;
        isRotatingLeft = false; isRotatingRight = false;
        isMovingUp = false; isMovingDown = false;
        isDraggingFurniture = false;
        
        // 【注意】不要在这里重置 manualHeightOffset，否则下一次点中它会掉下来

        if (editUIPanel != null) editUIPanel.SetActive(false);
    }

    // ================== 辅助计算 ==================

    // 【新增】计算目标初始悬空高度
    void UpdateManualHeightOffset()
    {
        if (targetFurniture == null) return;
        Vector3 rayOrigin = targetFurniture.position + Vector3.up * 1f; 
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f);
        float closestGroundY = -Mathf.Infinity;
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == targetFurniture) continue;
            if (hit.point.y > closestGroundY) closestGroundY = hit.point.y;
        }

        if (closestGroundY > -Mathf.Infinity)
        {
            // 如果它已经在空中，记录它跟地面的高度差
            float baseRestingY = closestGroundY + (furnitureHeight / 2f) + minYOffset;
            manualHeightOffset = targetFurniture.position.y - baseRestingY;
        }
        else
        {
            manualHeightOffset = 0f;
        }
    }

    void SnapToGround()
    {
        if (targetFurniture == null) return;
        Vector3 rayOrigin = targetFurniture.position + Vector3.up * 1f; 
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f);
        float closestGroundY = -Mathf.Infinity;
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == targetFurniture) continue;
            if (hit.point.y > closestGroundY) closestGroundY = hit.point.y;
        }
        
        if (closestGroundY > -Mathf.Infinity)
        {
            // 【核心变化】加上 manualHeightOffset，让它能在空中漂浮！
            float targetY = closestGroundY + (furnitureHeight / 2f) + minYOffset + manualHeightOffset;
            Vector3 newPos = targetFurniture.position;
            newPos.y = targetY;
            targetFurniture.position = newPos;
        }
    }

    void CalculateFurnitureHeight()
    {
        if (furnitureCollider != null)
            furnitureHeight = furnitureCollider.bounds.size.y;
        else
        {
            Renderer renderer = targetFurniture.GetComponentInChildren<Renderer>();
            furnitureHeight = renderer != null ? renderer.bounds.size.y : 0.5f;
        }
    }

    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}