using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParadiseTycoon.Ship
{
    /// <summary>
    /// Sails the guest boat along a water route between open sea and the dock, and back the same way.
    /// Uses scaled time, so pause stops it.
    /// </summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        [SerializeField] Transform seaPoint;
        [Tooltip("Optional points between the sea and the dock, ordered from the sea. Keep them on water.")]
        [SerializeField] Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField] Transform dockPoint;
        [SerializeField] SpriteRenderer boat;
        [SerializeField, Min(.1f)] float speed = 3.5f;
        [SerializeField, Min(0)] float bobHeight = .04f;

        readonly List<Vector3> path = new();
        Vector3 boatOffset;
        float length, travelled;
        Action arrived;
        bool docked, moving;

        public bool IsMoving => moving;
        public bool IsDocked => docked && !moving;

        void Awake()
        {
            boatOffset = boat.transform.localPosition;
            transform.position = seaPoint.position;
            boat.enabled = false;
        }

        /// <summary>The whole route from the sea to the dock (used by checks and gizmos).</summary>
        public IReadOnlyList<Vector3> Route()
        {
            var route = new List<Vector3> { seaPoint.position };
            foreach (var point in waypoints)
                if (point != null) route.Add(point.position);
            route.Add(dockPoint.position);
            return route;
        }

        public void Arrive(Action onDocked) => Move(true, onDocked);

        public void Depart(Action onGone) => Move(false, onGone);

        /// <summary>Finishes any movement immediately (tests and skipping).</summary>
        public void Complete()
        {
            if (!moving) return;
            travelled = length;
            Finish();
        }

        void Move(bool toDock, Action done)
        {
            path.Clear();
            path.AddRange(Route());
            if (!toDock) path.Reverse();
            length = 0;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            travelled = 0;
            docked = toDock;
            moving = true;
            arrived = done;
            boat.enabled = true;
            Place(0);
        }

        void Update()
        {
            boat.transform.localPosition = boatOffset + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * bobHeight);
            if (!moving || Time.deltaTime <= 0) return;
            travelled = Mathf.Min(length, travelled + speed * Time.deltaTime);
            if (travelled >= length) Finish();
            else Place(travelled);
        }

        void Finish()
        {
            Place(length);
            moving = false;
            if (!docked) boat.enabled = false;
            var callback = arrived;
            arrived = null;
            callback?.Invoke();
        }

        void Place(float distance)
        {
            // Ease in and out over the whole trip, but always stay on the route's segments.
            float eased = length > 0 ? Mathf.SmoothStep(0, length, distance / length) : 0;
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (eased <= segment || i == path.Count - 1)
                {
                    transform.position = Vector3.MoveTowards(path[i - 1], path[i], eased);
                    Face(path[i] - path[i - 1]);
                    return;
                }
                eased -= segment;
            }
            transform.position = path[^1];
        }

        void Face(Vector3 direction)
        {
            // The artwork faces lower-left; mirror it when sailing to the right.
            if (Mathf.Abs(direction.x) < .001f) return;
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x > 0 ? -1 : 1);
            transform.localScale = scale;
        }

        void OnDrawGizmosSelected()
        {
            if (seaPoint == null || dockPoint == null) return;
            Gizmos.color = Color.cyan;
            var route = Route();
            for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(route[i - 1], route[i]);
        }
    }
}
