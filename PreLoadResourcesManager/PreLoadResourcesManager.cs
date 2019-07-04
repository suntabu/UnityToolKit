using UnityToolKit.UnityObjects;

namespace UnityToolKit.PreLoadResourcesManager
{
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine.UI;
    using System;

    public class PreLoadResourcesManager : MonoBehaviour
    {
        //public delegate void OnResourecesLoadComplete();
        public event Action CallBack;

        public enum AssetPath
        {
            //资源路径
            StreamingAssets,
            Resources
        }

        public enum AssetType
        {
            //资源类型
            Sprite = 0,
            Texture2D = 1,
            Sprites //如果是Sprites，则只能在Resource目录中
        }

        public enum Wrap
        {
            Clamp,
            Repeat
        }

        public bool isAllready { get; private set; } //是否加载完成

        [System.Serializable]
        public class Asset
        {
            public string path;
            public AssetPath folder;
            public AssetType type;
            public SpriteMeshType meshType = SpriteMeshType.FullRect;
            public Vector2 pivot = Vector2.one * 0.5f;
            public bool readable = false; //如果加载的是图片，是否可读
            public bool cache = false; //是否缓存
            public Wrap warpMode;
            public Transform sprite;
            public Material material;
        }

        [Header("加载StreemAssets资源")] public List<Asset> preLoadResources = new List<Asset>();


        private int m_loadIndex;
        private int m_loadTotal;

        private bool m_preLoaded = false;

        public bool preLoaded
        {
            get { return m_preLoaded; }
        }

        public static PreLoadResourcesManager Instance
        {
            get
            {
                if (resourcesManagerObjs.Count > 0)
                {
                    if (resourcesManagerObjs.Peek() != null)
                    {
                        return resourcesManagerObjs.Peek();
                    }
                    else
                    {
                        resourcesManagerObjs.Pop();
                        return null;
                    }
                }
                return null;
            }
        }

        private Dictionary<Material, Texture> catchMaterialTexture = new Dictionary<Material, Texture>();

        //存放多个对象，返回当前操作的对象  场景和场景中的弹出窗
        private static Stack<PreLoadResourcesManager> resourcesManagerObjs = new Stack<PreLoadResourcesManager>();

        void Awake()
        {
            isAllready = false;
            resourcesManagerObjs.Push(this);
//		StartPreLoad ();
        }

        /// <summary>
        /// 开始预加载
        /// </summary>
        /// <returns>The load.</returns>
        public void StartPreLoad(Action callback = null)
        {
            isAllready = false;
            this.CallBack += callback;
            if (preLoadResources != null && preLoadResources.Count > 0 && m_loadIndex != preLoadResources.Count)
            {
                m_loadTotal = preLoadResources.Count;
                foreach (Asset asset in preLoadResources)
                {
                    StartCoroutine(LoadAsset(asset));
                }
            }
            else
            {
                PreLoadComplete();
            }
        }

        /// <summary>
        /// 添加预加载资源，需要在StartPreload之前调用
        /// </summary>
        /// <param name="assets">Assets.</param>
        public void AddPreloadAssets(List<Asset> assets)
        {
        }

        IEnumerator LoadAsset(Asset asset)
        {
            if (asset.folder == AssetPath.Resources)
            {
                ResourceRequest rr;
                switch (asset.type)
                {
                    case AssetType.Texture2D:
//				if(asset.cache)
//				{
//					if(!cachedTexture2D.ContainsKey(asset.path)){
//						ResourceRequest rr = Resources.LoadAsync<Texture2D>(asset.path);
//						yield return rr;
//						if(rr.isDone){
//							cachedTexture2D[asset.path] = rr.asset as Texture2D;
//						}
//					}
//				}
//				else
//				{
//					if(!loadedTexture2D.ContainsKey(asset.path)){
                        rr = Resources.LoadAsync<Texture2D>(asset.path);
                        yield return rr;
                        if (rr.isDone)
                        {
//							loadedTexture2D[asset.path] = rr.asset as Texture2D;
                        }
//					}
//				}
                        break;
                    case AssetType.Sprite:
//				if(asset.cache)
//				{
//					if(!cachedSprite.ContainsKey(asset.path)){
//						ResourceRequest rr = Resources.LoadAsync<Sprite>(asset.path);
//						yield return rr;
//						if(rr.isDone){
//							cachedSprite[asset.path] = rr.asset as Sprite;
//						}
//					}
//				}
//				else
//				{
//					if(!loadedSprite.ContainsKey(asset.path)){
                        rr = Resources.LoadAsync<Sprite>(asset.path);
                        yield return rr;
                        if (rr.isDone)
                        {
//							loadedSprite[asset.path] = rr.asset as Sprite;
                        }
//					}
//				}
                        break;
                    case AssetType.Sprites:
//				if(asset.cache)
//				{
//					if(!cachedTexture2D.ContainsKey(asset.path)){
//						Sprite[] sprites = Resources.LoadAll<Sprite>(asset.path);
//						for(int i=0;i<sprites.Length;++i){
//							cachedSprite[asset.path+"/"+sprites[i].name] = sprites[i];
//						}
//						cachedTexture2D[asset.path] = sprites[0].texture;
//					}
//				}
//				else
//				{
//					if(!loadedTexture2D.ContainsKey(asset.path)){
                        Sprite[] sprites = Resources.LoadAll<Sprite>(asset.path);
                        for (int i = 0; i < sprites.Length; ++i)
                        {
//							loadedSprite[asset.path+"/"+sprites[i].name] = sprites[i];
                        }
//						loadedTexture2D[asset.path] = sprites[0].texture;
//					}
//				}
                        break;
                }
                m_loadIndex++;
            }
            else
            {
                bool check = true;
//			if(asset.cache)
//			{
//				if(asset.type== AssetType.Texture2D){
//					if(cachedTexture2D.ContainsKey(asset.path)) check=false;
//				}else if(asset.type== AssetType.Sprite){
//					if(cachedSprite.ContainsKey(asset.path)) check=false;
//				}
//			}
//			else
//			{
//				if(asset.type== AssetType.Texture2D){
//					if(loadedTexture2D.ContainsKey(asset.path)) check=false;
//				}else if(asset.type== AssetType.Sprite){
//					if(loadedSprite.ContainsKey(asset.path)) check=false;
//				}
//			}

                if (check)
                {
                    WWW www = new WWW(PathUtils.streamingAssetPath + asset.path);
                    yield return www;
                    if (www.error == null || www.error.Length == 0)
                    {
                        switch (asset.type)
                        {
                            case AssetType.Texture2D:
                            case AssetType.Sprite:
                                Texture2D t = null;
                                if (asset.readable)
                                {
                                    t = www.texture;
                                }
                                else
                                {
                                    t = www.textureNonReadable;
                                }
                                t.wrapMode = asset.warpMode == Wrap.Clamp
                                    ? TextureWrapMode.Clamp
                                    : TextureWrapMode.Repeat;
                                t.filterMode = FilterMode.Bilinear;

                                if (asset.type == AssetType.Sprite)
                                {
                                    Sprite sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), asset.pivot, 100f,
                                        0,
                                        asset.meshType);
//							if (asset.cache) {
//								cachedSprite [asset.path] = sp;
//							} else {
//								loadedSprite [asset.path] = sp;
//							}
                                    if (asset.sprite != null && sp != null)
                                    {
                                        SpriteRenderer sr = asset.sprite.GetComponent<SpriteRenderer>();
                                        Image img = asset.sprite.GetComponent<Image>();
                                        RawImage rawImg = asset.sprite.GetComponent<RawImage>();
                                        if (rawImg != null)
                                        {
                                            rawImg.texture = t;
                                            rawImg.SetNativeSize();
                                        }
                                        if (sr != null)
                                            sr.sprite = sp;
                                        if (img != null)
                                            img.sprite = sp;
                                    }
                                    else
                                        Debug.LogError("资源设置对象没有Image，SpriteRenderer对象：" + asset.sprite.name);
                                }
                                else if (asset.type == AssetType.Texture2D)
                                {
//							if(asset.cache)
//							{
//								cachedTexture2D[asset.path] = t;
//							}
//							else
//							{
//								loadedTexture2D[asset.path] = t;
//							}
                                    catchMaterialTexture[asset.material] = asset.material.mainTexture;
                                    asset.material.mainTexture = t;
                                }
                                break;
                        }
                    }
                    else
                    {
                        print(www.error);
                    }
                    www.Dispose();
                }
                m_loadIndex++;
            }
            if (m_loadIndex == m_loadTotal)
            {
                yield return new WaitForEndOfFrame();
                PreLoadComplete();
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
        }

        void OnDestroy()
        {
            resourcesManagerObjs.Pop();
            foreach (Asset asset in preLoadResources)
            {
                if (asset.type == AssetType.Sprite && asset.sprite != null)
                {
                    SpriteRenderer sr = asset.sprite.GetComponent<SpriteRenderer>();
                    Image img = asset.sprite.GetComponent<Image>();
                    Sprite sp = null;
                    if (sr != null)
                    {
                        sp = sr.sprite;
                        sr.sprite = null;
                    }
                    if (img != null)
                    {
                        sp = img.sprite;
                        img.sprite = null;
                    }
                    GameObject.DestroyImmediate(sp);
                }
            }

            foreach (KeyValuePair<Material, Texture> key in catchMaterialTexture)
            {
                GameObject.DestroyImmediate(key.Key.mainTexture);
                key.Key.mainTexture = key.Value;
            }
            catchMaterialTexture.Clear();
        }

        /// <summary>
        /// 预先加载完成
        /// </summary>
        protected virtual void PreLoadComplete()
        {
            isAllready = true;
            if (CallBack != null)
                CallBack();
            CallBack = null;
        }


        public void DestroyLoaded()
        {
//		foreach(Sprite t in loadedSprite.Values){
//			if(t!=null && t.textureRect.width==t.texture.width && t.textureRect.height==t.texture.height ) {
//				DestroyImmediate(t);
//			}
//		}
//		foreach(Texture2D t in loadedTexture2D.Values){
//			if(t!=null) DestroyImmediate(t);
//		}
//		loadedSprite.Clear();
//		loadedTexture2D.Clear();
        }

        /// <summary>
        /// 删除缓存的，默认不会删除
        /// </summary>
        public void DestroyCached()
        {
//		foreach(Sprite t in cachedSprite.Values){
//			if(t!=null && t.textureRect.width==t.texture.width && t.textureRect.height==t.texture.height ) {
//				DestroyImmediate(t);
//			}
//		}
//		foreach(Texture2D t in cachedTexture2D.Values){
//			if(t!=null) DestroyImmediate(t);
//		}
//		cachedSprite.Clear();
//		cachedTexture2D.Clear();
        }
    }
}