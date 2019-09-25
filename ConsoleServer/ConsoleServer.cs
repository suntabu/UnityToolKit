// TODO:open this to enable this script

//#define CONSOLE_SERVER

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace UnityToolKit.ConsoleServer
{
    public class ConsoleServer : MonoBehaviour
    {
        // We can't use the behaviour reference from other threads, so we use a separate bool
        // to track the instance so we can use that on the other threads.
        private static bool _instanceExists;

        private static Thread _mainThread;
        private static object _lockObject = new object();
        private static readonly Queue<Action> _actions = new Queue<Action>();

        /// <summary>
        /// Gets a value indicating whether or not the current thread is the game's main thread.
        /// </summary>
        public static bool isMainThread
        {
            get { return Thread.CurrentThread == _mainThread; }
        }

        /// <summary>
        /// Queues an action to be invoked on the main game thread.
        /// </summary>
        /// <param name="action">The action to be queued.</param>
        public static void InvokeAsync(Action action)
        {
            if (isMainThread)
            {
                // Don't bother queuing work on the main thread; just execute it.
                action();
            }
            else
            {
                lock (_lockObject)
                {
                    _actions.Enqueue(action);
                }
            }
        }

        /// <summary>
        /// Queues an action to be invoked on the main game thread and blocks the
        /// current thread until the action has been executed.
        /// </summary>
        /// <param name="action">The action to be queued.</param>
        public static void Invoke(Action action)
        {
            bool hasRun = false;

            InvokeAsync(() =>
            {
                action();
                hasRun = true;
            });

            // Lock until the action has run
            while (!hasRun)
            {
                Thread.Sleep(5);
            }
        }

        void Awake()
        {
            _mainThread = Thread.CurrentThread;
            fileRoot = Application.persistentDataPath + "/";
            cacheRoot = Application.temporaryCachePath + "/";
            DontDestroyOnLoad(this.gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Stop();
                mInstance = null;
            }
        }

        void Update()
        {
            lock (_lockObject)
            {
                while (_actions.Count > 0)
                {
                    _actions.Dequeue()();
                }
            }
        }

#if CONSOLE_SERVER
        private void OnGUI()
        {
            if (isStarted)
            {
                var str = "Console Server ：" + (isRunning ? "Running" : "Stopped");
                GUI.color = Color.black;
                GUI.Label(new Rect(4, 4, 220, 30), str);
                GUI.Label(new Rect(5, 4, 220, 30), str);
                GUI.Label(new Rect(4, 5, 220, 30), str);
                GUI.color = Color.white;
                GUI.Label(new Rect(4, 3, 220, 30), str);
            }

            if (!isRunning)
            {
                StartServer();
            }
        }
#endif

        private int mPort = 55055;

        private bool isStarted
        {
            get { return listener != null; }
        }

        private bool isRunning
        {
            get { return listener != null && listener.IsListening && mRunningThread != null && mRunningThread.IsAlive; }
        }

        public bool mRegisterLogCallback = false;

        #region singleton

        private static ConsoleServer mInstance;

        public static ConsoleServer Instance
        {
            get
            {
                if (!mInstance)
                {
                    mInstance = FindObjectOfType<ConsoleServer>();
                }

                if (!mInstance)
                {
                    mInstance = new GameObject("ConsoleServer").AddComponent<ConsoleServer>();
                }

                return mInstance;
            }
        }

        public int Port
        {
            get { return mPort; }
        }

        public string IP
        {
            get
            {
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
#if UNITY_EDITOR
                    NetworkInterfaceType _type1 = NetworkInterfaceType.Wireless80211;
                    NetworkInterfaceType _type2 = NetworkInterfaceType.Ethernet;

                    if ((item.NetworkInterfaceType == _type1 || item.NetworkInterfaceType == _type2) && item.OperationalStatus == OperationalStatus.Up)
#endif 
                    {
                        foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                var instr = ip.Address.ToString();
                                Debug.Log("IP=> " + instr);
                                if (instr.StartsWith("10.") || instr.StartsWith("192.") || instr.StartsWith("172."))
                                {
                                    return instr;
                                }
                            }
                        }
                    }
                }

                return "localhost";
            }
        }

        #endregion singleton

        private Thread mRunningThread;

        private static string fileRoot, cacheRoot;
        private static HttpListener listener;
        private static List<RouteAttribute> registeredRoutes;

        internal static Dictionary<string, Func<string, string>> customActions =
            new Dictionary<string, Func<string, string>>();

        // List of supported files
        // FIXME add an api to register new types
        private static Dictionary<string, string> fileTypes = new Dictionary<string, string>
        {
            {"js", "application/javascript"},
            {"json", "application/json"},
            {"meta", "application/json"},
            {"jpg", "image/jpeg"},
            {"jpeg", "image/jpeg"},
            {"gif", "image/gif"},
            {"png", "image/png"},
            {"css", "text/css"},
            {"htm", "text/html"},
            {"html", "text/html"},
            {"ico", "image/x-icon"},
            {"log", "text/html"},
            {"txt", "text/html"},
        };

        private static Dictionary<string, string> internalRes = new Dictionary<string, string>
        {
            {"index.html", Res.INDEX_HTML},
            {"console.css", Res.INDEX_CSS},
            {"favicon.ico", Res.INDEX_ICO},
            {"upload.html", Res.UPLOAD_HTML}
        };

        private void RegisterRoutes()
        {
            if (registeredRoutes == null)
            {
                registeredRoutes = new List<RouteAttribute>();

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        // FIXME add support for non-static methods (FindObjectByType?)
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            RouteAttribute[] attrs =
                                method.GetCustomAttributes(typeof(RouteAttribute), true) as RouteAttribute[];
                            if (attrs.Length == 0)
                                continue;

                            RouteAttribute.Callback cbm =
                                Delegate.CreateDelegate(typeof(RouteAttribute.Callback), method, false) as
                                    RouteAttribute.Callback;
                            if (cbm == null)
                            {
                                Debug.LogError(string.Format(
                                    "Method {0}.{1} takes the wrong arguments for a console route.", type,
                                    method.Name));
                                continue;
                            }

                            // try with a bare action
                            foreach (RouteAttribute route in attrs)
                            {
                                if (route.m_route == null)
                                {
                                    Debug.LogError(string.Format("Method {0}.{1} needs a valid route regexp.", type,
                                        method.Name));
                                    continue;
                                }

                                route.m_callback = cbm;
                                registeredRoutes.Add(route);
                            }
                        }
                    }
                }

                RegisterFileHandlers();
            }
        }

        public delegate void FileHandlerDelegate(RequestContext context, bool download);

        static void WWWFileHandler(RequestContext context, bool download)
        {
            string path, type;
            FindFileType(context, download, out path, out type);

            WWW req = new WWW(path);
            while (!req.isDone)
            {
                Thread.Sleep(0);
            }

            if (string.IsNullOrEmpty(req.error))
            {
                context.Response.ContentType = type;
                if (download)
                    context.Response.AddHeader("Content-disposition",
                        string.Format("attachment; filename={0}", Path.GetFileName(path)));

                context.Response.WriteBytes(req.bytes);
                return;
            }

            if (req.error.StartsWith("Couldn't open file"))
            {
                context.pass = true;
            }
            else
            {
                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                context.Response.StatusDescription = string.Format("Fatal error:\n{0}", req.error);
            }
        }

        static void FileHandler(RequestContext context, bool download)
        {
            string path, type;
            FindFileType(context, download, out path, out type);

            if (File.Exists(path))
            {
                context.Response.WriteFile(path, type, download);
            }
            else
            {
                var cachePath = path.Replace(fileRoot, cacheRoot);
                if (File.Exists(cachePath))
                {
                    context.Response.WriteFile(cachePath, type, download);
                    return;
                }

                var filename = Path.GetFileName(path);
                if (internalRes.ContainsKey(filename))
                    context.Response.WriteBytes(Convert.FromBase64String(internalRes[filename]));
                else
                {
                    context.pass = true;
                }
            }
        }

        static void RegisterFileHandlers()
        {
            string pattern = string.Format("({0})", string.Join("|", fileTypes.Select(x => x.Key).ToArray()));
            RouteAttribute downloadRoute = new RouteAttribute(string.Format(@"^/download/", pattern));
            RouteAttribute fileRoute = new RouteAttribute(string.Format(@"^/(.*\.{0})$", pattern));

            bool needs_www = fileRoot.Contains("://");
            downloadRoute.m_runOnMainThread = needs_www;
            fileRoute.m_runOnMainThread = needs_www;

            FileHandlerDelegate callback = FileHandler;
            if (needs_www)
                callback = WWWFileHandler;

            downloadRoute.m_callback = delegate(RequestContext context) { callback(context, true); };
            fileRoute.m_callback = delegate(RequestContext context) { callback(context, false); };

            registeredRoutes.Add(downloadRoute);
            registeredRoutes.Add(fileRoute);
        }

        static void FindFileType(RequestContext context, bool download, out string path, out string type)
        {
            if (download)
                path = Path.Combine(fileRoot, context.path.Replace(context.match.ToString(), ""));
            else
            {
                path = Path.Combine(fileRoot, context.match.Groups[1].Value);
            }

            string ext = Path.GetExtension(path).ToLower().TrimStart(new char[] {'.'});
            if (download || !fileTypes.TryGetValue(ext, out type))
                type = "application/octet-stream";
        }

        void HandleRequest(RequestContext context)
        {
            RegisterRoutes();

            try
            {
                bool handled = false;

                for (; context.currentRoute < registeredRoutes.Count; ++context.currentRoute)
                {
                    RouteAttribute route = registeredRoutes[context.currentRoute];
                    Match match = route.m_route.Match(context.path);
//                    if (route.m_route.ToString() != "^/console/out$")
//                    {
//                        Debug.LogError(route.m_route + "  --->  " + context.path + "   " + match.Success);    
//                    }

                    if (!match.Success)
                        continue;

                    if (!route.m_methods.IsMatch(context.Request.HttpMethod))
                        continue;

                    // Upgrade to main thread if necessary
                    if (route.m_runOnMainThread && Thread.CurrentThread != _mainThread)
                    {
                        Invoke(() =>
                        {
                            context.match = match;
                            route.m_callback(context);
                        });

                        handled = !context.pass;
                        if (handled)
                            break;
                    }
                    else
                    {
                        context.match = match;
                        route.m_callback(context);
                        handled = !context.pass;
                        if (handled)
                            break;
                    }
                }

                if (!handled)
                {
                    context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                    context.Response.StatusDescription = "Not Found";
                }
            }
            catch (Exception exception)
            {
                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                context.Response.StatusDescription = string.Format("Fatal error:\n{0}", exception);

                Debug.LogException(exception);
            }

            context.Response.OutputStream.Close();
        }

        void HandleRequests()
        {
            while (true)
            {
                Thread.Sleep(16);
                if (listener == null || !listener.IsListening)
                {
                    continue;
                }

                var context = listener.GetContext();
                RequestContext rc = new RequestContext(context);
                HandleRequest(rc);
            }
        }


        public void StartServer(int port = 55055, bool isRegisterLogCallback = true)
        {
#if !CONSOLE_SERVER
            return;
#endif

            if (listener == null)
            {
                listener = new HttpListener();
            }

            if (!listener.IsListening)
            {
                mPort = port;
                Debug.Log("Starting Console Server on port : " + mPort);
                listener.Prefixes.Add("http://*:" + mPort + "/");
                listener.Start();
            }

            if (mRunningThread == null)
            {
                mRunningThread = new Thread(HandleRequests);
            }

            if (!mRunningThread.IsAlive)
                mRunningThread.Start();

            mRegisterLogCallback = isRegisterLogCallback;
            // Start server


            if (mRegisterLogCallback)
            {
                // Capture Console Logs
#if UNITY_5_3_OR_NEWER
                Application.logMessageReceived += Console.LogCallback;
#else
        Application.RegisterLogCallback(Console.LogCallback);
#endif
            }
        }

        public void Stop()
        {
            try
            {
                Debug.Log("Stop listening at :" + mPort);
                if (mRegisterLogCallback)
                {
#if UNITY_5_3_OR_NEWER
                    Application.logMessageReceived -= Console.LogCallback;
#else
        Application.RegisterLogCallback(null);
#endif
                }

                if (listener != null)
                {
                    listener.Stop();
                    listener.Close();
                    listener = null;
                }

                if (mRunningThread != null)
                {
                    mRunningThread.Abort();
                    mRunningThread = null;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message + "\n" + e.StackTrace);
            }
        }

        public void AddCustomAction(string key, Func<string, string> action)
        {
            customActions[key] = action;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action">the action to execute lua command, in Xlua it is LuaManager.Instance.SafeDoString(command)</param>
        /// <param name="filterKey">the key for filter lua command result log for real time viewing</param>
        public void AddLuaStartAction(Func<string, string> action, string filterKey = "dostring")
        {
            customActions[Res.LUA_ENTER_KEY] = action;
            Console.LUA_FILTER_KEY = filterKey;
        }
    }

    public class RequestContext
    {
        public HttpListenerContext context;
        public Match match;
        public bool pass;
        public string path;
        public int currentRoute;

        public HttpListenerRequest Request
        {
            get { return context.Request; }
        }

        public HttpListenerResponse Response
        {
            get { return context.Response; }
        }

        public RequestContext(HttpListenerContext ctx)
        {
            context = ctx;
            match = null;
            pass = false;
            path = WWW.UnEscapeURL(context.Request.Url.AbsolutePath);
            if (path == "/")
                path = "/index.html";
            currentRoute = 0;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        public delegate void CallbackSimple();

        public delegate void Callback(string[] args);

        public CommandAttribute(string cmd, string help, bool runOnMainThread = true)
        {
            m_command = cmd;
            m_help = help;
            m_runOnMainThread = runOnMainThread;
        }

        public string m_command;
        public string m_help;
        public bool m_runOnMainThread;
        public Callback m_callback;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RouteAttribute : Attribute
    {
        public delegate void Callback(RequestContext context);

        public RouteAttribute(string route, string methods = @"(GET|HEAD)", bool runOnMainThread = false)
        {
            m_route = new Regex(route, RegexOptions.IgnoreCase);
            m_methods = new Regex(methods);
            m_runOnMainThread = runOnMainThread;
        }

        public Regex m_route;
        public Regex m_methods;
        public bool m_runOnMainThread;
        public Callback m_callback;
    }


    public static class ResponseExtension
    {
        public static void WriteError(this HttpListenerResponse response, HttpStatusCode code, string statusDesc,
            string input, string type = "text/plain")
        {
            response.StatusCode = (int) code;
            response.StatusDescription = statusDesc;

            if (!string.IsNullOrEmpty(input))
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                response.ContentLength64 = buffer.Length;
                response.ContentType = type;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }

        public static void WriteString(this HttpListenerResponse response, string input, string type = "text/plain")
        {
            response.StatusCode = (int) HttpStatusCode.OK;
            response.StatusDescription = "OK";

            if (!string.IsNullOrEmpty(input))
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                response.ContentLength64 = buffer.Length;
                response.ContentType = type;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }

        public static void WriteBytes(this HttpListenerResponse response, byte[] bytes)
        {
            response.StatusCode = (int) HttpStatusCode.OK;
            response.StatusDescription = "OK";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteFile(this HttpListenerResponse response, string path,
            string type = "application/octet-stream", bool download = false)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                response.StatusCode = (int) HttpStatusCode.OK;
                response.StatusDescription = "OK";
                response.ContentLength64 = fs.Length;
                response.ContentType = type;
                if (download)
                    response.AddHeader("Content-disposition",
                        string.Format("attachment; filename={0}", Path.GetFileName(path)));

                byte[] buffer = new byte[64 * 1024];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // FIXME required?
                    System.Threading.Thread.Sleep(0);
                    response.OutputStream.Write(buffer, 0, read);
                }
            }
        }
    }


    internal class Res
    {
        public static string INDEX_HTML =
            "PCFET0NUWVBFIGh0bWw+CjxodG1sPgo8aGVhZD4KICAgIDxsaW5rIHJlbD0ic3R5bGVzaGVldCIgdHlwZT0idGV4dC9jc3MiIGhyZWY9ImNvbnNvbGUuY3NzIj4KICAgIDxsaW5rIHJlbD0ic2hvcnRjdXQgaWNvbiIgaHJlZj0iZmF2aWNvbi5pY28iIHR5cGU9ImltYWdlL3gtaW1hZ2UiPgogICAgPGxpbmsgcmVsPSJpY29uIiBocmVmPSJmYXZpY29uLmljb24iIHR5cGU9ImltYWdlL3gtaW1hZ2UiPgogICAgPHRpdGxlPkNvbnNvbGUgU2VydmVyPC90aXRsZT4KCiAgICA8c2NyaXB0IHNyYz0iaHR0cDovL2FqYXguZ29vZ2xlYXBpcy5jb20vYWpheC9saWJzL2pxdWVyeS8xLjEwLjIvanF1ZXJ5Lm1pbi5qcyI+CiAgICA8L3NjcmlwdD4KCiAgICA8c2NyaXB0PgogICAgICAgIHZhciBjb21tYW5kSW5kZXggPSAtMTsKICAgICAgICB2YXIgaGFzaCA9IG51bGw7CiAgICAgICAgdmFyIGlzVXBkYXRlUGF1c2VkID0gZmFsc2U7CiAgICAgICAgdmFyIF9kYXRhID0gIiI7CgogICAgICAgIGZ1bmN0aW9uIHNjcm9sbEJvdHRvbSgpIHsKICAgICAgICAgICAgJCgnI291dHB1dCcpLnNjcm9sbFRvcCgkKCcjb3V0cHV0JylbMF0uc2Nyb2xsSGVpZ2h0KTsKICAgICAgICB9CgogICAgICAgIGZ1bmN0aW9uIHJ1bkNvbW1hbmQoY29tbWFuZCkgewogICAgICAgICAgICBzY3JvbGxCb3R0b20oKTsKICAgICAgICAgICAgJC5nZXQoImNvbnNvbGUvcnVuP2NvbW1hbmQ9IiArIGVuY29kZVVSSShlbmNvZGVVUklDb21wb25lbnQoY29tbWFuZCkpLCBmdW5jdGlvbiAoZGF0YSwgc3RhdHVzKSB7CiAgICAgICAgICAgICAgICB1cGRhdGVDb25zb2xlKGZ1bmN0aW9uICgpIHsKICAgICAgICAgICAgICAgICAgICB1cGRhdGVDb21tYW5kKGNvbW1hbmRJbmRleCAtIDEpOwogICAgICAgICAgICAgICAgfSk7CiAgICAgICAgICAgIH0pOwogICAgICAgICAgICByZXNldElucHV0KCk7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiB1cGRhdGVDb25zb2xlKGNhbGxiYWNrKSB7CiAgICAgICAgICAgIGlmIChpc1VwZGF0ZVBhdXNlZCkgcmV0dXJuOwogICAgICAgICAgICAkLmdldCgiY29uc29sZS9vdXQiLCBmdW5jdGlvbiAoZGF0YSwgc3RhdHVzKSB7CiAgICAgICAgICAgICAgICBpZiAoZGF0YSA9PSB1bmRlZmluZWQgfHwgX2RhdGEubGVuZ3RoID09IFN0cmluZyhkYXRhKS5sZW5ndGgpIHsKICAgICAgICAgICAgICAgICAgICByZXR1cm47CiAgICAgICAgICAgICAgICB9CgogICAgICAgICAgICAgICAgX2RhdGEgPSBTdHJpbmcoZGF0YSk7CgogICAgICAgICAgICAgICAgbGV0IGxpbmVzID0gX2RhdGEuc3BsaXQoL1xufFxyL2cpOwogICAgICAgICAgICAgICAgbGV0IGh0bWwgPSAiIjsKICAgICAgICAgICAgICAgIGZvciAobGV0IGkgPSAwOyBpIDwgbGluZXMubGVuZ3RoOyBpKyspIHsKICAgICAgICAgICAgICAgICAgICBsZXQgbGluZSA9IGxpbmVzW2ldOwogICAgICAgICAgICAgICAgICAgIGxldCBpbmRleCA9IGxpbmUuaW5kZXhPZignaHR0cCcpOwogICAgICAgICAgICAgICAgICAgIGlmIChpbmRleCA+PSAwKSB7CiAgICAgICAgICAgICAgICAgICAgICAgIGxldCB1cmwgPSBsaW5lLnN1YnN0cmluZyhpbmRleCkudHJpbSgpOwogICAgICAgICAgICAgICAgICAgICAgICBjb25zb2xlLmxvZyhsaW5lKQogICAgICAgICAgICAgICAgICAgICAgICBjb25zb2xlLmxvZyhsaW5lLnN1YnN0cmluZygwLCBpbmRleCAtIDEpKQogICAgICAgICAgICAgICAgICAgICAgICBsaW5lID0gbGluZS5zdWJzdHJpbmcoMCwgaW5kZXgpICsgJzxhIGhyZWY9IicgKyB1cmwgKyAnIj4nICsgdXJsICsgIjwvYT4iOwogICAgICAgICAgICAgICAgICAgICAgICBjb25zb2xlLmxvZyhsaW5lKQogICAgICAgICAgICAgICAgICAgIH1lbHNlewogICAgICAgICAgICAgICAgICAgICAgICBpbmRleCA9IGxpbmUuaW5kZXhPZignOi8nKTsKICAgICAgICAgICAgICAgICAgICAgICAgaWYgKGluZGV4ID49IDApIHsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIGxldCB1cmwgPSBsaW5lLnN1YnN0cmluZyhpbmRleCAtIDEpLnRyaW0oKTsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIGxpbmUgPSBsaW5lLnN1YnN0cmluZygwLCBpbmRleCAtIDEpICsgJzxhIGhyZWY9ZmlsZTovLy8nICsgdXJsICsgJz4nICsgdXJsICsgIjwvYT4iOwogICAgICAgICAgICAgICAgICAgICAgICAgICAgY29uc29sZS5sb2cobGluZSkKICAgICAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgICAgIH0KCgoKICAgICAgICAgICAgICAgICAgICBodG1sICs9IGxpbmUgKyAnPC9icj4nOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgaHRtbCArPSAiPGJyPjxicj48YnI+IgogICAgICAgICAgICAgICAgLy8gQ2hlY2sgaWYgd2UgYXJlIHNjcm9sbGVkIHRvIHRoZSBib3R0b20gdG8gZm9yY2Ugc2Nyb2xsaW5nIG9uIHVwZGF0ZQogICAgICAgICAgICAgICAgbGV0IG91dHB1dCA9ICQoJyNvdXRwdXQnKTsKICAgICAgICAgICAgICAgIGxldCBzaG91bGRTY3JvbGwgPSBNYXRoLmFicygob3V0cHV0WzBdLnNjcm9sbEhlaWdodCAtIG91dHB1dC5zY3JvbGxUb3AoKSkgLSBvdXRwdXQuaW5uZXJIZWlnaHQoKSkgPCA1OwogICAgICAgICAgICAgICAgb3V0cHV0Lmh0bWwoaHRtbCk7CiAgICAgICAgICAgICAgICAvL2NvbnNvbGUubG9nKHNob3VsZFNjcm9sbCArICIgOj0gIiArIG91dHB1dFswXS5zY3JvbGxIZWlnaHQgKyAiIC0gIiArIG91dHB1dC5zY3JvbGxUb3AoKSArICIgKCIgKyBNYXRoLmFicygob3V0cHV0WzBdLnNjcm9sbEhlaWdodCAtIG91dHB1dC5zY3JvbGxUb3AoKSkgLSBvdXRwdXQuaW5uZXJIZWlnaHQoKSkgKyAiKSA9PSAiICsgb3V0cHV0LmlubmVySGVpZ2h0KCkpOwogICAgICAgICAgICAgICAgLy9jb25zb2xlLmxvZyhTdHJpbmcoZGF0YSkpOwogICAgICAgICAgICAgICAgaWYgKGNhbGxiYWNrKSBjYWxsYmFjaygpOwogICAgICAgICAgICAgICAgaWYgKHNob3VsZFNjcm9sbCkgc2Nyb2xsQm90dG9tKCk7CiAgICAgICAgICAgIH0pOwogICAgICAgIH0KCiAgICAgICAgZnVuY3Rpb24gcmVzZXRJbnB1dCgpIHsKICAgICAgICAgICAgY29tbWFuZEluZGV4ID0gLTE7CiAgICAgICAgICAgICQoIiNpbnB1dCIpLnZhbCgiIik7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiBwcmV2aW91c0NvbW1hbmQoKSB7CiAgICAgICAgICAgIHVwZGF0ZUNvbW1hbmQoY29tbWFuZEluZGV4ICsgMSk7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiBuZXh0Q29tbWFuZCgpIHsKICAgICAgICAgICAgdXBkYXRlQ29tbWFuZChjb21tYW5kSW5kZXggLSAxKTsKICAgICAgICB9CgogICAgICAgIGZ1bmN0aW9uIHVwZGF0ZUNvbW1hbmQoaW5kZXgpIHsKICAgICAgICAgICAgLy8gQ2hlY2sgaWYgd2UgYXJlIGF0IHRoZSBkZWZ1YWx0IGluZGV4IGFuZCBjbGVhciB0aGUgaW5wdXQKICAgICAgICAgICAgaWYgKGluZGV4IDwgMCkgewogICAgICAgICAgICAgICAgcmVzZXRJbnB1dCgpOwogICAgICAgICAgICAgICAgcmV0dXJuOwogICAgICAgICAgICB9CgogICAgICAgICAgICAkLmdldCgiY29uc29sZS9jb21tYW5kSGlzdG9yeT9pbmRleD0iICsgaW5kZXgsIGZ1bmN0aW9uIChkYXRhLCBzdGF0dXMpIHsKICAgICAgICAgICAgICAgIGlmIChkYXRhKSB7CiAgICAgICAgICAgICAgICAgICAgY29tbWFuZEluZGV4ID0gaW5kZXg7CiAgICAgICAgICAgICAgICAgICAgJCgiI2lucHV0IikudmFsKFN0cmluZyhkYXRhKSk7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0pOwogICAgICAgIH0KCiAgICAgICAgZnVuY3Rpb24gY29tcGxldGUoY29tbWFuZCkgewogICAgICAgICAgICAkLmdldCgiY29uc29sZS9jb21wbGV0ZT9jb21tYW5kPSIgKyBjb21tYW5kLCBmdW5jdGlvbiAoZGF0YSwgc3RhdHVzKSB7CiAgICAgICAgICAgICAgICBpZiAoZGF0YSkgewogICAgICAgICAgICAgICAgICAgICQoIiNpbnB1dCIpLnZhbChTdHJpbmcoZGF0YSkpOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9KTsKICAgICAgICB9CgogICAgICAgIC8vIFBvbGwgdG8gdXBkYXRlIHRoZSBjb25zb2xlIG91dHB1dAogICAgICAgIHdpbmRvdy5zZXRJbnRlcnZhbChmdW5jdGlvbiAoKSB7CiAgICAgICAgICAgIHVwZGF0ZUNvbnNvbGUobnVsbCkKICAgICAgICB9LCA1MDApOwogICAgPC9zY3JpcHQ+CjwvaGVhZD4KCjxib2R5IGNsYXNzPSJjb25zb2xlIj4KPGJ1dHRvbiBpZD0icGF1c2VVcGRhdGVzIj5QYXVzZSBVcGRhdGVzPC9idXR0b24+CjxkaXYgaWQ9Im91dHB1dCIgY2xhc3M9ImNvbnNvbGUiPjwvZGl2Pgo8dGV4dGFyZWEgaWQ9ImlucHV0IiBjbGFzcz0iY29uc29sZSIgYXV0b2ZvY3VzIHJvd3M9IjEiPjwvdGV4dGFyZWE+Cgo8c2NyaXB0PgogICAgLy8gc2V0dXAgb3VyIHBhdXNlIHVwZGF0ZXMgYnV0dG9uCiAgICAkKCIjcGF1c2VVcGRhdGVzIikuY2xpY2soZnVuY3Rpb24gKCkgewogICAgICAgIC8vY29uc29sZS5sb2coInBhdXNlIHVwZGF0ZXMgIiArIGlzVXBkYXRlUGF1c2VkKTsKICAgICAgICBpc1VwZGF0ZVBhdXNlZCA9ICFpc1VwZGF0ZVBhdXNlZDsKICAgICAgICAkKCIjcGF1c2VVcGRhdGVzIikuaHRtbChpc1VwZGF0ZVBhdXNlZCA/ICJSZXN1bWUgVXBkYXRlcyIgOiAiUGF1c2UgVXBkYXRlcyIpOwogICAgfSk7CgogICAgJCgiI2lucHV0Iikua2V5ZG93bihmdW5jdGlvbiAoZSkgewogICAgICAgIGlmIChlLmtleUNvZGUgPT0gMTMpIHsgLy8gRW50ZXIKICAgICAgICAgICAgLy8gd2UgZG9uJ3Qgd2FudCBhIGxpbmUgYnJlYWsgaW4gdGhlIGNvbnNvbGUKICAgICAgICAgICAgZS5wcmV2ZW50RGVmYXVsdCgpOwogICAgICAgICAgICBydW5Db21tYW5kKCQoIiNpbnB1dCIpLnZhbCgpKTsKICAgICAgICB9IGVsc2UgaWYgKGUua2V5Q29kZSA9PSAzOCkgeyAvLyBVcAogICAgICAgICAgICBwcmV2aW91c0NvbW1hbmQoKTsKICAgICAgICB9IGVsc2UgaWYgKGUua2V5Q29kZSA9PSA0MCkgeyAvLyBEb3duCiAgICAgICAgICAgIG5leHRDb21tYW5kKCk7CiAgICAgICAgfSBlbHNlIGlmIChlLmtleUNvZGUgPT0gMjcpIHsgLy8gRXNjYXBlCiAgICAgICAgICAgIHJlc2V0SW5wdXQoKTsKICAgICAgICB9IGVsc2UgaWYgKGUua2V5Q29kZSA9PSA5KSB7IC8vIFRhYgogICAgICAgICAgICBlLnByZXZlbnREZWZhdWx0KCk7CiAgICAgICAgICAgIGNvbXBsZXRlKCQoIiNpbnB1dCIpLnZhbCgpKTsKICAgICAgICB9CiAgICB9KTsKPC9zY3JpcHQ+CjwvYm9keT4KCjwvaHRtbD4=";

        public static string INDEX_CSS =
            "aHRtbCwgYm9keSB7CiAgICBoZWlnaHQ6MTAwJTsKfQoKdGV4dGFyZWEgewogICAgcmVzaXplOm5vbmU7Cn0KCmJvZHkuY29uc29sZSB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOmJsYWNrOwp9CgpkaXYuY29uc29sZSB7CiAgICBoZWlnaHQ6OTglOwogICAgd2lkdGg6MTAwJTsKICAgIGJhY2tncm91bmQtY29sb3I6YmxhY2s7CiAgICBjb2xvcjojRjBGMEYwOwogICAgZm9udC1zaXplOjE0cHg7CiAgICBmb250LWZhbWlseTptb25vc3BhY2U7CiAgICBvdmVyZmxvdy15OmF1dG87CiAgICBvdmVyZmxvdy14OmF1dG87CiAgICB3aGl0ZS1zcGFjZTpub3JtYWw7CiAgICB3b3JkLXdyYXA6YnJlYWstd29yZDsKfQoKdGV4dGFyZWEuY29uc29sZSB7CiAgICB3aWR0aDoxMDAlOwogICAgYmFja2dyb3VuZC1jb2xvcjpibGFjazsKICAgIGNvbG9yOiNGMEYwRjA7CiAgICBmb250LXNpemU6MTRweDsKICAgIGZvbnQtZmFtaWx5Om1vbm9zcGFjZTsKICAgIHBvc2l0aW9uOmZpeGVkOwogICAgYm90dG9tOiAzcHg7CiAgICBkaXNwbGF5OiBibG9jazsKfQoKYSB7CiAgICBjb2xvcjojZjRlNTQyOwp9CmE6aG92ZXJ7CiAgICBjb2xvcjojZjVlNjQzOwp9CgpzcGFuLldhcm5pbmcgewogICAgY29sb3I6I2Y0ZTU0MjsKfQoKc3Bhbi5Bc3NlcnQgewogICAgY29sb3I6I2Y0ZTU0MjsKfQoKc3Bhbi5FcnJvciB7CiAgICBjb2xvcjogI2ZmMDA1MjsKfQoKc3Bhbi5FeGNlcHRpb24gewogICAgY29sb3I6I2ZmMDAwMDsKfQoKc3Bhbi5IZWxwIHsKICAgIGNvbG9yOiAjMDlmZmE2Owp9CgpidXR0b24jcGF1c2VVcGRhdGVzIHsKICAgIHdpZHRoOjE1MHB4OwogICAgaGVpZ2h0OjQwcHg7CiAgICBwb3NpdGlvbjpmaXhlZDsKICAgIGZsb2F0OnJpZ2h0OwogICAgbWFyZ2luLXJpZ2h0OjUwcHg7CiAgICBtYXJnaW4tdG9wOjEwcHg7CiAgICByaWdodDowcHg7CiAgICBvcGFjaXR5Oi41Owp9";

        public static string INDEX_ICO =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAABuklEQVQ4jaWTPUhbURiGn5t7vJJrUFuTXNKIVgsVQyQoikjF0g5VQToKQkXcXLq4CRUHwa1bJzsUCi3+tIsOoohCB8GCSBVCoZXrX9WkNUbIj5GbpINybUgTUvrBWc4573Pe7+U70lZzXRpAODUkWVBIpZMGRjAAgAWgvPspslUtSKx6faj1Xip6nwEghFMj7t8msacXBDBcboqcGjH/NsKpISRZkKi9z2XZrYIANk8DhEOU+Jo4X1lCAIS2Nokf/aB58i3HC/M4Hjzk68sJLo6PsgDFdgdl6k27GakFlheJ7elIHY/QHj+h3NdonkW+f2Nn8hXG6S/CKxsUudzAdYgmzWajqm8A/c1rSj1eXF095rrd0vrXljIcnG18Jrqrc2/oOUgS+7PvUSursLe158wkw0H4yyZ3+wext7VzMPMO//gohx+nc4qzAAAWRUGS5byinC38WZ4X49QNjyDU/AOWe3ZTKUinMKIRjGiEi8DJvwH8E2MEV5fzvg4g0kkDi1Jsbhx8mOLnp1Wi+k5eoUVRSMZjCCMYwNHZc+X6MkFofc28ZL3jzhIqFQ5EdQ1WTwPhhTmk//3OvwGiHYnCU40aDAAAAABJRU5ErkJggg==";

        public static string UPLOAD_HTML =
            "PCFET0NUWVBFIGh0bWw+CjxodG1sIGxhbmc9ImVuIj4KPGhlYWQ+CiAgICA8bWV0YSBjaGFyc2V0PSJVVEYtOCI+CiAgICA8dGl0bGU+VXBsb2FkPC90aXRsZT4KICAgIDxzY3JpcHQ+CiAgICAgICAgdmFyIG9uVXBsb2FkID0gZnVuY3Rpb24gKCkgewogICAgICAgICAgICB2YXIgZmlsZXMgPSBkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgnZmlsZXMnKS5maWxlczsgLy9maWxlc+aYr+aWh+S7tumAieaLqeahhumAieaLqeeahOaWh+S7tuWvueixoeaVsOe7hAoKICAgICAgICAgICAgaWYgKGZpbGVzLmxlbmd0aCA9PSAwKSB7CiAgICAgICAgICAgICAgICBhbGVydCgibm8gZmlsZXMgbmVlZCB1cGxvYWQiKTsKICAgICAgICAgICAgICAgIHJldHVybjsKICAgICAgICAgICAgfQoKICAgICAgICAgICAgdmFyIGZvcm0gPSBuZXcgRm9ybURhdGEoKSwKICAgICAgICAgICAgICAgIHVybCA9ICJodHRwOi8vIiArIHdpbmRvdy5sb2NhdGlvbi5ob3N0ICsgIi91cGxvYWQiLCAvL+acjeWKoeWZqOS4iuS8oOWcsOWdgAogICAgICAgICAgICAgICAgZmlsZSA9IGZpbGVzWzBdOwogICAgICAgICAgICBmb3JtLmFwcGVuZCgnZmlsZScsIGZpbGUpOwogICAgICAgICAgICBmb3JtLmFwcGVuZCgnZmlsZW5hbWUnLCBmaWxlLm5hbWUpOwoKICAgICAgICAgICAgdmFyIHhociA9IG5ldyBYTUxIdHRwUmVxdWVzdCgpOwogICAgICAgICAgICB4aHIub3BlbigicG9zdCIsIHVybCwgdHJ1ZSk7Ci8v5LiK5Lyg6L+b5bqm5LqL5Lu2CiAgICAgICAgICAgIHhoci51cGxvYWQuYWRkRXZlbnRMaXN0ZW5lcigicHJvZ3Jlc3MiLCBmdW5jdGlvbiAocmVzdWx0KSB7CiAgICAgICAgICAgICAgICBpZiAocmVzdWx0Lmxlbmd0aENvbXB1dGFibGUpIHsKICAgICAgICAgICAgICAgICAgICAvL+S4iuS8oOi/m+W6pgogICAgICAgICAgICAgICAgICAgIHZhciBwZXJjZW50ID0gKHJlc3VsdC5sb2FkZWQgLyByZXN1bHQudG90YWwgKiAxMDApLnRvRml4ZWQoMik7CiAgICAgICAgICAgICAgICAgICAgZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3Byb2dyZXNzTnVtYmVyJykuaW5uZXJIVE1MID0gcGVyY2VudCArICclJzsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfSwgZmFsc2UpOwoKICAgICAgICAgICAgeGhyLmFkZEV2ZW50TGlzdGVuZXIoInJlYWR5c3RhdGVjaGFuZ2UiLCBmdW5jdGlvbiAoKSB7CiAgICAgICAgICAgICAgICB2YXIgcmVzdWx0ID0geGhyOwogICAgICAgICAgICAgICAgaWYgKHJlc3VsdC5zdGF0dXMgIT0gMjAwKSB7IC8vZXJyb3IKICAgICAgICAgICAgICAgICAgICBjb25zb2xlLmxvZygn5LiK5Lyg5aSx6LSlJywgcmVzdWx0LnN0YXR1cywgcmVzdWx0LnN0YXR1c1RleHQsIHJlc3VsdC5yZXNwb25zZSk7CgogICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgZWxzZSBpZiAocmVzdWx0LnJlYWR5U3RhdGUgPT0gNCkgeyAvL2ZpbmlzaGVkCiAgICAgICAgICAgICAgICAgICAgY29uc29sZS5sb2coJ+S4iuS8oOaIkOWKnycsIHJlc3VsdCk7CiAgICAgICAgICAgICAgICAgICAgZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3Byb2dyZXNzTnVtYmVyJykuaW5uZXJIVE1MID0gJzEwMCUnOwogICAgICAgICAgICAgICAgfQoKICAgICAgICAgICAgICAgIGRvY3VtZW50LmdldEVsZW1lbnRCeUlkKCdyZXN1bHQnKS5pbm5lckhUTUwgPSByZXN1bHQucmVzcG9uc2U7CiAgICAgICAgICAgIH0pOwogICAgICAgICAgICB4aHIuc2VuZChmb3JtKTsgLy/lvIDlp4vkuIrkvKAKCiAgICAgICAgfQoKICAgIDwvc2NyaXB0Pgo8L2hlYWQ+Cjxib2R5Pgo8aW5wdXQgdHlwZT0iZmlsZSIgbmFtZT0iZmlsZVBpY2tlciIgaWQ9ImZpbGVzIi8+CiZuYnNwOzxsYWJlbCBpZD0icHJvZ3Jlc3NOdW1iZXIiPjwvbGFiZWw+CjxidXR0b24gdHlwZT0iYnV0dG9uIiBvbmNsaWNrPSJvblVwbG9hZCgpIj5VcGxvYWQgRmlsZXMhPC9idXR0b24+CiZuYnNwOzxsYWJlbCBpZD0icmVzdWx0Ij48L2xhYmVsPgo8L2JvZHk+CjwvaHRtbD4=";


        public static string LUA_ENTER_KEY = "enterlua";

        public static string LUA_GLOBAL = @"
function printall()
    if CS ~= nil then
        local goes = CS.UnityEngine.Transform.FindObjectsOfType(typeof(CS.UnityEngine.Transform))

        for i = 0, goes.Length - 1 do
            print(i, goes[i])
        end
        return goes;
    end
end

function help()
    local methods = {
        printall = ""print and return all active loaded transforms in scene""
    }

    for i,v in pairs(methods) do
        print(tostring(i).."" : ""..v);
    end
end
        ";
    }


    struct QueuedCommand
    {
        public CommandAttribute command;
        public string[] args;
    }


    public class Console
    {
        internal static int CurrentState;

        internal const int STATE_TAIL = 2;
        internal const int STATE_LUA = 1;
        internal const int STATE_NONE = 0;

        // Max number of lines in the console output
        const int MAX_LINES = 300;

        // Maximum number of commands stored in the history
        const int MAX_HISTORY = 50;

        // Prefix for user inputted command
        const string COMMAND_OUTPUT_PREFIX = "> ";

        private static Console instance;
        private CommandTree m_commands;
        private List<string> m_output;
        private List<string> m_unitylog;
        private List<string> m_unityloge;
        private List<string> m_unityloga;
        private List<string> m_unitylogw;
        private List<string> m_history;

        private static string _dataString;

        private static string DateString
        {
            get
            {
                if (string.IsNullOrEmpty(_dataString))
                {
                    _dataString = DateTime.Now.ToString("u")
                        .Replace("-", "_")
                        .Replace(" ", "_")
                        .Replace(":", "_")
                        .Replace("Z", "");
                }

                return _dataString;
            }
        }


        private static string _logFilePath;

        private static string LogFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    _logFilePath = Path.Combine(Application.persistentDataPath, "Unity_" + DateString + ".log");
                }

                return _logFilePath;
            }
        }

        private static string _logErrorFilePath;

        private static string LogErrorFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logErrorFilePath))
                {
                    _logErrorFilePath = Path.Combine(Application.persistentDataPath,
                        "Unity_" + DateString + "_error" + ".log");
                }

                return _logErrorFilePath;
            }
        }

        private static string _logAllFilePath;

        private static string LogAllFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logAllFilePath))
                {
                    _logAllFilePath = Path.Combine(Application.persistentDataPath,
                        "Unity_" + DateString + "_all" + ".log");
                }

                return _logAllFilePath;
            }
        }

        private static string _logWarningFilePath;
        private const string EXCEPTION_KEY = "exception";
        public static string LUA_FILTER_KEY;
        

        private static string LogWarningFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logWarningFilePath))
                {
                    _logWarningFilePath = Path.Combine(Application.persistentDataPath,
                        "Unity_" + DateString + "_warning" + ".log");
                }

                return _logWarningFilePath;
            }
        }

        private Console()
        {
            m_commands = new CommandTree();
            m_output = new List<string>();
            m_history = new List<string>();
            m_unitylog = new List<string>();
            m_unityloge = new List<string>();
            m_unityloga = new List<string>();
            m_unitylogw = new List<string>();

            RegisterAttributes();
        }

        public static Console Instance
        {
            get
            {
                if (instance == null) instance = new Console();
                return instance;
            }
        }


        /* Execute a command */
        public static void Run(string str)
        {
            if (str.Length > 0)
            {
                LogCommand(str);
                Instance.RecordCommand(str);
                Instance.m_commands.Run(str);
            }
        }

        /* Clear all output from console */
        [Command("clear", "clears console output", false)]
        public static void Clear()
        {
            Instance.m_unityloga.Clear();
            Instance.m_unityloge.Clear();
            Instance.m_unitylogw.Clear();
            Instance.m_unitylog.Clear();
            Instance.m_output.Clear();
        }


        [Command("lp", "list playerprefs", true)]
        public static void ShowPlayerPrefs(string[] args)
        {
            if (args.Length >= 1)
            {
                if (PlayerPrefs.HasKey(args[0]))
                {
                    Log("    " + args[0] + " = " + PlayerPrefs.GetString(args[0]));
                }
                else
                    Log("    has not a key: " + args[0]);
            }
            else
            {
                Log("    need a key for inspecting");
            }
        }

        [Command("as", "add string to playerprefs", true)]
        public static void AddString(string[] args)
        {
            if (args.Length >= 2)
            {
                PlayerPrefs.SetString(args[0], args[1]);
                Log("    added " + args[0] + " = " + args[1]);
            }
            else
            {
                Log("    need a key & value for inspecting");
            }
        }


        [Command("cp", "clear all playerprefs", true)]
        public static void DeleteAll(string[] args)
        {
            PlayerPrefs.DeleteAll();
            Log("    Deleted All");
        }

        /* Print a list of all console commands */
        [Command("help", "prints commands", false)]
        public static void Help()
        {
            string help = "Commands:";
            foreach (CommandAttribute cmd in Instance.m_commands.OrderBy(m => m.m_command))
            {
                help += string.Format("\n{0} : {1}", cmd.m_command, cmd.m_help);
            }

            Log("<span class='Help'>" + help + "\n More details refer to  </span>" +
                "https://www.suntabu.com/post/unity-console-server/");
        }

        [Command("log", "print unity log with line n, with '-f' to enter log auto-print mode", false)]
        public static void Log(params string[] args)
        {
            if (args.Length <= 0)
            {
                LogCommand();
                return;
            }

            int count = 0;
            if (int.TryParse(args[0], out count))
            {
                LogCommand(LogType.Log, count);
            }
            else if (args[0].Trim() == "-f")
            {
                CurrentState = STATE_TAIL;
                Log(">>>>>>> LOG <<<<<<<");
            }
            else if (args[0].Trim() == "exit")
            {
                CurrentState = STATE_NONE;
                Log(">>>>>>> LOG EXIT <<<<<<<");
            }
            else
            {
                LogCommand();
            }
        }

        [Command("loge", "print unity error log with line n", false)]
        public static void LogE(params string[] args)
        {
            if (args.Length <= 0)
            {
                LogCommand(LogType.Error);
                return;
            }

            int count = 0;
            if (int.TryParse(args[0], out count))
            {
                LogCommand(LogType.Error, count);
            }
            else
            {
                LogCommand(LogType.Error);
            }
        }

        [Command("logw", "print unity warning log with line n", false)]
        public static void LogW(params string[] args)
        {
            if (args.Length <= 0)
            {
                LogCommand(LogType.Warning);
                return;
            }

            int count = 0;
            if (int.TryParse(args[0], out count))
            {
                LogCommand(LogType.Warning, count);
            }
            else
            {
                LogCommand(LogType.Warning);
            }
        }

        [Command("loga", "print unity all log with line n", false)]
        public static void LogA(params string[] args)
        {
            if (args.Length <= 0)
            {
                LogAll();
                return;
            }

            int count = 0;
            if (int.TryParse(args[0], out count))
            {
                LogAll(count);
            }
            else
            {
                LogAll();
            }
        }

        [Command("lua", "enter lua state")]
        public static void EnterLua(params string[] args)
        {
            CurrentState = STATE_LUA;

            if (!ConsoleServer.customActions.ContainsKey(Res.LUA_ENTER_KEY))
            {
                Log("Lua callback not registered");
                return;
            }

            Func<string, string> action = ConsoleServer.customActions[Res.LUA_ENTER_KEY];


            if (args.Length > 0)
            {
                if (args[0].ToLower() == "exit")
                {
                    CurrentState = STATE_NONE;
                    Log(">>>>> exit lua <<<<<");
                }
                else
                {
                    Log("DOString: " + args[0]);
                    try
                    {
                        action(args[0]);
                    }
                    catch (Exception e)
                    {
                        Log(e.Message + "\n" + e.StackTrace);
                    }
                }
            }
            else
            {
                Log(">>>>> lua <<<<<");
                EnterLua(Res.LUA_GLOBAL);
            }
        }

        [Command("sdirs",
            "print all files in streaming assets directory, parameters could be ignored folders, split with space")]
        public static void StreamingDirectory(string[] args)
        {
            PrintDirectoryFiles(Application.streamingAssetsPath, args);
        }

        [Command("tdirs",
            "print all files in temporary assets directory, parameters could be ignored folders, split with space")]
        public static void TempDirectory(string[] args)
        {
            PrintDirectoryFiles(Application.temporaryCachePath, args);
        }

        static void PrintDirectoryFiles(string dirRoot, params string[] args)
        {
            var files = Directory.GetFiles(dirRoot, "*", SearchOption.AllDirectories);

            var ignores = new List<string>();
            if (args.Length > 0)
            {
                ignores.AddRange(args.Where(t => !string.IsNullOrEmpty(t)));
            }

            foreach (var file in files)
            {
                if (!file.EndsWith(".meta"))
                {
                    bool isLog = true;
                    foreach (var t in ignores)
                    {
                        if (!file.ToLower().Contains(t)) continue;
                        isLog = false;
                        break;
                    }

                    if (isLog)
                    {
                        var fileInfo = new FileInfo(file);
                        var log = (fileInfo.Length / 1024f).ToString("F2").PadLeft(10, '-') + "KB - " + file;
                        Log(log);
                    }
                }
            }
        }

        [Command("pdirs",
            "print all files in persistent directory, parameters could be ignored folders, split with space")]
        public static void PersistentDirectory(string[] args)
        {
            PrintDirectoryFiles(Application.persistentDataPath, args);
        }

        [Command("gettfile", "get file from temporary directory")]
        public static void GetFileFromTemporary(string[] args)
        {
            if (args.Length <= 0)
            {
                Log("A file path should be given!");
                return;
            }

            var myFile = args[0];
            var files = Directory.GetFiles(Application.temporaryCachePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.ToLower().Contains(myFile.ToLower()))
                {
                    var filename = file.Substring(Application.temporaryCachePath.Length + 1).Replace('\\', '/');
                    Log(string.Format("-> http://{0}:{1}/download/{2}", ConsoleServer.Instance.IP,
                        ConsoleServer.Instance.Port,
                        filename));
                    break;
                }
            }
        }

        [Command("getpfile", "get file from persistent directory")]
        public static void GetFileFromPersistent(string[] args)
        {
            if (args.Length <= 0)
            {
                Log("A file path should be given!");
                return;
            }

            var myFile = args[0];
            var files = Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories);
            ;
            foreach (var file in files)
            {
                if (file.ToLower().Contains(myFile.ToLower()))
                {
                    var filename = file.Substring(Application.persistentDataPath.Length + 1).Replace('\\', '/');
                    Log(string.Format("-> http://{0}:{1}/download/{2}", ConsoleServer.Instance.IP,
                        ConsoleServer.Instance.Port,
                        filename));
                    break;
                }
            }
        }

        [Command("getsfile", "get file from Streaming directory, the file would be copied into persistent path first",
            true)]
        public static void GetFileFromStreaming(string[] args)
        {
            if (args.Length <= 0)
            {
                Log("A file path should be given!");
                return;
            }

            var myFile = args[0];
            var files = Directory.GetFiles(Application.streamingAssetsPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (file.ToLower().Contains(myFile.ToLower()) && !file.EndsWith(".meta"))
                {
                    var filename = file.Substring(Application.streamingAssetsPath.Length + 1).Replace('\\', '/');
                    var www = new WWW(file);
                    while (!www.isDone)
                    {
                    }

                    File.WriteAllBytes(Application.persistentDataPath + "/" + myFile, www.bytes);

                    Log(string.Format("-> http://{0}:{1}/download/{2}", ConsoleServer.Instance.IP,
                        ConsoleServer.Instance.Port,
                        filename));
                    break;
                }
            }
        }


        /* Find command based on partial string */
        public static string Complete(string partialCommand)
        {
            return Instance.m_commands.Complete(partialCommand);
        }

        /* Logs user input to output */
        public static void LogCommand(string cmd)
        {
            Log(COMMAND_OUTPUT_PREFIX + cmd);
        }

        /* Logs string to output */
        public static void Log(string str)
        {
            Instance.m_output.Add(string.Format("{0} : {1}", DateTime.Now.ToString(), str));
            if (Instance.m_output.Count > MAX_LINES)
                Instance.m_output.RemoveAt(0);
        }

        private static void LogToFile(string log, LogType type)
        {
            var newLog = string.Format("\n{0} : {1}\n", DateTime.Now.ToString(), log);

            Instance.m_unityloga.Add(newLog);
            if (Instance.m_unityloga.Count > MAX_LINES)
                Instance.m_unityloga.RemoveAt(0);
            File.AppendAllText(LogAllFilePath, newLog, Encoding.UTF8);

            switch (type)
            {
                case LogType.Log:
                    Instance.m_unitylog.Add(newLog);
                    if (Instance.m_unitylog.Count > MAX_LINES)
                        Instance.m_unitylog.RemoveAt(0);
                    File.AppendAllText(LogFilePath, newLog, Encoding.UTF8);
                    break;
                case LogType.Warning:
                    Instance.m_unitylogw.Add(newLog);
                    if (Instance.m_unitylogw.Count > MAX_LINES)
                        Instance.m_unitylogw.RemoveAt(0);
                    File.AppendAllText(LogWarningFilePath, newLog, Encoding.UTF8);
                    break;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    Instance.m_unityloge.Add(newLog);
                    if (Instance.m_unityloge.Count > MAX_LINES)
                        Instance.m_unityloge.RemoveAt(0);
                    File.AppendAllText(LogErrorFilePath, newLog, Encoding.UTF8);
                    break;
            }
        }

        public static void LogAll(int count = 20)
        {
            int printCount = count;
            if (count > MAX_LINES)
            {
                Log(string.Format("Only support {0} lines log, wanna more please download log", MAX_LINES));
            }

            printCount = Mathf.Min(MAX_LINES, Instance.m_unityloga.Count);
            for (int i = 0; i < printCount; i++)
            {
                Log(Instance.m_unityloga[i]);
            }
        }

        public static void LogCommand(LogType type = LogType.Log, int count = 20)
        {
            int printCount = count;
            if (count > MAX_LINES)
            {
                Log(string.Format("Only support {0} lines log, wanna more please download log", MAX_LINES));
            }

            switch (type)
            {
                case LogType.Warning:
                    printCount = Mathf.Min(MAX_LINES, Instance.m_unitylogw.Count);
                    for (int i = 0; i < printCount; i++)
                    {
                        Log(Instance.m_unitylogw[i]);
                    }

                    break;
                case LogType.Log:
                    printCount = Mathf.Min(MAX_LINES, Instance.m_unitylog.Count);
                    for (int i = 0; i < printCount; i++)
                    {
                        Log(Instance.m_unitylog[i]);
                    }

                    break;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    printCount = Mathf.Min(MAX_LINES, Instance.m_unityloge.Count);
                    for (int i = 0; i < printCount; i++)
                    {
                        Log(Instance.m_unityloge[i]);
                    }

                    break;
            }
        }

        /* Callback for Unity logging */
        public static void LogCallback(string logString, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                stackTrace = new System.Diagnostics.StackTrace().ToString();
            }

            if (string.IsNullOrEmpty(stackTrace))
            {
                Log("No stacktrace string found");
            }

            var formatStr = "<span class='" + type + "'>" + logString + "\n" + stackTrace + "</span>";

            if (type != LogType.Log)
            {
                Console.LogToFile(formatStr, type);
            }
            else
            {
                Console.LogToFile(logString, type);
            }

            var content = (logString + stackTrace).ToLower();
            //TODO: add lua execute result logs to show on real time.
            if ((!string.IsNullOrEmpty(LUA_FILTER_KEY) && content.Contains(LUA_FILTER_KEY)) ||
                CurrentState == STATE_TAIL || content.Contains(EXCEPTION_KEY))
            {
                Log(formatStr);
            }
        }

        /* Returns the output */
        public static string Output()
        {
            return string.Join("\n", Instance.m_output.ToArray());
        }

        /* Register a new console command */
        public static void RegisterCommand(string command, string desc, CommandAttribute.Callback callback,
            bool runOnMainThread = true)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new Exception("Command String cannot be empty");
            }

            CommandAttribute cmd = new CommandAttribute(command, desc, runOnMainThread)
            {
                m_callback = callback
            };

            Instance.m_commands.Add(cmd);
        }

        private void RegisterAttributes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // HACK: IL2CPP crashes if you attempt to get the methods of some classes in these assemblies.
                if (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("mscorlib"))
                {
                    continue;
                }

                foreach (Type type in assembly.GetTypes())
                {
                    // FIXME add support for non-static methods (FindObjectByType?)
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        CommandAttribute[] attrs =
                            method.GetCustomAttributes(typeof(CommandAttribute), true) as CommandAttribute[];
                        if (attrs.Length == 0)
                            continue;

                        CommandAttribute.Callback cb =
                            Delegate.CreateDelegate(typeof(CommandAttribute.Callback), method, false) as
                                CommandAttribute.Callback;
                        if (cb == null)
                        {
                            CommandAttribute.CallbackSimple cbs =
                                Delegate.CreateDelegate(typeof(CommandAttribute.CallbackSimple), method, false) as
                                    CommandAttribute.CallbackSimple;
                            if (cbs != null)
                            {
                                cb = delegate(string[] args) { cbs(); };
                            }
                        }

                        if (cb == null)
                        {
                            Debug.LogError(string.Format(
                                "Method {0}.{1} takes the wrong arguments for a console command.", type, method.Name));
                            continue;
                        }

                        // try with a bare action
                        foreach (CommandAttribute cmd in attrs)
                        {
                            if (string.IsNullOrEmpty(cmd.m_command))
                            {
                                Debug.LogError(string.Format("Method {0}.{1} needs a valid command name.", type,
                                    method.Name));
                                continue;
                            }

                            cmd.m_callback = cb;
                            m_commands.Add(cmd);
                        }
                    }
                }
            }
        }

        /* Get a previously ran command from the history */
        public static string PreviousCommand(int index)
        {
            return index >= 0 && index < Instance.m_history.Count ? Instance.m_history[index] : null;
        }

        /* Update history with a new command */
        private void RecordCommand(string command)
        {
            m_history.Insert(0, command);
            if (m_history.Count > MAX_HISTORY)
                m_history.RemoveAt(m_history.Count - 1);
        }

        // Our routes
        [Route("^/console/out$")]
        public static void Output(RequestContext context)
        {
            context.Response.WriteString(Console.Output());
        }

        [Route("^/upload$", "POST", true)]
        public static void Upload(RequestContext context)
        {
            try
            {
                string fileName = string.Empty;
                string filePath = string.Empty;
                //获取Post请求中的参数和值帮助类
                HttpListenerPostParaHelper httppost = new HttpListenerPostParaHelper(context.context);
                //获取Post过来的参数和数据
                List<HttpListenerPostValue> lst = httppost.GetHttpListenerPostValue();
                foreach (var key in lst)
                {
                    if (key.type == 0)
                    {
                        string value = Encoding.UTF8.GetString(key.datas).Replace("\r\n", "");
                        if (key.name == "filename")
                        {
                            fileName = value;
                            filePath = Application.persistentDataPath + "/" + fileName;
                        }
                    }
                }


                foreach (var key in lst)
                {
                    if (key.type == 1)
                    {
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (key.name == "file")
                            {
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                }

                                FileStream fs = new FileStream(filePath, FileMode.Create);
                                fs.Write(key.datas, 0, key.datas.Length);
                                fs.Close();
                                fs.Dispose();
                            }
                        }
                    }
                }

                if (fileName.ToLower().EndsWith(".zip"))
                {
                    var actionKey = "unzip";

                    if (!ConsoleServer.customActions.ContainsKey(actionKey))
                    {
                        context.Response.WriteString("unzip callback not registered");
                        return;
                    }

                    Func<string, string> action = ConsoleServer.customActions[actionKey];

                    string result = action(filePath + "," + Application.persistentDataPath);

                    Debug.LogError("--->" + result);
                    if (result.ToLower() == "false")
                    {
                        context.Response.WriteError(HttpStatusCode.BadRequest, "BadRequest", "unzip failed");
                        return;
                    }
                }


                context.Response.WriteString("Received successfully: " + filePath);
            }
            catch (Exception e)
            {
                context.Response.WriteError(HttpStatusCode.BadRequest, "BadRequest", e.Message + "/n" + e.StackTrace);
            }
        }

        [Route("^/console/run$")]
        public static void Run(RequestContext context)
        {
            string command = Uri.UnescapeDataString(context.Request.QueryString.Get("command"));
            if (!string.IsNullOrEmpty(command))
                Console.Run(command);

            context.Response.StatusCode = (int) HttpStatusCode.OK;
            context.Response.StatusDescription = "OK";
        }

        [Route("^/console/commandHistory$")]
        public static void History(RequestContext context)
        {
            string index = context.Request.QueryString.Get("index");

            string previous = null;
            if (!string.IsNullOrEmpty(index))
                previous = Console.PreviousCommand(System.Int32.Parse(index));

            context.Response.WriteString(previous);
        }

        [Route("^/console/complete$")]
        public static void Complete(RequestContext context)
        {
            string partialCommand = context.Request.QueryString.Get("command");

            string found = null;
            if (partialCommand != null)
                found = Console.Complete(partialCommand);

            context.Response.WriteString(found);
        }


        private static void WriteStreamToFile(BinaryReader br, string fileName, long length)
        {
            byte[] fileContents = new byte[] { };
            var bytes = new byte[length];
            int i = 0;
            while ((i = br.Read(bytes, 0, (int) length)) != 0)
            {
                byte[] arr = new byte[fileContents.LongLength + i];
                fileContents.CopyTo(arr, 0);
                Array.Copy(bytes, 0, arr, fileContents.Length, i);
                fileContents = arr;
            }

            using (var fs = new FileStream(fileName, FileMode.Create))
            {
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(fileContents);
                }
            }
        }
    }

    class CommandTree : IEnumerable<CommandAttribute>
    {
        private Dictionary<string, CommandTree> m_subcommands;
        private CommandAttribute m_command;

        public CommandTree()
        {
            m_subcommands = new Dictionary<string, CommandTree>();
        }

        public void Add(CommandAttribute cmd)
        {
            _add(cmd.m_command.ToLower().Split(' '), 0, cmd);
        }

        private void _add(string[] commands, int command_index, CommandAttribute cmd)
        {
            if (commands.Length == command_index)
            {
                m_command = cmd;
                return;
            }

            string token = commands[command_index];
            if (!m_subcommands.ContainsKey(token))
            {
                m_subcommands[token] = new CommandTree();
            }

            m_subcommands[token]._add(commands, command_index + 1, cmd);
        }

        public IEnumerator<CommandAttribute> GetEnumerator()
        {
            if (m_command != null && m_command.m_command != null)
                yield return m_command;

            foreach (KeyValuePair<string, CommandTree> entry in m_subcommands)
            {
                foreach (CommandAttribute cmd in entry.Value)
                {
                    if (cmd != null && cmd.m_command != null)
                        yield return cmd;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public string Complete(string partialCommand)
        {
            return _complete(partialCommand.Split(' '), 0, "");
        }

        public string _complete(string[] partialCommand, int index, string result)
        {
            if (partialCommand.Length == index && m_command != null)
            {
                // this is a valid command... so we do nothing
                return result;
            }
            else if (partialCommand.Length == index)
            {
                // This is valid but incomplete.. print all of the subcommands
                Console.LogCommand(result);
                foreach (string key in m_subcommands.Keys.OrderBy(m => m))
                {
                    Console.Log(result + " " + key);
                }

                return result + " ";
            }
            else if (partialCommand.Length == (index + 1))
            {
                string partial = partialCommand[index];
                if (m_subcommands.ContainsKey(partial))
                {
                    result += partial;
                    return m_subcommands[partial]._complete(partialCommand, index + 1, result);
                }

                // Find any subcommands that match our partial command
                List<string> matches = new List<string>();
                foreach (string key in m_subcommands.Keys.OrderBy(m => m))
                {
                    if (key.StartsWith(partial))
                    {
                        matches.Add(key);
                    }
                }

                if (matches.Count == 1)
                {
                    // Only one command found, log nothing and return the complete command for the user input
                    return result + matches[0] + " ";
                }
                else if (matches.Count > 1)
                {
                    // list all the options for the user and return partial
                    Console.LogCommand(result + partial);
                    foreach (string match in matches)
                    {
                        Console.Log(result + match);
                    }
                }

                return result + partial;
            }

            string token = partialCommand[index];
            if (!m_subcommands.ContainsKey(token))
            {
                return result;
            }

            result += token + " ";
            return m_subcommands[token]._complete(partialCommand, index + 1, result);
        }

        public void Run(string commandStr)
        {
            if (Console.CurrentState == Console.STATE_LUA)
            {
                _run(new string[] {"lua", commandStr.Replace("lua ", "")}, 0);
            }
            else if (Console.CurrentState == Console.STATE_TAIL)
            {
                _run(new string[] {"log", commandStr.Replace("log ", "")}, 0);
            }
            else
            {
                // Split user input on spaces ignoring anything in qoutes
                Regex regex = new Regex(@""".*?""|[^\s]+");
                MatchCollection matches = regex.Matches(commandStr);
                string[] tokens = new string[matches.Count];
                for (int i = 0; i < tokens.Length; ++i)
                {
                    tokens[i] = matches[i].Value.Replace("\"", "");
                    Debug.Log(tokens[i]);
                }

                _run(tokens, 0);
            }
        }

        static string[] emptyArgs = new string[0] { };

        private void _run(string[] commands, int index)
        {
            if (commands.Length == index)
            {
                RunCommand(emptyArgs);
                return;
            }

            string token = commands[index].ToLower();
            if (!m_subcommands.ContainsKey(token))
            {
                RunCommand(commands.Skip(index).ToArray());
                return;
            }

            m_subcommands[token]._run(commands, index + 1);
        }

        private void RunCommand(string[] args)
        {
            if (m_command == null)
            {
                Console.Log("command not found");
            }
            else
            {
                if (m_command.m_runOnMainThread)
                {
                    ConsoleServer.Invoke(() => { m_command.m_callback(args); });
                }
                else
                    m_command.m_callback(args);
            }
        }
    }


    /// <summary>
    /// HttpListenner监听Post请求参数值实体
    /// </summary>
    public class HttpListenerPostValue
    {
        /// <summary>
        /// 0=> 参数
        /// 1=> 文件
        /// </summary>
        public int type = 0;

        public string name;
        public byte[] datas;
    }

    /// <summary>
    /// 获取Post请求中的参数和值帮助类
    /// </summary>
    public class HttpListenerPostParaHelper
    {
        private HttpListenerContext request;

        public HttpListenerPostParaHelper(HttpListenerContext request)
        {
            this.request = request;
        }

        private bool CompareBytes(byte[] source, byte[] comparison)
        {
            try
            {
                int count = source.Length;
                if (source.Length != comparison.Length)
                    return false;
                for (int i = 0; i < count; i++)
                    if (source[i] != comparison[i])
                        return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private byte[] ReadLineAsBytes(Stream SourceStream)
        {
            var resultStream = new MemoryStream();
            while (true)
            {
                int data = SourceStream.ReadByte();
                resultStream.WriteByte((byte) data);
                if (data == 10)
                    break;
            }

            resultStream.Position = 0;
            byte[] dataBytes = new byte[resultStream.Length];
            resultStream.Read(dataBytes, 0, dataBytes.Length);
            return dataBytes;
        }

        /// <summary>
        /// 获取Post过来的参数和数据
        /// </summary>
        /// <returns></returns>
        public List<HttpListenerPostValue> GetHttpListenerPostValue()
        {
            try
            {
                List<HttpListenerPostValue> HttpListenerPostValueList = new List<HttpListenerPostValue>();
                if (request.Request.ContentType.Length > 20 &&
                    string.Compare(request.Request.ContentType.Substring(0, 20), "multipart/form-data;", true) == 0)
                {
                    string[] HttpListenerPostValue = request.Request.ContentType.Split(';').Skip(1).ToArray();
                    string boundary = string.Join(";", HttpListenerPostValue).Replace("boundary=", "").Trim();
                    byte[] ChunkBoundary = Encoding.UTF8.GetBytes("--" + boundary + "\r\n");
                    byte[] EndBoundary = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");
                    Stream SourceStream = request.Request.InputStream;
                    var resultStream = new MemoryStream();
                    bool CanMoveNext = true;
                    HttpListenerPostValue data = null;
                    while (CanMoveNext)
                    {
                        byte[] currentChunk = ReadLineAsBytes(SourceStream);
                        if (!Encoding.UTF8.GetString(currentChunk).Equals("\r\n"))
                            resultStream.Write(currentChunk, 0, currentChunk.Length);
                        if (CompareBytes(ChunkBoundary, currentChunk))
                        {
                            byte[] result = new byte[resultStream.Length - ChunkBoundary.Length];
                            resultStream.Position = 0;
                            resultStream.Read(result, 0, result.Length);
                            CanMoveNext = true;
                            if (result.Length > 0)
                                data.datas = result;
                            data = new HttpListenerPostValue();
                            HttpListenerPostValueList.Add(data);
                            resultStream.Dispose();
                            resultStream = new MemoryStream();
                        }
                        else if (Encoding.UTF8.GetString(currentChunk).Contains("Content-Disposition"))
                        {
                            byte[] result = new byte[resultStream.Length - 2];
                            resultStream.Position = 0;
                            resultStream.Read(result, 0, result.Length);
                            CanMoveNext = true;
                            data.name = Encoding.UTF8.GetString(result)
                                .Replace("Content-Disposition: form-data; name=\"", "").Replace("\"", "").Split(';')[0];
                            resultStream.Dispose();
                            resultStream = new MemoryStream();
                        }
                        else if (Encoding.UTF8.GetString(currentChunk).Contains("Content-Type"))
                        {
                            CanMoveNext = true;
                            data.type = 1;
                            resultStream.Dispose();
                            resultStream = new MemoryStream();
                        }
                        else if (CompareBytes(EndBoundary, currentChunk))
                        {
                            byte[] result = new byte[resultStream.Length - EndBoundary.Length - 2];
                            resultStream.Position = 0;
                            resultStream.Read(result, 0, result.Length);
                            data.datas = result;
                            resultStream.Dispose();
                            CanMoveNext = false;
                        }
                    }
                }

                return HttpListenerPostValueList;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
