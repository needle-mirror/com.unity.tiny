#if UNITY_EDITOR
﻿
using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

namespace Unity.Tiny.Rendering
{

    /**
    * Receives data from the PlayerConnection to make changes to the scene view.
    */
    [InitializeOnLoad]
    static class SceneViewConnectionService
    {
        static bool s_IsSyncCameraListenerRegistered;
        static GameObject s_CameraTransformGameObject;

        static SceneViewConnectionService()
        {
            RegisterSyncCameraCallbackListener();
        }

        static void AlignViewToCamera(Vector3 cameraLoc, Quaternion cameraRot, float fov)
        {
            if (s_CameraTransformGameObject == null)
            {
                s_CameraTransformGameObject = new GameObject();
            }

            Transform transform = s_CameraTransformGameObject.transform;
            transform.position = cameraLoc;
            transform.rotation = cameraRot;

            SceneView.lastActiveSceneView.cameraSettings.fieldOfView = fov;
            SceneView.lastActiveSceneView.AlignViewToObject(transform);
        }

        /**
         * Callback function that reads the camera data provided by the player connection
         * and moves the scene camera to its position.
         */
        static unsafe void SyncSceneCameraToGameView(MessageEventArgs args)
        {

            CameraSynchronizationMessage camInfo;

            // read float3 location, quaternion rotation, and float fov from struct
            fixed (byte* pOut = args.data)
            {
                UnsafeUtility.CopyPtrToStructure(pOut, out camInfo);
            }

            AlignViewToCamera(camInfo.position, camInfo.rotation, camInfo.fovDegrees);
        }

        static void RemoveCallBackOnQuit()
        {
            EditorConnection.instance.Unregister(SharedCameraSyncInfo.syncCameraGuid, SyncSceneCameraToGameView);
        }

        static void RegisterSyncCameraCallbackListener()
        {
            if (!s_IsSyncCameraListenerRegistered)
            {
                s_IsSyncCameraListenerRegistered = true;
                EditorConnection.instance.Register(SharedCameraSyncInfo.syncCameraGuid, SyncSceneCameraToGameView);
                EditorApplication.quitting += RemoveCallBackOnQuit;
            }
        }
    }
}
#endif
