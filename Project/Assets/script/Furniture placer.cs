using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class FurniturePlacer : MonoBehaviour
{
    [Header("AR 组件")]
    public ARRaycastManager raycastManager;
    public GameObject furniturePrefab;
    public FurnitureManipulator manipulator;

    [Header("UI 设置")]
    public RectTransform dragUI;
    public Canvas parentCanvas;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject currentFurniture;
    private bool isDraggingUI = false;
    private bool hasMovedEnough = false;
    private Vector2 dragStartPosition;

    void Update()
    {
        HandleInput();

        if (isDraggingUI && currentFurniture != null)
        {
            UpdateFurniturePosition();
        }
    }

    void HandleInput()
    {
        if (Application.isEditor)
        {
            // 电脑端处理
            if (IsDraggingOnUI())
            {
                HandleUIDrag();
            }
        }
        else
        {
            // 手机端处理
            HandleMobileInput();
        }
    }

    #region 电脑端逻辑
    bool IsDraggingOnUI()
    {
        if (isDraggingUI)
            return true;

        if (Mouse.current.leftButton.wasPressedThisFrame && IsPointerOverSpecificUI())
        {
            isDraggingUI = true;
            hasMovedEnough = false;
            dragStartPosition = Mouse.current.position.ReadValue();
            Debug.Log("开始拖拽 UI (PC)");
            return true;
        }

        return false;
    }

    void HandleUIDrag()
    {
        if (Mouse.current.leftButton.isPressed)
        {
            if (!hasMovedEnough && currentFurniture == null)
            {
                Vector2 currentPos = Mouse.current.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartPosition, currentPos);

                if (dragDistance > 20f)
                {
                    hasMovedEnough = true;
                    Debug.Log($"✅ 生成家具 (PC)");
                    TrySpawnFurniture(currentPos);
                }
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDraggingUI = false;
            hasMovedEnough = false;
            Debug.Log("结束拖拽 UI (PC)");
        }
    }
    #endregion

    #region 手机端逻辑
    void HandleMobileInput()
    {
        if (Touchscreen.current == null) return;
        var touch = Touchscreen.current.primaryTouch;

        // 开始拖拽
        if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
        {
            if (IsTouchOverUI(touch.position.ReadValue()))
            {
                isDraggingUI = true;
                hasMovedEnough = false;
                dragStartPosition = touch.position.ReadValue();
                Debug.Log("开始拖拽 UI (手机)");
            }
        }

        // 拖拽中
        if (isDraggingUI && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
        {
            if (!hasMovedEnough && currentFurniture == null)
            {
                Vector2 currentPos = touch.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartPosition, currentPos);

                if (dragDistance > 15f)
                {
                    hasMovedEnough = true;
                    Debug.Log($"✅ 生成家具 (手机)");
                    TrySpawnFurniture(currentPos);
                }
            }
        }

        // 结束拖拽
        if (isDraggingUI && (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                             touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled))
        {
            isDraggingUI = false;
            hasMovedEnough = false;
            Debug.Log("结束拖拽 UI (手机)");
        }
    }

    bool IsTouchOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == dragUI.gameObject ||
                result.gameObject.transform.IsChildOf(dragUI))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    Vector2 GetCurrentScreenPosition()
    {
        if (Application.isEditor)
        {
            return Mouse.current.position.ReadValue();
        }

        // 手机端：获取第一个活跃的触摸
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            return touch.position.ReadValue();
        }

        return new Vector2(Screen.width / 2, Screen.height / 2);
    }

    void TrySpawnFurniture(Vector2 screenPos)
    {
        Debug.Log($"=== 尝试生成家具 ===");
        Debug.Log($"屏幕位置: {screenPos}");

        if (raycastManager == null)
        {
            Debug.LogError("❌ raycastManager 未赋值！");
            return;
        }

        Debug.Log("正在执行射线检测...");
        bool hitPlane = raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon);

        Debug.Log($"射线检测结果: {hitPlane}");
        Debug.Log($"击中数量: {hits.Count}");

        if (!hitPlane || hits.Count == 0)
        {
            Debug.Log("尝试其他 TrackableType...");
            bool hitAny = raycastManager.Raycast(screenPos, hits, TrackableType.AllTypes);
            Debug.Log($"TrackableType.AllTypes 结果: {hitAny}, 击中数量: {hits.Count}");

            if (hitAny && hits.Count > 0)
            {
                Debug.Log($"击中的类型: {hits[0].hitType}");
                Debug.Log($"击中位置: {hits[0].pose.position}");
            }
            else
            {
                Debug.LogWarning("⚠️ 未检测到任何平面，请确保 AR 已经识别到地面");
                return;
            }
        }

        float distance = Vector3.Distance(Camera.main.transform.position, hits[0].pose.position);
        Debug.Log($"平面距离相机: {distance}米");

        if (distance < 0.5f || distance > 5f)
        {
            Debug.LogWarning($"⚠️ 平面距离不合理：{distance}米，需要 0.5-5 米之间");
        }

        if (furniturePrefab == null)
        {
            Debug.LogError("❌ furniturePrefab 未赋值！");
            return;
        }

        Pose pose = hits[0].pose;
        Debug.Log($"✅ 准备生成家具，位置: {pose.position}, 旋转: {pose.rotation}");

        currentFurniture = Instantiate(furniturePrefab, pose.position, pose.rotation);

        if (currentFurniture == null)
        {
            Debug.LogError("❌ 家具生成失败！");
            return;
        }

        Debug.Log($"✅ 家具生成成功！");

        if (manipulator != null)
        {
            manipulator.SetTargetFurniture(currentFurniture);
            Debug.Log("已设置到 FurnitureManipulator");
        }
    }

    void UpdateFurniturePosition()
    {
        if (currentFurniture == null) return;

        Vector2 screenPos = GetCurrentScreenPosition();

        if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            currentFurniture.transform.SetPositionAndRotation(
                hits[0].pose.position,
                hits[0].pose.rotation
            );
        }
    }

    bool IsPointerOverSpecificUI()
    {
        if (EventSystem.current == null) return false;

        Vector2 pointerPos = Application.isEditor
            ? Mouse.current.position.ReadValue()
            : Touchscreen.current != null ? Touchscreen.current.primaryTouch.position.ReadValue() : Vector2.zero;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = pointerPos;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == dragUI.gameObject ||
                result.gameObject.transform.IsChildOf(dragUI))
            {
                Debug.Log($"✓ 击中 UI：{result.gameObject.name}");
                return true;
            }
        }

        return false;  // ✅ 添加这行，确保所有路径都有返回值
    }

    public void ClearFurniture()
    {
        if (currentFurniture != null)
        {
            Destroy(currentFurniture);
            currentFurniture = null;
        }
        isDraggingUI = false;
    }
}