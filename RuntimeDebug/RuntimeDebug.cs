using UnityToolKit.Utils;
using System;
using UnityEngine.Events;

namespace UnityEngine
{
    public class RuntimeDebug : MonoBehaviour
    {
        public enum CommandPosEnum
        {
            LeftTop,
            Top,
            RightTop,
            Right,
            RightBottom,
            Bottom,
            LeftBottom,
            Left
        }


        public float areaRatio = 0.1f;
        public CommandPosEnum[] CommandPositions;
        public event Action<bool> OnEnableStateChanged;

        private int areaInPixel;
        private int currentInx;

        [HideInInspector] public bool IsSomethingEnabled = false;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            areaInPixel = (int) (Screen.width * areaRatio);
        }

        private void Update()
        {
            if (CommandPositions.IsEmpty())
            {
                return;
            }

            if (currentInx >= CommandPositions.Length)
            {
                currentInx = 0;
                IsSomethingEnabled = !IsSomethingEnabled;
                if (OnEnableStateChanged != null)
                    OnEnableStateChanged(IsSomethingEnabled);
                Debug.LogWarning("Succeed: " + name + "enabled " + IsSomethingEnabled);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (ValidateClickPos(CommandPositions[currentInx]))
                {
                    Debug.Log("Passed: " + name + "     " + CommandPositions[currentInx] + "    <----" + currentInx);
                    currentInx++;
                }
                else
                {
                    // Debug.Log("failed: " + CommandPositions[currentInx] + "    <----" + currentInx);

                    currentInx = 0;
                }
            }
        }

        private bool ValidateClickPos(CommandPosEnum targetPosEnum)
        {
            var p = Input.mousePosition;
            switch (targetPosEnum)
            {
                case CommandPosEnum.LeftTop:
                    return Vector2.Distance(p, new Vector2(0, Screen.height)) <= areaInPixel;
                    break;
                case CommandPosEnum.Top:
                    return Vector2.Distance(p, new Vector2(Screen.width * 0.5f, Screen.height)) <= areaInPixel;
                    break;
                case CommandPosEnum.RightTop:
                    return Vector2.Distance(p, new Vector2(Screen.width, Screen.height)) <= areaInPixel;
                    break;
                case CommandPosEnum.Right:
                    return Vector2.Distance(p, new Vector2(Screen.width, Screen.height * 0.5f)) <= areaInPixel;
                    break;
                case CommandPosEnum.RightBottom:
                    return Vector2.Distance(p, new Vector2(Screen.width, 0)) <= areaInPixel;
                    break;
                case CommandPosEnum.Bottom:
                    return Vector2.Distance(p, new Vector2(Screen.width * .5f, 0)) <= areaInPixel;
                    break;
                case CommandPosEnum.LeftBottom:
                    return Vector2.Distance(p, new Vector2(0, 0)) <= areaInPixel;
                    break;
                case CommandPosEnum.Left:
                    return Vector2.Distance(p, new Vector2(0, Screen.height * 0.5f)) <= areaInPixel;
                    break;
            }


            return false;
        }

        public static void CreateRuntimeCommand(string cmdName, RuntimeDebug.CommandPosEnum[] cmds,
            Action<bool> onEnableStateChanged)
        {
            var runtimeScript = new GameObject(cmdName).AddComponent<RuntimeDebug>();
            runtimeScript.CommandPositions = cmds;
            runtimeScript.OnEnableStateChanged += onEnableStateChanged;
        }
    }
}
