using UnityToolKit.Utils;

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

        public static bool IsSomethingEnabled;


        private int areaInPixel;
        public float areaRatio = 0.1f;

        public CommandPosEnum[] CommandPositions;
        private int currentInx;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            areaInPixel = (int) (Screen.width * areaRatio);
        }

        private void Update()
        {
            if (CommandPositions.IsEmpty()) return;

            if (currentInx >= CommandPositions.Length)
            {
                currentInx = 0;
                IsSomethingEnabled = !IsSomethingEnabled;
                Debug.LogWarning("Succeed ---> enabled " + IsSomethingEnabled);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (ValidateClickPos(CommandPositions[currentInx]))
                {
                    Debug.Log("passed: " + CommandPositions[currentInx] + "    <----" + currentInx);
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
                case CommandPosEnum.Top:
                    return Vector2.Distance(p, new Vector2(Screen.width * 0.5f, Screen.height)) <= areaInPixel;
                case CommandPosEnum.RightTop:
                    return Vector2.Distance(p, new Vector2(Screen.width, Screen.height)) <= areaInPixel;
                case CommandPosEnum.Right:
                    return Vector2.Distance(p, new Vector2(Screen.width, Screen.height * 0.5f)) <= areaInPixel;
                case CommandPosEnum.RightBottom:
                    return Vector2.Distance(p, new Vector2(Screen.width, 0)) <= areaInPixel;
                case CommandPosEnum.Bottom:
                    return Vector2.Distance(p, new Vector2(Screen.width * .5f, 0)) <= areaInPixel;
                case CommandPosEnum.LeftBottom:
                    return Vector2.Distance(p, new Vector2(0, 0)) <= areaInPixel;
                case CommandPosEnum.Left:
                    return Vector2.Distance(p, new Vector2(0, Screen.height * 0.5f)) <= areaInPixel;
            }


            return false;
        }
    }
}