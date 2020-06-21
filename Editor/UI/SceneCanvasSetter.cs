using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityToolKit.UI
{
    public class SceneCanvasSetter
    {
        [MenuItem("UnityToolKit/Reset Canvas!")]
        public static void ResetCanvas()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            CanvasScaler canvasScaler = null;
            if (canvas == null)
            {
                canvas = new GameObject("Canvas").AddComponent<Canvas>();
                canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvasScaler = canvas.GetComponent<CanvasScaler>();
            }


            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(768, 1136);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            canvasScaler.matchWidthOrHeight = 0;
            canvasScaler.referencePixelsPerUnit = 100;
        }
    }
}