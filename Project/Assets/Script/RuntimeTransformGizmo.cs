using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

public class FixedGizmoController : MonoBehaviour
{
    [Header("核心设置")]
    public Camera mainCamera;
    public Transform gizmoRoot;
    public LayerMask furnitureLayer;
    public LayerMask gizmoLayer;

    [Header("地面检测")]
    public float minYOffset = 0.01f;

    [Header("UI 绑定")]
    public GameObject confirmButtonObj;

    [Header("遥控灵敏度")]
    public float moveSpeed = 0.002f;
    public float rotateSpeed = 0.3f;
    public float scaleSpeed = 0.002f;

    private Transform targetFurniture;
    private bool isEditing = false;
    private Collider furnitureCollider;
    private float furnitureHeight = 0.5f;

    private enum GizmoPart { None, MoveX, MoveY, MoveZ, RotateX, RotateY, RotateZ, ScaleCenter }
    private GizmoPart currentPart = GizmoPart.None;
    private Vector2 lastScreenPos;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(false);
        if (confirmButtonObj != null) confirmButtonObj.SetActive(false);
    }

    void Update()
    {
        Vector2 screenPos = Vector2.zero;
        bool isPointerDown = false;
        bool isPointerPressed = false;
        bool isPointerUp = false;

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

        if (isPointerUp)
        {
            currentPart = GizmoPart.None;
        }

        if (IsPointerOverUI(screenPos) && currentPart == GizmoPart.None) return;

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

        if (Physics.Raycast(ray, out RaycastHit gizmoHit, 100f, gizmoLayer))
        {
            string partName = gizmoHit.collider.gameObject.name;
            ParseGizmoPart(partName);
            lastScreenPos = screenPos;
            return;
        }

        if (isEditing) return;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, furnitureLayer))
        {
            targetFurniture = hit.collider.transform.root;
            furnitureCollider = targetFurniture.GetComponent<Collider>();
            if (furnitureCollider == null)
                furnitureCollider = targetFurniture.GetComponentInChildren<Collider>();

            CalculateFurnitureHeight();
            SnapToGround();  // 选中时吸附地面

            isEditing = true;

            if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(true);
            if (confirmButtonObj != null) confirmButtonObj.SetActive(true);
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
                if (newScale.x > 0.05f)
                {
                    targetFurniture.localScale = newScale;
                    CalculateFurnitureHeight();
                }
                break;
        }

        // 每次移动后都吸附地面（X/Z移动时必须）
        if (currentPart == GizmoPart.MoveX || currentPart == GizmoPart.MoveZ)
        {
            SnapToGround();
        }

        lastScreenPos = screenPos;
    }

    /// <summary>
    /// 核心：自动找到家具下方的地面并吸附
    /// </summary>
    void SnapToGround()
    {
        if (targetFurniture == null) return;

        // 从家具中心向下发射射线
        Vector3 rayOrigin = targetFurniture.position;
        Ray ray = new Ray(rayOrigin, Vector3.down);

        // 获取所有碰撞体（不排除任何层）
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f);

        float closestGroundY = -Mathf.Infinity;

        foreach (RaycastHit hit in hits)
        {
            // 排除家具自身
            if (hit.collider.transform.root == targetFurniture) continue;

            // 这个就是地面（或其他物体），取最靠近家具的那个（Y值最大的）
            if (hit.point.y > closestGroundY)
            {
                closestGroundY = hit.point.y;
            }
        }

        // 如果找到了地面
        if (closestGroundY > -Mathf.Infinity)
        {
            float targetY = closestGroundY + furnitureHeight / 2f + minYOffset;
            Vector3 newPos = targetFurniture.position;
            newPos.y = targetY;
            targetFurniture.position = newPos;
        }
    }

    void CalculateFurnitureHeight()
    {
        if (furnitureCollider != null)
        {
            furnitureHeight = furnitureCollider.bounds.size.y;
        }
        else
        {
            Renderer renderer = targetFurniture.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                furnitureHeight = renderer.bounds.size.y;
            }
            else
            {
                furnitureHeight = 0.5f;
            }
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

    public void OnConfirmEditButtonClicked()
    {
        if (!isEditing) return;

        isEditing = false;
        targetFurniture = null;
        furnitureCollider = null;

        if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(false);
        if (confirmButtonObj != null) confirmButtonObj.SetActive(false);
    }
}