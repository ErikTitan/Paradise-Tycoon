using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud
{
    /// <summary>Resolution-independent rounded panel with a subtle vertical gradient.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudPanel : MaskableGraphic
    {
        [SerializeField] float radius = 18;
        [SerializeField] Color bottomTint = new Color(.8f,.8f,.8f,1);
        public void Style(Color top, Color bottom, float cornerRadius)
        { color = top; bottomTint = bottom; radius = cornerRadius; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            float round = Mathf.Min(radius, Mathf.Min(r.width,r.height)*.5f);
            Color Shade(float y) => Color.Lerp(bottomTint,color,Mathf.InverseLerp(r.yMin,r.yMax,y));
            mesh.AddVert(r.center,Shade(r.center.y),Vector2.one*.5f);
            int count = 0;
            for(int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? r.xMax-round : r.xMin+round,
                    corner == 0 || corner == 3 ? r.yMin+round : r.yMax-round);
                for(int step = 0; step <= 8; step++)
                {
                    float angle = (-90+corner*90+step*90f/8)*Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*round;
                    mesh.AddVert(point,Shade(point.y),Vector2.zero); count++;
                }
            }
            for(int i = 1; i <= count; i++) mesh.AddTriangle(0,i,i==count ? 1 : i+1);
        }
    }
}

