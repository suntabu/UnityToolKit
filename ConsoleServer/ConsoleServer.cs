using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace UnityToolKit.ConsoleServer
{
    
    
// TODO:open this to enable this script
//#define CONSOLE_SERVER
 
    
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

        private int mPort = 55055;

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
                string hostName = Dns.GetHostName(); //本机名   
                //System.Net.IPAddress[] addressList = Dns.GetHostByName(hostName).AddressList;//会警告GetHostByName()已过期，我运行时且只返回了一个IPv4的地址   
                System.Net.IPAddress[] addressList = Dns.GetHostAddresses(hostName);
                if (addressList.Length > 0)
                {
                    for (int i = 0; i < addressList.Length; i++)
                    {
                        var ip = addressList[i];
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ipstr = ip.ToString();
                            if (ipstr.StartsWith("10.") || ipstr.StartsWith("192.") || ipstr.StartsWith("172."))
                            {
                                return ipstr;
                            }
                        }
                    }

                    return "localhost";
                }
                else
                {
                    return "localhost";
                }
            }
        }

        #endregion singleton

        private Thread mRunningThread;

        private static string fileRoot;
        private static HttpListener listener;
        private static List<RouteAttribute> registeredRoutes;
        private static Queue<RequestContext> mainRequests = new Queue<RequestContext>();

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
            RouteAttribute downloadRoute = new RouteAttribute(string.Format(@"^/download/(.*\.{0})$", pattern));
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
            path = Path.Combine(fileRoot, context.match.Groups[1].Value);

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
                while (mainRequests.Count == 0)
                {
                    Thread.Sleep(100);
                }

                RequestContext context = null;
                lock (mainRequests)
                {
                    context = mainRequests.Dequeue();
                }

                HandleRequest(context);

                Thread.Sleep(16);
            }
        }

        void ListenerCallback(IAsyncResult result)
        {
            RequestContext context = new RequestContext(listener.EndGetContext(result));

            HandleRequest(context);

            if (listener.IsListening)
            {
                listener.BeginGetContext(ListenerCallback, null);
            }
        }


        public void StartServer(int port = 55055, bool isRegisterLogCallback = true)
        {
#if !CONSOLE_SERVER
                return;
#endif


            if (!UnityEngine.Debug.isDebugBuild)
            {
                throw new InvalidOperationException("Console Server 只能在Debug Build中使用！");
            }

            if (listener != null && listener.IsListening && mRunningThread != null && mRunningThread.IsAlive)
            {
                return;
            }
            else
            {
                if (listener != null)
                {
                    Stop();
                }
            }

            mPort = port;
            mRegisterLogCallback = isRegisterLogCallback;
            // Start server
            Debug.Log("Starting Console Server on port : " + mPort);
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + mPort + "/");
            listener.Start();
            listener.BeginGetContext(ListenerCallback, null);

            if (mRegisterLogCallback)
            {
                // Capture Console Logs
#if UNITY_5_3_OR_NEWER
                Application.logMessageReceived += Console.LogCallback;
#else
        Application.RegisterLogCallback(Console.LogCallback);
#endif
            }

            if (mRunningThread == null)
            {
                mRunningThread = new Thread(HandleRequests);
            }

            if (!mRunningThread.IsAlive)
                mRunningThread.Start();
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
            "PCFET0NUWVBFIGh0bWw+CjxodG1sPgo8aGVhZD4KICAgIDxsaW5rIHJlbD0ic3R5bGVzaGVldCIgdHlwZT0idGV4dC9jc3MiIGhyZWY9ImNvbnNvbGUuY3NzIj4KICAgIDxsaW5rIHJlbD0ic2hvcnRjdXQgaWNvbiIgaHJlZj0iZmF2aWNvbi5pY28iIHR5cGU9ImltYWdlL3gtaW1hZ2UiPgogICAgPGxpbmsgcmVsPSJpY29uIiBocmVmPSJmYXZpY29uLmljb24iIHR5cGU9ImltYWdlL3gtaW1hZ2UiPgogICAgPHRpdGxlPkNvbnNvbGUgU2VydmVyPC90aXRsZT4KCiAgICA8c2NyaXB0IHNyYz0iaHR0cDovL2FqYXguZ29vZ2xlYXBpcy5jb20vYWpheC9saWJzL2pxdWVyeS8xLjEwLjIvanF1ZXJ5Lm1pbi5qcyI+CiAgICA8L3NjcmlwdD4KCiAgICA8c2NyaXB0PgogICAgICAgIHZhciBjb21tYW5kSW5kZXggPSAtMTsKICAgICAgICB2YXIgaGFzaCA9IG51bGw7CiAgICAgICAgdmFyIGlzVXBkYXRlUGF1c2VkID0gZmFsc2U7CiAgICAgICAgdmFyIF9kYXRhID0gIiI7CgogICAgICAgIGZ1bmN0aW9uIHNjcm9sbEJvdHRvbSgpIHsKICAgICAgICAgICAgJCgnI291dHB1dCcpLnNjcm9sbFRvcCgkKCcjb3V0cHV0JylbMF0uc2Nyb2xsSGVpZ2h0KTsKICAgICAgICB9CgogICAgICAgIGZ1bmN0aW9uIHJ1bkNvbW1hbmQoY29tbWFuZCkgewogICAgICAgICAgICBzY3JvbGxCb3R0b20oKTsKICAgICAgICAgICAgJC5nZXQoImNvbnNvbGUvcnVuP2NvbW1hbmQ9IiArIGVuY29kZVVSSShlbmNvZGVVUklDb21wb25lbnQoY29tbWFuZCkpLCBmdW5jdGlvbiAoZGF0YSwgc3RhdHVzKSB7CiAgICAgICAgICAgICAgICB1cGRhdGVDb25zb2xlKGZ1bmN0aW9uICgpIHsKICAgICAgICAgICAgICAgICAgICB1cGRhdGVDb21tYW5kKGNvbW1hbmRJbmRleCAtIDEpOwogICAgICAgICAgICAgICAgfSk7CiAgICAgICAgICAgIH0pOwogICAgICAgICAgICByZXNldElucHV0KCk7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiB1cGRhdGVDb25zb2xlKGNhbGxiYWNrKSB7CiAgICAgICAgICAgIGlmIChpc1VwZGF0ZVBhdXNlZCkgcmV0dXJuOwogICAgICAgICAgICAkLmdldCgiY29uc29sZS9vdXQiLCBmdW5jdGlvbiAoZGF0YSwgc3RhdHVzKSB7CiAgICAgICAgICAgICAgICBpZiAoZGF0YSA9PSB1bmRlZmluZWQgfHwgX2RhdGEubGVuZ3RoID09IFN0cmluZyhkYXRhKS5sZW5ndGgpIHsKICAgICAgICAgICAgICAgICAgICByZXR1cm47CiAgICAgICAgICAgICAgICB9CgogICAgICAgICAgICAgICAgX2RhdGEgPSBTdHJpbmcoZGF0YSk7CgogICAgICAgICAgICAgICAgdmFyIGxpbmVzID0gX2RhdGEuc3BsaXQoL1xufFxyL2cpOwogICAgICAgICAgICAgICAgdmFyIGh0bWwgPSAiIjsKICAgICAgICAgICAgICAgIGZvciAodmFyIGkgPSAwOyBpIDwgbGluZXMubGVuZ3RoOyBpKyspIHsKICAgICAgICAgICAgICAgICAgICB2YXIgbGluZSA9IGxpbmVzW2ldOwogICAgICAgICAgICAgICAgICAgIHZhciBpbmRleCA9IGxpbmUuaW5kZXhPZignaHR0cCcpOwogICAgICAgICAgICAgICAgICAgIGlmIChpbmRleCA+PSAwKSB7CiAgICAgICAgICAgICAgICAgICAgICAgIHZhciB1cmwgPSBsaW5lLnN1YnN0cmluZyhpbmRleCk7CiAgICAgICAgICAgICAgICAgICAgICAgIGxpbmUgPSBsaW5lLnN1YnN0cmluZygwLCBpbmRleC0xKSArICc8YSBocmVmPScgKyB1cmwgKyAnPicgKyB1cmwgKyAiPC9hPiI7CiAgICAgICAgICAgICAgICAgICAgICAgIGNvbnNvbGUubG9nKGxpbmUpCiAgICAgICAgICAgICAgICAgICAgfQoKICAgICAgICAgICAgICAgICAgICBodG1sICs9IGxpbmUgKyAnPC9icj4nOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgaHRtbCArPSAiPGJyPjxicj48YnI+IgogICAgICAgICAgICAgICAgLy8gQ2hlY2sgaWYgd2UgYXJlIHNjcm9sbGVkIHRvIHRoZSBib3R0b20gdG8gZm9yY2Ugc2Nyb2xsaW5nIG9uIHVwZGF0ZQogICAgICAgICAgICAgICAgdmFyIG91dHB1dCA9ICQoJyNvdXRwdXQnKTsKICAgICAgICAgICAgICAgIHNob3VsZFNjcm9sbCA9IE1hdGguYWJzKChvdXRwdXRbMF0uc2Nyb2xsSGVpZ2h0IC0gb3V0cHV0LnNjcm9sbFRvcCgpKSAtIG91dHB1dC5pbm5lckhlaWdodCgpKSA8IDU7CiAgICAgICAgICAgICAgICBvdXRwdXQuaHRtbChodG1sKTsKICAgICAgICAgICAgICAgIC8vY29uc29sZS5sb2coc2hvdWxkU2Nyb2xsICsgIiA6PSAiICsgb3V0cHV0WzBdLnNjcm9sbEhlaWdodCArICIgLSAiICsgb3V0cHV0LnNjcm9sbFRvcCgpICsgIiAoIiArIE1hdGguYWJzKChvdXRwdXRbMF0uc2Nyb2xsSGVpZ2h0IC0gb3V0cHV0LnNjcm9sbFRvcCgpKSAtIG91dHB1dC5pbm5lckhlaWdodCgpKSArICIpID09ICIgKyBvdXRwdXQuaW5uZXJIZWlnaHQoKSk7CiAgICAgICAgICAgICAgICAvL2NvbnNvbGUubG9nKFN0cmluZyhkYXRhKSk7CiAgICAgICAgICAgICAgICBpZiAoY2FsbGJhY2spIGNhbGxiYWNrKCk7CiAgICAgICAgICAgICAgICBpZiAoc2hvdWxkU2Nyb2xsKSBzY3JvbGxCb3R0b20oKTsKICAgICAgICAgICAgfSk7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiByZXNldElucHV0KCkgewogICAgICAgICAgICBjb21tYW5kSW5kZXggPSAtMTsKICAgICAgICAgICAgJCgiI2lucHV0IikudmFsKCIiKTsKICAgICAgICB9CgogICAgICAgIGZ1bmN0aW9uIHByZXZpb3VzQ29tbWFuZCgpIHsKICAgICAgICAgICAgdXBkYXRlQ29tbWFuZChjb21tYW5kSW5kZXggKyAxKTsKICAgICAgICB9CgogICAgICAgIGZ1bmN0aW9uIG5leHRDb21tYW5kKCkgewogICAgICAgICAgICB1cGRhdGVDb21tYW5kKGNvbW1hbmRJbmRleCAtIDEpOwogICAgICAgIH0KCiAgICAgICAgZnVuY3Rpb24gdXBkYXRlQ29tbWFuZChpbmRleCkgewogICAgICAgICAgICAvLyBDaGVjayBpZiB3ZSBhcmUgYXQgdGhlIGRlZnVhbHQgaW5kZXggYW5kIGNsZWFyIHRoZSBpbnB1dAogICAgICAgICAgICBpZiAoaW5kZXggPCAwKSB7CiAgICAgICAgICAgICAgICByZXNldElucHV0KCk7CiAgICAgICAgICAgICAgICByZXR1cm47CiAgICAgICAgICAgIH0KCiAgICAgICAgICAgICQuZ2V0KCJjb25zb2xlL2NvbW1hbmRIaXN0b3J5P2luZGV4PSIgKyBpbmRleCwgZnVuY3Rpb24gKGRhdGEsIHN0YXR1cykgewogICAgICAgICAgICAgICAgaWYgKGRhdGEpIHsKICAgICAgICAgICAgICAgICAgICBjb21tYW5kSW5kZXggPSBpbmRleDsKICAgICAgICAgICAgICAgICAgICAkKCIjaW5wdXQiKS52YWwoU3RyaW5nKGRhdGEpKTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfSk7CiAgICAgICAgfQoKICAgICAgICBmdW5jdGlvbiBjb21wbGV0ZShjb21tYW5kKSB7CiAgICAgICAgICAgICQuZ2V0KCJjb25zb2xlL2NvbXBsZXRlP2NvbW1hbmQ9IiArIGNvbW1hbmQsIGZ1bmN0aW9uIChkYXRhLCBzdGF0dXMpIHsKICAgICAgICAgICAgICAgIGlmIChkYXRhKSB7CiAgICAgICAgICAgICAgICAgICAgJCgiI2lucHV0IikudmFsKFN0cmluZyhkYXRhKSk7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0pOwogICAgICAgIH0KCiAgICAgICAgLy8gUG9sbCB0byB1cGRhdGUgdGhlIGNvbnNvbGUgb3V0cHV0CiAgICAgICAgd2luZG93LnNldEludGVydmFsKGZ1bmN0aW9uICgpIHsKICAgICAgICAgICAgdXBkYXRlQ29uc29sZShudWxsKQogICAgICAgIH0sIDUwMCk7CiAgICA8L3NjcmlwdD4KPC9oZWFkPgoKPGJvZHkgY2xhc3M9ImNvbnNvbGUiPgo8YnV0dG9uIGlkPSJwYXVzZVVwZGF0ZXMiPlBhdXNlIFVwZGF0ZXM8L2J1dHRvbj4KPGRpdiBpZD0ib3V0cHV0IiBjbGFzcz0iY29uc29sZSI+PC9kaXY+Cjx0ZXh0YXJlYSBpZD0iaW5wdXQiIGNsYXNzPSJjb25zb2xlIiBhdXRvZm9jdXMgcm93cz0iMSI+PC90ZXh0YXJlYT4KCjxzY3JpcHQ+CiAgICAvLyBzZXR1cCBvdXIgcGF1c2UgdXBkYXRlcyBidXR0b24KICAgICQoIiNwYXVzZVVwZGF0ZXMiKS5jbGljayhmdW5jdGlvbiAoKSB7CiAgICAgICAgLy9jb25zb2xlLmxvZygicGF1c2UgdXBkYXRlcyAiICsgaXNVcGRhdGVQYXVzZWQpOwogICAgICAgIGlzVXBkYXRlUGF1c2VkID0gIWlzVXBkYXRlUGF1c2VkOwogICAgICAgICQoIiNwYXVzZVVwZGF0ZXMiKS5odG1sKGlzVXBkYXRlUGF1c2VkID8gIlJlc3VtZSBVcGRhdGVzIiA6ICJQYXVzZSBVcGRhdGVzIik7CiAgICB9KTsKCiAgICAkKCIjaW5wdXQiKS5rZXlkb3duKGZ1bmN0aW9uIChlKSB7CiAgICAgICAgaWYgKGUua2V5Q29kZSA9PSAxMykgeyAvLyBFbnRlcgogICAgICAgICAgICAvLyB3ZSBkb24ndCB3YW50IGEgbGluZSBicmVhayBpbiB0aGUgY29uc29sZQogICAgICAgICAgICBlLnByZXZlbnREZWZhdWx0KCk7CiAgICAgICAgICAgIHJ1bkNvbW1hbmQoJCgiI2lucHV0IikudmFsKCkpOwogICAgICAgIH0gZWxzZSBpZiAoZS5rZXlDb2RlID09IDM4KSB7IC8vIFVwCiAgICAgICAgICAgIHByZXZpb3VzQ29tbWFuZCgpOwogICAgICAgIH0gZWxzZSBpZiAoZS5rZXlDb2RlID09IDQwKSB7IC8vIERvd24KICAgICAgICAgICAgbmV4dENvbW1hbmQoKTsKICAgICAgICB9IGVsc2UgaWYgKGUua2V5Q29kZSA9PSAyNykgeyAvLyBFc2NhcGUKICAgICAgICAgICAgcmVzZXRJbnB1dCgpOwogICAgICAgIH0gZWxzZSBpZiAoZS5rZXlDb2RlID09IDkpIHsgLy8gVGFiCiAgICAgICAgICAgIGUucHJldmVudERlZmF1bHQoKTsKICAgICAgICAgICAgY29tcGxldGUoJCgiI2lucHV0IikudmFsKCkpOwogICAgICAgIH0KICAgIH0pOwo8L3NjcmlwdD4KPC9ib2R5PgoKPC9odG1sPg==";

        public static string INDEX_CSS =
            "aHRtbCwgYm9keSB7CiAgICBoZWlnaHQ6MTAwJTsKfQoKdGV4dGFyZWEgewogICAgcmVzaXplOm5vbmU7Cn0KCmJvZHkuY29uc29sZSB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOmJsYWNrOwp9CgpkaXYuY29uc29sZSB7CiAgICBoZWlnaHQ6OTglOwogICAgd2lkdGg6MTAwJTsKICAgIGJhY2tncm91bmQtY29sb3I6YmxhY2s7CiAgICBjb2xvcjojRjBGMEYwOwogICAgZm9udC1zaXplOjE0cHg7CiAgICBmb250LWZhbWlseTptb25vc3BhY2U7CiAgICBvdmVyZmxvdy15OmF1dG87CiAgICBvdmVyZmxvdy14OmF1dG87CiAgICB3aGl0ZS1zcGFjZTpub3JtYWw7CiAgICB3b3JkLXdyYXA6YnJlYWstd29yZDsKfQoKdGV4dGFyZWEuY29uc29sZSB7CiAgICB3aWR0aDoxMDAlOwogICAgYmFja2dyb3VuZC1jb2xvcjpibGFjazsKICAgIGNvbG9yOiNGMEYwRjA7CiAgICBmb250LXNpemU6MTRweDsKICAgIGZvbnQtZmFtaWx5Om1vbm9zcGFjZTsKICAgIHBvc2l0aW9uOmZpeGVkOwogICAgYm90dG9tOiAzcHg7CiAgICBkaXNwbGF5OiBibG9jazsKfQoKc3Bhbi5XYXJuaW5nIHsKICAgIGNvbG9yOiNmNGU1NDI7Cn0KCnNwYW4uQXNzZXJ0IHsKICAgIGNvbG9yOiNmNGU1NDI7Cn0KCnNwYW4uRXJyb3IgewogICAgY29sb3I6I2ZmMDAwMDsKfQoKc3Bhbi5FeGNlcHRpb24gewogICAgY29sb3I6I2ZmMDAwMDsKfQoKc3Bhbi5IZWxwIHsKICAgIGNvbG9yOiMxNmYzZmY7Cn0KCmJ1dHRvbiNwYXVzZVVwZGF0ZXMgewogICAgd2lkdGg6MTUwcHg7CiAgICBoZWlnaHQ6NDBweDsKICAgIHBvc2l0aW9uOmZpeGVkOwogICAgZmxvYXQ6cmlnaHQ7CiAgICBtYXJnaW4tcmlnaHQ6NTBweDsKICAgIG1hcmdpbi10b3A6MTBweDsKICAgIHJpZ2h0OjBweDsKICAgIG9wYWNpdHk6LjU7Cn0=";

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

        private static string _logWarningFilePath;
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

            Log("<span class='Help'>" + help + "</span>");
        }

        [Command("log", "print unity log with line n", false)]
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
                        Log("-> " + file);
                }
            }
        }

        [Command("pdirs",
            "print all files in persistent directory, parameters could be ignored folders, split with space")]
        public static void PersistentDirectory(string[] args)
        {
            PrintDirectoryFiles(Application.persistentDataPath, args);
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
            Instance.m_output.Add(str);
            if (Instance.m_output.Count > MAX_LINES)
                Instance.m_output.RemoveAt(0);
        }

        private static void LogToFile(string log, LogType type)
        {
            var newLog = string.Format("{0} : {1}", DateTime.Now.ToShortTimeString(), log);

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

            if (type != LogType.Log)
            {
                Console.LogToFile("<span class='" + type + "'>" + logString + "\n" + stackTrace + "</span>", type);
            }
            else
            {
                Console.LogToFile(logString, type);
            }


            //TODO: add lua execute result logs to show on real time.
            if (!string.IsNullOrEmpty(LUA_FILTER_KEY) && stackTrace.ToLower().Contains(LUA_FILTER_KEY))
            {
                Log(logString);
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