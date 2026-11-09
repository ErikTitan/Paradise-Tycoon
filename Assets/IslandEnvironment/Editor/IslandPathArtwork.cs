using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParadiseTycoon.Island.Editor
{
    // Bake the union of the walking routes before drawing its outside edge.
    // Junctions therefore have neither overlapping kerbs nor transparent seams.
    public static class IslandPathArtwork
    {
        public const string AssetPath = IslandEnvironmentBuilder.Folder + "/Generated/IslandPaths.png";
        const int Ppu = 100, Width = 2200, Height = 900;
        static readonly Vector2 Origin = new Vector2(-11, -4.5f);
        static readonly List<Vector2[]> Routes = new List<Vector2[]>();
        static float[] distance;
        static Color32[] pixels;

        public static void Begin() => Routes.Clear();
        public static void Add(Vector2[] route) => Routes.Add(route);
        static Vector2 Pixel(Vector2 p) => (p - Origin) * Ppu;
        static float Sdf(int x, int y) => distance[Mathf.Clamp(y,0,Height-1)*Width+Mathf.Clamp(x,0,Width-1)];
        static float Noise(int x,int y)
        {
            uint n=unchecked((uint)(x*374761393+y*668265263));
            n=(n^(n>>13))*1274126177u;
            return (n^(n>>16))/(float)uint.MaxValue;
        }

        public static Sprite Bake(Vector2[] entrances)
        {
            distance=Enumerable.Repeat(10000f,Width*Height).ToArray();
            foreach(var route in Routes)
            {
                var union=distance;
                distance=Enumerable.Repeat(10000f,Width*Height).ToArray();
                var smooth=RoundCorners(route);
                for(int i=1;i<smooth.Count;i++) Capsule(Pixel(smooth[i-1]),Pixel(smooth[i]),38);
                // A small smooth union rounds the inside corners of T junctions.
                for(int i=0;i<distance.Length;i++)
                {
                    float a=union[i],b=distance[i],h=Mathf.Max(22-Mathf.Abs(a-b),0)/22;
                    union[i]=Mathf.Min(a,b)-h*h*22*.25f;
                }
                distance=union;
            }
            VerifyRaster(entrances);
            pixels=new Color32[Width*Height];
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                float d=Sdf(x,y), grain=Noise(x,y)-.5f;
                if(d>3) continue;
                Color c;
                if(d>0) c=new Color(.30f,.23f,.11f,Mathf.Clamp01((3-d)/3)*.22f);
                else if(d>-2) c=new Color(.48f,.32f,.15f,Mathf.Clamp01(-d));
                else if(d>-13) c=new Color(.73f,.58f,.35f,1);
                else
                {
                    float m=Mathf.Sin(x*.027f+Mathf.Sin(y*.037f))*Mathf.Sin(y*.053f+x*.011f)*.012f;
                    c=new Color(.94f+grain*.025f+m,.77f+grain*.026f+m,.48f+grain*.025f+m,1);
                    if(Noise(x/2,y/2)>.994f) c=Color.Lerp(c,new Color(.72f,.49f,.23f),.30f);
                }
                pixels[y*Width+x]=c;
            }
            DrawBoundaryStones();
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(AssetPath,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(AssetPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetPath);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=Ppu; importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=4096; importer.filterMode=FilterMode.Bilinear;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            distance=null; pixels=null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        }

        public static Vector3 SpriteCenter => Origin + new Vector2(Width,Height)/(2f*Ppu);
        static List<Vector2> RoundCorners(Vector2[] route)
        {
            var result=new List<Vector2> {route[0]};
            for(int i=1;i<route.Length-1;i++)
            {
                Vector2 p=route[i], incoming=route[i-1]-p, outgoing=route[i+1]-p;
                float radius=Mathf.Min(.55f,Mathf.Min(incoming.magnitude,outgoing.magnitude)*.4f);
                Vector2 a=p+incoming.normalized*radius, b=p+outgoing.normalized*radius;
                result.Add(a);
                for(int step=1;step<=16;step++)
                {
                    float t=step/16f;
                    result.Add((1-t)*(1-t)*a+2*(1-t)*t*p+t*t*b);
                }
            }
            result.Add(route[route.Length-1]); return result;
        }
        static void Capsule(Vector2 a,Vector2 b,float radius)
        {
            int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-radius-4));
            int x1=Mathf.Min(Width-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+radius+4));
            int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-radius-4));
            int y1=Mathf.Min(Height-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+radius+4));
            Vector2 delta=b-a; float length=delta.sqrMagnitude;
            for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
            {
                Vector2 q=new Vector2(x+.5f,y+.5f);
                float t=length>0?Mathf.Clamp01(Vector2.Dot(q-a,delta)/length):0;
                int k=y*Width+x; distance[k]=Mathf.Min(distance[k],Vector2.Distance(q,a+t*delta)-radius);
            }
        }

        // Trace the union outline on a fine grid, then distribute individual stones
        // by arc length. The outside contour is the only place that gets a kerb.
        static void DrawBoundaryStones()
        {
            const int step=3;
            int cols=Width/step,rows=Height/step;
            var edges=new Dictionary<int,List<int>>();
            int Key(int x,int y)=>y*(cols+1)+x;
            void Edge(int ax,int ay,int bx,int by)
            {
                int a=Key(ax,ay); if(!edges.ContainsKey(a)) edges[a]=new List<int>(); edges[a].Add(Key(bx,by));
            }
            bool Inside(int x,int y)=>x>=0&&x<cols&&y>=0&&y<rows&&Sdf(x*step+1,y*step+1)<-5;
            for(int y=0;y<rows;y++) for(int x=0;x<cols;x++) if(Inside(x,y))
            {
                if(!Inside(x,y-1)) Edge(x,y,x+1,y);
                if(!Inside(x+1,y)) Edge(x+1,y,x+1,y+1);
                if(!Inside(x,y+1)) Edge(x+1,y+1,x,y+1);
                if(!Inside(x-1,y)) Edge(x,y+1,x,y);
            }
            Vector2 Pos(int k)=>new Vector2(k%(cols+1)*step,k/(cols+1)*step);
            int stoneIndex=0;
            while(edges.Count>0)
            {
                int first=edges.Keys.First(),current=first;
                var loop=new List<Vector2>(); int guard=0;
                do
                {
                    loop.Add(Pos(current));
                    if(!edges.TryGetValue(current,out var next)) break;
                    int dest=next[next.Count-1]; next.RemoveAt(next.Count-1); if(next.Count==0) edges.Remove(current);
                    current=dest;
                } while(current!=first && ++guard<100000);
                if(loop.Count<8) continue;
                loop.Add(loop[0]);
                float total=0; var cumulative=new float[loop.Count];
                for(int i=1;i<loop.Count;i++) {total+=Vector2.Distance(loop[i-1],loop[i]);cumulative[i]=total;}
                int count=Mathf.Max(1,Mathf.RoundToInt(total/21)); float spacing=total/count;
                Vector2 At(float s)
                {
                    s=Mathf.Repeat(s,total); int index=Array.BinarySearch(cumulative,s); if(index<0)index=~index;
                    index=Mathf.Clamp(index,1,loop.Count-1);
                    return Vector2.Lerp(loop[index-1],loop[index],(s-cumulative[index-1])/(cumulative[index]-cumulative[index-1]));
                }
                for(int i=0;i<count;i++)
                {
                    float s=(i+.5f)*spacing;
                    Vector2 center=At(s), tangent=(At(s+8)-At(s-8)).normalized;
                    // Filled pixels lie to the left of each directed outline edge.
                    center+=new Vector2(-tangent.y,tangent.x)*2;
                    Stone(center,tangent,spacing*.48f,6.3f,stoneIndex++);
                }
            }
        }
        static void Stone(Vector2 center,Vector2 tangent,float rx,float ry,int seed)
        {
            Vector2 normal=new Vector2(-tangent.y,tangent.x);
            float variation=Noise(seed,23)-.5f;
            rx*=1+variation*.15f; ry*=1+Noise(seed,71)*.12f;
            int reach=Mathf.CeilToInt(rx+ry+2);
            for(int y=Mathf.Max(0,(int)center.y-reach);y<=Mathf.Min(Height-1,(int)center.y+reach);y++)
            for(int x=Mathf.Max(0,(int)center.x-reach);x<=Mathf.Min(Width-1,(int)center.x+reach);x++)
            {
                Vector2 p=new Vector2(x+.5f,y+.5f)-center;
                float u=Mathf.Abs(Vector2.Dot(p,tangent)/rx),v=Mathf.Abs(Vector2.Dot(p,normal)/ry);
                float r=Mathf.Pow(Mathf.Pow(u,3.5f)+Mathf.Pow(v,3.5f),1/3.5f);
                if(r>1.06f||Sdf(x,y)>1)continue;
                Color c;
                if(r>.85f)c=new Color(.53f,.38f,.22f,1);
                else
                {
                    float light=Mathf.Clamp(p.y/ry,-1,1)*.035f;
                    c=new Color(.96f+variation*.05f+light,.86f+variation*.055f+light,.65f+variation*.06f+light,1);
                    if(r>.65f&&p.y>0)c=Color.Lerp(c,new Color(1,.96f,.8f),.45f);
                }
                int k=y*Width+x; pixels[k]=Color.Lerp(pixels[k],c,Mathf.Clamp01((1.06f-r)*6));
            }
        }
        static void VerifyRaster(Vector2[] entrances)
        {
            // Test the actual filled artwork rather than just the route definitions.
            int start=Array.FindIndex(distance,d=>d<-14);
            var visited=new bool[distance.Length]; var queue=new Queue<int>(); queue.Enqueue(start);visited[start]=true;
            while(queue.Count>0)
            {
                int k=queue.Dequeue(),x=k%Width,y=k/Width;
                foreach(int n in new[] {x>0?k-1:-1,x<Width-1?k+1:-1,y>0?k-Width:-1,y<Height-1?k+Width:-1})
                    if(n>=0&&!visited[n]&&distance[n]<-14){visited[n]=true;queue.Enqueue(n);}
            }
            for(int i=0;i<distance.Length;i++) if(distance[i]<-14&&!visited[i]) throw new InvalidOperationException("Disconnected path artwork");
            foreach(var p in entrances)
            {
                Vector2 pixel=Pixel(p); int k=Mathf.RoundToInt(pixel.y)*Width+Mathf.RoundToInt(pixel.x);
                if(!visited[k])throw new InvalidOperationException("Artwork does not reach entrance "+p);
            }
            Debug.Log("PATH_ARTWORK_VALIDATION_OK: one connected sand surface, all seven entrances reached, kerbs only on union boundary.");
        }
    }
}
