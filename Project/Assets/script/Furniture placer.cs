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
        #region 输入兼容处理（方便在电脑上运行）

        bool b_IsInputDetected = false;
        Vector2 inputPos = Vector2.zero;

        //如果在编辑器中运行，输入是来源于鼠标点击
        if (Application.isEditor)
        {
            b_IsInputDetected = Input.GetMouseButtonDown(0);
            inputPos = Input.mousePosition;
        }
        else //如果不是，输入是来源于触屏
        {
            b_IsInputDetected = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
            if(b_IsInputDetected) inputPos = Input.GetTouch(0).position;
        }

        #endregion
        
        //检测到输入
        if (b_IsInputDetected)
        {
            Debug.Log("检测到输入！");
            
            //射线模块检查
            if (raycastManager.subsystem == null)
            {
                Debug.LogError("射线检测子系统未运行！请检查 XR Plug-in Management 设置。");
                return;
            }
            
            if (raycastManager.Raycast(inputPos, hits, TrackableType.Planes))
            {
                Debug.Log("点中了平面！");
                
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
            else
            {
                Debug.LogWarning("射线发射了，但没点中任何平面。当前平面数量：" + hits.Count);
            }
        }
    }
}