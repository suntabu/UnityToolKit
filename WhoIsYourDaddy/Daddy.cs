#define daddy

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace UnityToolKit.WhoIsYourDaddy
{
    public class Daddy : MonoBehaviour
    {
        public static void Start()
        {
            var instances = GameObject.FindObjectsOfType<Daddy>();
            if (instances.Length <= 0)
            {
                var go = new GameObject("Daddy", typeof(Daddy));
            }
        }

        public const string ERROR = "{0}:\n<color=red>{1}</color>";
        public const string WARNING = "{0}:\n<color=yellow>{1}</color>";
        public const string NORMAL = "{0}:\n<color=white>{1}</color>";
        public const string DONE = ">>> DONE <<<\n";
        public const string MSG = ">> {0}";
        public const string MSG_1 = "    {0}";
        public const string MSG_2 = "     {0}";

        private static Daddy instance;
        private static bool instantiated = false;

        static List<DaddyCommandAttribute> _attributes = new List<DaddyCommandAttribute>();

        [Range(0f, 01f)] public float BackgroundOpacity = 0.5f;
        public Color BackgroundColor = Color.black;

        GUIStyle styleContainer, styleButton, styleText;
        int padding = 5;

        private bool destroying = false;
        private int mSelect = -1, mLastSelect;


        public enum PrintType
        {
            Error,
            Warning,
            Normal,
            All,
        }

        private PrintType mPrintType = PrintType.All;

        public static Daddy Instance
        {
            get
            {
                if (instantiated) return instance;

                instance = GameObject.FindObjectOfType(typeof(Daddy)) as Daddy;

                // Object not found, we create a new one
                if (instance == null)
                {
                    // Try to load the default prefab
                    try
                    {
                        instance = Instantiate(Resources.Load("DaddyPrefab", typeof(Daddy))) as Daddy;
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Failed to load default Daddy prefab...");
                        instance = new GameObject("ScreenLogger", typeof(Daddy)).GetComponent<Daddy>();
                    }

                    // Problem during the creation, this should not happen
                    if (instance == null)
                    {
                        Debug.LogError("Problem during the creation of Daddy");
                    }
                    else instantiated = true;
                }
                else
                {
                    instantiated = true;
                }

                return instance;
            }
        }

#if daddy
        public void Awake()
        {
            Application.logMessageReceived += HandleLog;


            Daddy[] obj = GameObject.FindObjectsOfType<Daddy>();

            if (obj.Length > 1)
            {
                Debug.Log("Destroying Daddy Script, already exists...");

                destroying = true;

                Destroy(gameObject);
                return;
            }

            InitStyles();

#if !UNITY_EDITOR
            DontDestroyOnLoad(this);
#endif
            _attributes = new List<DaddyCommandAttribute>(16);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        DaddyCommandAttribute[] attrs =
                            method.GetCustomAttributes(typeof(DaddyCommandAttribute), true) as DaddyCommandAttribute[];
                        if (attrs.Length == 0)
                            continue;

                        Action cbm =
                            Delegate.CreateDelegate(typeof(Action), method,
                                false) as Action;
                        if (cbm == null)
                        {
                            Debug.LogError(string.Format("Method {0}.{1} takes the wrong arguments for a rule checker.",
                                type, method.Name));
                            continue;
                        }

                        // try with a bare action
                        foreach (DaddyCommandAttribute rule in attrs)
                        {
                            rule.CommandInvoker = cbm;
                            rule.methodName = method.Name;
                            _attributes.Add(rule);

                            Debug.Log("add :" + rule.msg);
                        }
                    }
                }
            }

            inputStrs = _attributes.Select(t => t.msg).ToArray();
        }


        void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
            // If destroyed because already exists, don't need to de-register callback
            if (destroying) return;
        }

        void HandleLog(string condition, string stackTrace, LogType type)
        {
            scrollPos += Vector2.up * 200;

            if (type == LogType.Exception || type == LogType.Error)
            {
                isOpen = true;
            }

            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    AddLogEntry(PrintType.Error, Daddy.ERROR, condition, stackTrace);
                    break;
                case LogType.Warning:
                    AddLogEntry(PrintType.Warning, Daddy.WARNING, condition, stackTrace);
                    break;
                case LogType.Log:
                    AddLogEntry(PrintType.Normal, Daddy.NORMAL, condition, stackTrace);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type", type, null);
            }
        }

        void AddLogEntry(PrintType pt, string formater, string condition, string stackTrace)
        {
            var logMessage = string.Format("<b>{0}</b>\n<size=14>{1}</size>\n\n", condition, stackTrace);
            switch (pt)
            {
                case PrintType.Error:
                    errorLog.AppendFormat(formater, DateTime.Now.ToString(), logMessage);
                    break;
                case PrintType.Warning:
                    warningLog.AppendFormat(formater, DateTime.Now.ToString(), logMessage);
                    break;
                case PrintType.Normal:
                    normalLog.AppendFormat(formater, DateTime.Now.ToString(), logMessage);
                    break;
            }

            allLog.AppendFormat(formater, DateTime.Now.ToString(), logMessage);
        }

        string PrintEntries()
        {
            switch (mPrintType)
            {
                case PrintType.Error:
                    return errorLog.ToString();
                    break;
                case PrintType.Warning:
                    return warningLog.ToString();
                    break;
                case PrintType.Normal:
                    return normalLog.ToString();
                    break;
                case PrintType.All:
                    return allLog.ToString();
                    break;
            }

            return string.Empty;
        }


        void Update()
        {
            if (Input.touchCount >= 8)
            {
                isOpen = true;
            }
        }

        Rect DrawMainWindow()
        {
            float w = Screen.width;
            float h = Screen.height;
            float x = 1, y = 1;

            var rect = new Rect(x, y, w, h);
            GUILayout.BeginArea(rect, styleContainer);
            {
                GUILayout.BeginScrollView(scrollPos);
                {
                    mSelect = GUILayout.SelectionGrid(mSelect, inputStrs, 4, GUILayout.MinHeight(150));
                    if (mSelect != mLastSelect)
                    {
                        if (mSelect >= 0 && mSelect < _attributes.Count)
                        {
                            _attributes[mSelect].CommandInvoker.Invoke();
                            mSelect = -1;
                        }

                        mLastSelect = mSelect;
                    }

                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
            }
            return rect;
        }

        private float heightValue;

        Rect DrawLogWindow()
        {
            var width = GUILayout.Width(100);
            var height = GUILayout.Height(60);
            GUILayout.BeginArea(new Rect(1, 1, Screen.width - 2, 70), styleContainer);
            {
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Clear", width, height))
                    {
                        errorLog.Remove(0, errorLog.Length);
                        warningLog.Remove(0, warningLog.Length);
                        normalLog.Remove(0, normalLog.Length);
                        allLog.Remove(0, allLog.Length);
                        
                    }

                    if (GUILayout.Button("Hide", width, height))
                    {
                        Instance.OpenOrCloseWindow(false);
                    }

                    if (GUILayout.Button("Top", width, height))
                    {
                        scrollPos = Vector2.zero;
                    }

                    if (GUILayout.Button("Bottom:" + heightValue, width, height))
                    {
                        scrollPos = new Vector2(0, Mathf.Infinity);
                    }

                    if (GUILayout.Button("Up", width, height))
                    {
                        scrollPos -= Vector2.up * 50;
                    }

                    if (GUILayout.Button("Down", width, height))
                    {
                        scrollPos += Vector2.up * 50;
                    }

                    if (GUILayout.Button("Error", width, height))
                    {
                        mPrintType = PrintType.Error;
                    }

                    if (GUILayout.Button("Warning", width, height))
                    {
                        mPrintType = PrintType.Warning;
                    }

                    if (GUILayout.Button("Info", width, height))
                    {
                        mPrintType = PrintType.Normal;
                    }

                    if (GUILayout.Button("All", width, height))
                    {
                        mPrintType = PrintType.All;
                    }

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }

            var rect = new Rect(1, 70, Screen.width - 2, Screen.height - 72);
            GUILayout.BeginArea(rect, styleContainer);
            {
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    GUI.enabled = false;
                    rect.y = 1;
                    GUILayout.Label(PrintEntries(), styleText);
                    GUI.enabled = true;
                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
            }
            return rect;
        }

        void OnGUI()
        {
            if (!isOpen) return;

            if (styleText == null)
            {
                styleText = GUI.skin.textArea;
                styleText.richText = true;
                styleText.alignment = TextAnchor.LowerLeft;
            }


            Rect scrollViewRect;


            if (isLogWindowOpen)
            {
                scrollViewRect = DrawLogWindow();
            }
            else
            {
                scrollViewRect = DrawMainWindow();
            }

            /* Move by mouse drag */
            // Check if the mouse is above our scrollview.
            if (scrollViewRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    scrollPos += Event.current.delta;
                    Event.current.Use();
                }
            }
        }
#endif
        Vector2 scrollPos = Vector2.one;
        private string[] inputStrs;
        StringBuilder errorLog = new StringBuilder();
        StringBuilder warningLog = new StringBuilder();
        StringBuilder normalLog = new StringBuilder();
        StringBuilder allLog = new StringBuilder();


        private Action<string> OnUnityLog;

        public bool isOpen, isLogWindowOpen;


        public void InspectorGUIUpdated()
        {
            InitStyles();
        }

        private void InitStyles()
        {
            Texture2D back = new Texture2D(1, 1);
            BackgroundColor.a = BackgroundOpacity;
            back.SetPixel(0, 0, BackgroundColor);
            back.Apply();

            styleContainer = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.green,
                    background = back
                },
                wordWrap = true,
                padding = new RectOffset(padding, padding, padding, padding)
            };


            styleButton = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.yellow,
                    background = back
                },
                wordWrap = true,
                padding = new RectOffset(padding, padding, padding, padding)
            };
        }

        protected float m_checkValue = 0.8f;

        private Vector3 m_detalAcceleration;
        private Vector3 m_oldAcceleration;
        private Vector3 m_newAcceleration;
        private const float m_interval = 1f;
        private float m_delta = 0f;

        public void OpenOrCloseWindow(bool isOpen)
        {
            this.isLogWindowOpen = isOpen;
        }
    }


    [AttributeUsage(AttributeTargets.Method)]
    public class DaddyCommandAttribute : Attribute
    {
        public string methodName;
        public string msg;

        public DaddyCommandAttribute(string str)
        {
            msg = str;
        }


        public Action CommandInvoker;
    }


    public class DaddyCommands
    {
//        [DaddyCommand("Load a scene with its name")]
        public static string LoadScene(string sceneName)
        {
            try
            {
                SceneManager.LoadScene(sceneName);
                return Daddy.DONE;
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }

//        [DaddyCommand("Show all audioclip in scene")]
        public static string ShowPlayingSound(string para)
        {
            try
            {
                var audiosources = GameObject.FindObjectsOfType<AudioSource>();
                var sb = new StringBuilder();
                sb.Append(string.Format(Daddy.MSG, "Audios:\n"));
                foreach (var audiosource in audiosources)
                {
                    if (audiosource.clip != null)
                    {
                        var msg = string.Format(Daddy.MSG_1,
                            "[ " + audiosource.clip.name + " ] is " + (audiosource.isPlaying ? "" : " NOT ") +
                            " playing\n");
                        sb.Append(msg);
                    }
                }

                return sb.ToString();
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }

//        [DaddyCommand("Check whether a gameobject exists")]
        public static string CheckGameObjectExist(string path)
        {
            try
            {
                //TODO:
                return Daddy.DONE;
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }

//        [DaddyCommand("Write key/STRING value to playerprefs, use ',' to split")]
        public static string WriteKeyStringValue(string vals)
        {
            try
            {
                var strs = vals.Split(',');
                var key = strs[0];
                var newValue = strs[1];
                var oldStr = PlayerPrefs.GetString(key);
                PlayerPrefs.SetString(key, newValue);
                PlayerPrefs.Save();

                return string.Format(Daddy.MSG, key + " : " + oldStr + " --> " + newValue);
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }

//        [DaddyCommand("Write key/INT value to playerprefs, use ',' to split")]
        public static string WriteKeyIntValue(string vals)
        {
            try
            {
                var strs = vals.Split(',');
                var key = strs[0];
                var newValue = int.Parse(strs[1]);
                var oldStr = PlayerPrefs.GetInt(key);
                PlayerPrefs.SetInt(key, newValue);
                PlayerPrefs.Save();

                return string.Format(Daddy.MSG, key + " : " + oldStr + " --> " + newValue);
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }


//        [DaddyCommand("Write key/FLOAT value to playerprefs, use ',' to split")]
        public static string WriteKeyFloatValue(string vals)
        {
            try
            {
                var strs = vals.Split(',');
                var key = strs[0];
                var newValue = float.Parse(strs[1]);
                var oldStr = PlayerPrefs.GetFloat(key);
                PlayerPrefs.SetFloat(key, newValue);
                PlayerPrefs.Save();

                return string.Format(Daddy.MSG, key + " : " + oldStr + " --> " + newValue);
            }
            catch (Exception e)
            {
                return string.Format(Daddy.ERROR, e.Message);
            }
        }

        [DaddyCommand("Show log window")]
        public static void ShowLogWindow()
        {
            Daddy.Instance.OpenOrCloseWindow(true);
        }

        [DaddyCommand("Hide This")]
        public static void HideMainWindow()
        {
            Daddy.Instance.isOpen = false;
        }
    }
}