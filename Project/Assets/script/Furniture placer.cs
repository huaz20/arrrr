using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class FurniturePlacer : MonoBehaviour
{
    [Header("AR 组件引用")]
    public ARRaycastManager raycastManager;
    public GameObject furniturePrefab; // 家具预制体，Inspector赋值
    public FurnitureManipulator manipulator; // 家具操作脚本引用

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    public GameObject spawnedFurniture; // 生成的家具对象

    void Update()
    {
        // 安卓触摸点击
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (raycastManager.Raycast(Input.GetTouch(0).position, hits, TrackableType.Planes))
            {
                Pose hitPose = hits[0].pose;

                // 如果没有家具，生成新家具
                if (spawnedFurniture == null)
                {
                    spawnedFurniture = Instantiate(furniturePrefab, hitPose.position, hitPose.rotation);
                    manipulator.SetTargetFurniture(spawnedFurniture);
                }
                // 如果已有家具，移动到点击位置
                else
                {
                    spawnedFurniture.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                }
            }
        }
    }
}