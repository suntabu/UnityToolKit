using System;
using System.Reflection;

namespace UnityToolKit.Singleton
{
    /// <summary>
    /// Author:gouzhun 
    ///  thread safe singleton base class for inheriting.
    /// </summary>
    /// <typeparam name="T">the generic type</typeparam>
    public class Singleton<T> where T : class
    {
        static object _lock = new object();

        private volatile static T _instance;

        /// <summary>
        /// singleton instance, the object is created when this api first called.
        /// so pay attention to the first calling if you want to do some performance improvement.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            ConstructorInfo constructor = null;
                            // Binding flags exclude public constructors.
                            constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[0], null);

                            if (constructor == null || constructor.IsAssembly)
                            {
                                throw new Exception(string.Format("A private or " + "protected constructor is missing for '{0}'.", typeof(T).Name));
                            }
                            // Also exclude internal constructors.
                            _instance = (T)constructor.Invoke(null);
                        }
                    }
                }
                return _instance;
            }

        }

        static Singleton()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        public void DestroyInstance()
        {
            _instance = null;
        }

        public virtual void Clear()
        {
            throw new NotImplementedException("Clear methods is not implemented");
        }

    }
}