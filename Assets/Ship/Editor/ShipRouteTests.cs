using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.SceneFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParadiseTycoon.Ship.Editor
{
    /// <summary>The guest boat must stay on water on its way to the dock and back.</summary>
    public sealed class ShipRouteTests
    {
        const string MainScene = "Assets/Scenes/Main.unity";

        /// <summary>Alpha of a sprite's source PNG, so import settings do not need Read/Write.</summary>
        sealed class Pixels
        {
            readonly SpriteRenderer renderer;
            readonly Color32[] colors;
            readonly int width, height;
            readonly Vector2 scale;

            public Pixels(SpriteRenderer renderer)
            {
                this.renderer = renderer;
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(renderer.sprite.texture)));
                colors = texture.GetPixels32();
                width = texture.width;
                height = texture.height;
                scale = new Vector2(width / (float)renderer.sprite.texture.width, height / (float)renderer.sprite.texture.height);
                Object.DestroyImmediate(texture);
            }

            bool Solid(Vector2 spritePixel)
            {
                int x = (int)(spritePixel.x * scale.x), y = (int)(spritePixel.y * scale.y);
                return x >= 0 && y >= 0 && x < width && y < height && colors[y * width + x].a > 64;
            }

            public bool Covers(Vector3 world)
            {
                var sprite = renderer.sprite;
                Vector2 local = renderer.transform.InverseTransformPoint(world);
                Vector2 pixel = local * sprite.pixelsPerUnit + sprite.pivot + sprite.rect.position;
                return sprite.rect.Contains(pixel) && Solid(pixel);
            }

            /// <summary>World positions of the visible pixels, sampled every few pixels.</summary>
            public List<Vector3> Outline(int step)
            {
                var sprite = renderer.sprite;
                var points = new List<Vector3>();
                for (int y = (int)sprite.rect.yMin; y < sprite.rect.yMax; y += step)
                for (int x = (int)sprite.rect.xMin; x < sprite.rect.xMax; x += step)
                {
                    var pixel = new Vector2(x, y);
                    if (!Solid(pixel)) continue;
                    Vector2 local = (pixel - sprite.rect.position - sprite.pivot) / sprite.pixelsPerUnit;
                    points.Add(renderer.transform.TransformPoint(local));
                }
                return points;
            }
        }

        Scene scene;

        [SetUp]
        public void Open() => scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);

        [TearDown]
        public void Close() => EditorSceneManager.CloseScene(scene, true);

        T Find<T>(string name = null) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Single(component => name == null || component.name == name);

        [Test]
        public void BoatStaysOnWaterAlongTheWholeRoute()
        {
            var ship = Find<ShipVisual>();
            var hull = new Pixels(ship.GetComponentInChildren<SpriteRenderer>(true));
            var land = new[] { Find<SpriteRenderer>("Island_ContinuousCoast"), Find<SpriteRenderer>("DockLanding") }
                .Select(renderer => new Pixels(renderer)).ToArray();
            var route = ship.Route();
            Vector3 start = ship.transform.position, scale = ship.transform.localScale;
            try
            {
                // The boat sails west on arrival and is mirrored when it sails back east.
                foreach (float facing in new[] { 1f, -1f })
                {
                    ship.transform.position = Vector3.zero;
                    ship.transform.localScale = new Vector3(Mathf.Abs(scale.x) * facing, scale.y, scale.z);
                    var outline = hull.Outline(8);
                    for (int i = 1; i < route.Count; i++)
                    {
                        float length = Vector3.Distance(route[i - 1], route[i]);
                        for (float travelled = 0; travelled <= length; travelled += .1f)
                        {
                            var at = Vector3.MoveTowards(route[i - 1], route[i], travelled);
                            var hit = outline.Select(point => point + at)
                                .Where(point => land.Any(map => map.Covers(point)))
                                .Select(point => (Vector3?)point).FirstOrDefault();
                            Assert.IsNull(hit, $"Boat at {at} overlaps land at {hit}");
                        }
                    }
                }
            }
            finally
            {
                ship.transform.position = start;
                ship.transform.localScale = scale;
            }
        }

        [Test]
        public void BoatEntersAndLeavesOutsideTheCamera()
        {
            var ship = Find<ShipVisual>();
            var framing = Find<IslandCameraFraming>();
            var camera = framing.GetComponent<Camera>();
            float halfBoat = ship.GetComponentInChildren<SpriteRenderer>(true).bounds.extents.x;
            // 20:9 is the widest supported screen, so it shows the most sea.
            framing.Frame(2400, 1080, new Rect(0, 0, 2400, 1080));
            float rightEdge = camera.transform.position.x + camera.orthographicSize * 2400f / 1080f;
            Assert.That(ship.Route()[0].x - halfBoat, Is.GreaterThan(rightEdge),
                "The boat would appear and disappear on screen.");
        }
    }
}
