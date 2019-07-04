using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace UnityToolKit.UI
{
    /// <summary>
    /// 修改图片显示大小，触摸区域不变
    /// </summary>
    [ExecuteInEditMode ,AddComponentMenu("UI/Extend Image")]
    public class ExtendImage : Image
    {

        [Header ("Horizontal, Vertical")]
        [SerializeField]
        private Vector2 m_Extend=Vector2.zero;

        public Vector2 extend{
            get{ return m_Extend; }
            set{
                if(!m_Extend.Equals(value)){
                    m_Extend = value;
                    SetVerticesDirty();
                }
            }
        }

        protected override void OnPopulateMesh (VertexHelper toFill)
        {
            base.OnPopulateMesh (toFill);

            var rect = GetPixelAdjustedRect ();
            if (rect.width < m_Extend.x || rect.height < m_Extend.y) {
                return;
            }

            var ratioX = (rect.width - m_Extend.x) / rect.width;
            var ratioY = (rect.height - m_Extend.y) / rect.height;

            var verticesCount = toFill.currentVertCount;
            for (int i = 0; i < verticesCount; i++) {
                var v = new UIVertex ();
                toFill.PopulateUIVertex (ref v, i);
                v.position.x *= ratioX;
                v.position.y *= ratioY;
                toFill.SetUIVertex (v, i);
            }
        }

        public override void SetNativeSize()
        {
            if (overrideSprite != null)
            {
                float w = overrideSprite.rect.width / pixelsPerUnit;
                float h = overrideSprite.rect.height / pixelsPerUnit;
                rectTransform.anchorMax = rectTransform.anchorMin;
                rectTransform.sizeDelta = new Vector2(w+m_Extend.x, h+m_Extend.y);
                SetAllDirty();
            }
        }
    }
}