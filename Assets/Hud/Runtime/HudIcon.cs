using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud
{
    /// <summary>Small original geometric icons; no external art or font glyph dependency.</summary>
    public sealed class HudIcon : MaskableGraphic
    {
        public enum Shape { Coin, Energy, Resort }
        [SerializeField] Shape shape;
        public void SetShape(Shape value) { shape=value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r=rectTransform.rect; var c=r.center;
            float size=Mathf.Min(r.width,r.height)*.46f;
            if(shape==Shape.Energy)
            {
                var points = new[] {new Vector2(.12f,1),new Vector2(-.72f,-.08f),new Vector2(-.06f,-.08f),new Vector2(-.22f,-1),new Vector2(.76f,.24f),new Vector2(.08f,.24f)};
                foreach(var p in points) mesh.AddVert(c+p*size,color,Vector2.zero);
                mesh.AddTriangle(0,1,2); mesh.AddTriangle(0,2,5); mesh.AddTriangle(2,3,4); mesh.AddTriangle(2,4,5);
            }
            else if(shape==Shape.Resort)
            {
                mesh.AddVert(c,color,Vector2.zero);
                for(int i=0;i<10;i++) { float a=(90+i*36)*Mathf.Deg2Rad; mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size*(i%2==0?1:.48f),color,Vector2.zero); }
                for(int i=1;i<=10;i++) mesh.AddTriangle(0,i,i==10?1:i+1);
            }
            else
            {
                mesh.AddVert(c,color,Vector2.zero);
                for(int i=0;i<40;i++) {float a=i*Mathf.PI*2/40; mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size,color,Vector2.zero);}
                for(int i=1;i<=40;i++) mesh.AddTriangle(0,i,i==40?1:i+1);
                int first=mesh.currentVertCount;
                var dark=new Color(.72f,.35f,.035f,1);
                foreach(var p in new[]{new Vector2(-.13f,-.53f),new Vector2(-.13f,.53f),new Vector2(.13f,.53f),new Vector2(.13f,-.53f)}) mesh.AddVert(c+p*size,dark,Vector2.zero);
                mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
            }
        }
    }
}
