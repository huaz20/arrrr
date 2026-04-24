using UnityEngine;

public class FurnitureManipulator : MonoBehaviour
{
    [Header("操作参数")]
    public float rotationSpeed = 0.1f;
    public float scaleSpeed = 0.01f;
    public Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 maxScale = new Vector3(2f, 2f, 2f);

    private GameObject targetFurniture;
    private Vector2 touch0PrevPos, touch1PrevPos;

    void Update()
    {
        if (targetFurniture == null) return;

        // 单指滑动 → 旋转
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                float rotationDelta = touch.deltaPosition.x * rotationSpeed;
                targetFurniture.transform.Rotate(Vector3.up, rotationDelta, Space.World);
            }
        }

        // 双指操作 → 缩放 + 旋转
        else if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // 双指上一帧位置
            Vector2 touch0Pos = touch0.position;
            Vector2 touch1Pos = touch1.position;

            // 缩放逻辑
            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0Pos - touch1Pos).magnitude;
            float scaleDelta = (currentMagnitude - prevMagnitude) * scaleSpeed;

            Vector3 newScale = targetFurniture.transform.localScale + Vector3.one * scaleDelta;
            newScale = new Vector3(
                Mathf.Clamp(newScale.x, minScale.x, maxScale.x),
                Mathf.Clamp(newScale.y, minScale.y, maxScale.y),
                Mathf.Clamp(newScale.z, minScale.z, maxScale.z)
            );
            targetFurniture.transform.localScale = newScale;

            // 双指旋转
            float angle = Vector2.SignedAngle(touch1Pos - touch0Pos, touch1PrevPos - touch0PrevPos);
            targetFurniture.transform.Rotate(Vector3.up, -angle * rotationSpeed, Space.World);

            // 更新上一帧位置
            touch0PrevPos = touch0Pos;
            touch1PrevPos = touch1Pos;
        }
    }

    // 设置当前要操作的家具
    public void SetTargetFurniture(GameObject furniture)
    {
        targetFurniture = furniture;
    }
}