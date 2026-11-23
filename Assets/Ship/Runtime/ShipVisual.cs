using System;
using UnityEngine;

namespace ParadiseTycoon.Ship
{
    /// <summary>Sails the guest boat between open sea and the dock. Uses scaled time, so pause stops it.</summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        [SerializeField] Transform seaPoint;
        [SerializeField] Transform dockPoint;
        [SerializeField] SpriteRenderer boat;
        [SerializeField, Min(.1f)] float travelSeconds = 2.5f;
        [SerializeField, Min(0)] float bobHeight = .04f;

        Vector3 from, to, boatOffset;
        float progress = 1;
        Action arrived;
        bool docked;

        public bool IsMoving => progress < 1;
        public bool IsDocked => docked && !IsMoving;

        void Awake()
        {
            boatOffset = boat.transform.localPosition;
            transform.position = seaPoint.position;
            boat.enabled = false;
        }

        public void Arrive(Action onDocked) => Move(seaPoint.position, dockPoint.position, true, onDocked);

        public void Depart(Action onGone) => Move(dockPoint.position, seaPoint.position, false, onGone);

        /// <summary>Finishes any movement immediately (tests and skipping).</summary>
        public void Complete()
        {
            if (!IsMoving) return;
            progress = 1;
            Finish();
        }

        void Move(Vector3 start, Vector3 end, bool toDock, Action done)
        {
            from = start;
            to = end;
            docked = toDock;
            progress = 0;
            arrived = done;
            boat.enabled = true;
            transform.position = start;
            // The artwork faces lower-left; mirror it when sailing to the right.
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (end.x > start.x ? -1 : 1);
            transform.localScale = scale;
        }

        void Update()
        {
            boat.transform.localPosition = boatOffset + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * bobHeight);
            if (!IsMoving || Time.deltaTime <= 0) return;
            progress = Mathf.Min(1, progress + Time.deltaTime / travelSeconds);
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1) Finish();
        }

        void Finish()
        {
            transform.position = to;
            if (!docked) boat.enabled = false;
            var callback = arrived;
            arrived = null;
            callback?.Invoke();
        }
    }
}
