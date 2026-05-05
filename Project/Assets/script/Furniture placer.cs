using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FurniturePlacer : MonoBehaviour
{
    [Header("AR 组件")]
    public ARRaycastManager raycastManager;
    public GameObject furniturePrefab;
    public FurnitureManipulator manipulator;

    [Header("UI 设置")]
    public RectTransform dragUI;
    public Canvas parentCanvas;
    public Button clearAllButton; // 一键清除按钮

    [Header("放置设置")]
    public float maxPlacementDistance = 5f;
    public float minPlacementDistance = 0.3f;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private List<GameObject> spawnedFurniture = new List<GameObject>();
    private GameObject currentDraggingFurniture;
    private bool isDraggingUI = false;
    private bool hasMovedEnough = false;
    private Vector2 dragStartPosition;

    void Start()
    {
        // 绑定清除按钮事件
        if (clearAllButton != null)
        {
            clearAllButton.onClick.AddListener(ClearAllFurniture);
            Debug.Log("✅ 清除按钮已绑定");
        }
        else
        {
            Debug.LogWarning("⚠️ clearAllButton 未赋值，请在 Inspector 中绑定");
        }
    }

    void Update()
    {
        HandleInput();

        if (isDraggingUI && currentDraggingFurniture != null)
        {
            UpdateFurniturePosition();
        }
    }

    void HandleInput()
    {
        if (Application.isEditor)
        {
            if (IsDraggingOnUI())
            {
                HandleUIDrag();
            }
        }
        else
        {
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
            if (!hasMovedEnough && currentDraggingFurniture == null)
            {
                Vector2 currentPos = Mouse.current.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartPosition, currentPos);

                if (dragDistance > 20f)
                {
                    hasMovedEnough = true;
                    TrySpawnFurniture(currentPos);
                }
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDraggingUI = false;
            hasMovedEnough = false;
            currentDraggingFurniture = null;
        }
    }
    #endregion

    #region 手机端逻辑
    void HandleMobileInput()
    {
        if (Touchscreen.current == null) return;
        var touch = Touchscreen.current.primaryTouch;

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

        if (isDraggingUI && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
        {
            if (!hasMovedEnough && currentDraggingFurniture == null)
            {
                Vector2 currentPos = touch.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartPosition, currentPos);

                if (dragDistance > 15f)
                {
                    hasMovedEnough = true;
                    TrySpawnFurniture(currentPos);
                }
            }
        }

        if (isDraggingUI && (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                             touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled))
        {
            isDraggingUI = false;
            hasMovedEnough = false;
            currentDraggingFurniture = null;
        }
    }

    bool IsTouchOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // 检查是否点到拖拽UI
        foreach (var result in results)
        {
            if (result.gameObject == dragUI.gameObject ||
                result.gameObject.transform.IsChildOf(dragUI))
            {
                return true;
            }
        }

        // 检查是否点到清除按钮（如果点到按钮，不开始拖拽）
        if (clearAllButton != null)
        {
            foreach (var result in results)
            {
                if (result.gameObject == clearAllButton.gameObject ||
                    result.gameObject.transform.IsChildOf(clearAllButton.transform))
                {
                    return false; // 点到按钮，不开始拖拽
                }
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

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            return touch.position.ReadValue();
        }

        return new Vector2(Screen.width / 2, Screen.height / 2);
    }

    void TrySpawnFurniture(Vector2 screenPos)
    {
        if (raycastManager == null || furniturePrefab == null) return;

        // 射线检测，只检测带边界的平面
        if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon)) return;
        if (hits.Count == 0) return;

        // 获取击中点
        Pose hitPose = hits[0].pose;

        // 检查距离
        float distance = Vector3.Distance(Camera.main.transform.position, hitPose.position);
        if (distance < minPlacementDistance || distance > maxPlacementDistance) return;

        // 创建物体
        GameObject newFurniture = Instantiate(furniturePrefab, hitPose.position, hitPose.rotation);

        // 调整位置，让物体底部对齐平面
        AdjustToGroundPlane(newFurniture);

        spawnedFurniture.Add(newFurniture);
        currentDraggingFurniture = newFurniture;

        Debug.Log($"✅ 生成家具，当前总数: {spawnedFurniture.Count}");

        if (manipulator != null)
            manipulator.SetTargetFurniture(newFurniture);
    }

    void AdjustToGroundPlane(GameObject furniture)
    {
        float bottomOffset = GetBottomOffset(furniture);
        Vector3 currentPos = furniture.transform.position;
        currentPos.y = currentPos.y - bottomOffset;
        furniture.transform.position = currentPos;
    }

    float GetBottomOffset(GameObject obj)
    {
        // 使用 Renderer 的 bounds
        Renderer renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            return bounds.min.y - obj.transform.position.y;
        }

        // 使用 Collider
        Collider collider = obj.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            return bounds.min.y - obj.transform.position.y;
        }

        // 组合所有 Renderer
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }
            return combinedBounds.min.y - obj.transform.position.y;
        }

        return 0f;
    }

    void UpdateFurniturePosition()
    {
        if (currentDraggingFurniture == null) return;

        Vector2 screenPos = GetCurrentScreenPosition();

        if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon)) return;
        if (hits.Count == 0) return;

        Pose hitPose = hits[0].pose;

        currentDraggingFurniture.transform.position = hitPose.position;
        currentDraggingFurniture.transform.rotation = hitPose.rotation;
        AdjustToGroundPlane(currentDraggingFurniture);
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
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 一键清除所有家具
    /// </summary>
    public void ClearAllFurniture()
    {
        int count = spawnedFurniture.Count;

        foreach (GameObject furniture in spawnedFurniture)
        {
            if (furniture != null)
                Destroy(furniture);
        }

        spawnedFurniture.Clear();
        currentDraggingFurniture = null;
        isDraggingUI = false;

        Debug.Log($"🗑️ 已清除 {count} 个家具");
    }

    /// <summary>
    /// 清除最后一个家具
    /// </summary>
    public void ClearLastFurniture()
    {
        if (spawnedFurniture.Count > 0)
        {
            GameObject lastFurniture = spawnedFurniture[spawnedFurniture.Count - 1];
            if (lastFurniture != null)
                Destroy(lastFurniture);
            spawnedFurniture.RemoveAt(spawnedFurniture.Count - 1);

            if (currentDraggingFurniture == lastFurniture)
                currentDraggingFurniture = null;

            Debug.Log($"🗑️ 已清除最后一个家具，剩余: {spawnedFurniture.Count}个");
        }
    }
}