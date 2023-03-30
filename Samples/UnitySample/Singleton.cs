using UnityEngine;

namespace UnitySample
{
    /// <summary>
    /// A singleton template that adds component to the scene automatically and persists across scenes
    /// </summary>
    /// <typeparam name="T">Type of the Component</typeparam>
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindObjectOfType<T>();
                    if (s_instance == null)
                    {
                        s_instance = new GameObject("Segment Singleton").AddComponent<T>();
                        DontDestroyOnLoad(s_instance.gameObject);
                    }
                }

                return s_instance;
            }
        }

        protected virtual void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
