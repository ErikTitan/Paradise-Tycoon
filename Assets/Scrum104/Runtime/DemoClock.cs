using UnityEngine;
using UnityEngine.UI;
namespace ParadiseTycoon.Scrum104
{
    public sealed class DemoClock : MonoBehaviour
    {
        public Text label;
        public RectTransform marker;
        public float Elapsed { get; private set; }
        void Update()
        {
            Elapsed += Time.deltaTime;
            label.text = "TESTOVACIA SCÉNA\nHerný čas: " + Elapsed.ToString("F1") + " s\nPočas pauzy sa čas aj pohyb zastavia.";
            marker.anchoredPosition = new Vector2(Mathf.Sin(Elapsed) * 220, -110);
        }
    }
}
