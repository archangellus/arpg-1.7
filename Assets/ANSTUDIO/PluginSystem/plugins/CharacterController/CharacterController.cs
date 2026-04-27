using System;
using UnityEngine;

// The original namespace conflicted with Unity's CharacterController component.
// Renamed the namespace to avoid a type/namespace collision.
namespace PLAYERTWO.ARPGProject.Controllers
{
    /// <summary>
    /// Plug-in that restores behaviour shown in the sample Entity.cs.
    /// It attaches click-dead-zone handling to all entities and provides an
    /// EventBus hook to move entities while respecting the dead-zone.
    /// </summary>
    public class CharacterControllerPlugin : IPlugin
    {

        const string k_MoveTo = "CharacterController.MoveTo";
        const string k_HandleWaypoint = "CharacterController.HandleWaypointMovement";
        const string k_FaceTo = "CharacterController.FaceTo";

        public void Initialize()
        {
            EventBus.Subscribe(k_MoveTo, OnMoveToRequested);
            EventBus.Subscribe(k_HandleWaypoint, OnHandleWaypointMovement);
            EventBus.Subscribe(k_FaceTo, OnFaceToRequested);

            // Ensure every entity in the scene has the extras component so we
            // can store click dead-zone information.
            AttachExtrasToEntities();

            Debug.Log("[CharacterControllerPlugin] Initialized");
        }

        public void Shutdown()
        {
            EventBus.Unsubscribe(k_MoveTo, OnMoveToRequested);
            EventBus.Unsubscribe(k_HandleWaypoint, OnHandleWaypointMovement);
            EventBus.Unsubscribe(k_FaceTo, OnFaceToRequested);

            RemoveExtrasFromEntities();

            Debug.Log("[CharacterControllerPlugin] Shutdown");
        }

        static void AttachExtrasToEntities()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsByType<Entity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!entity.TryGetComponent<CharacterControllerExtras>(out _))
                    entity.gameObject.AddComponent<CharacterControllerExtras>();
            }
        }

        static void RemoveExtrasFromEntities()
        {
            foreach (var extras in UnityEngine.Object.FindObjectsByType<CharacterControllerExtras>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (extras)
                    UnityEngine.Object.Destroy(extras);
            }
        }

        static CharacterControllerExtras GetExtras(Entity entity)
        {
            if (!entity.TryGetComponent<CharacterControllerExtras>(out var extras))
                extras = entity.gameObject.AddComponent<CharacterControllerExtras>();
            return extras;
        }

        static void OnMoveToRequested(object payload)
        {
            if (payload is not object[] args || args.Length < 2)
                return;

            if (args[0] is not Entity entity || args[1] is not Vector3 point)
                return;

            Action<bool> callback = null;
            if (args.Length >= 3 && args[2] is Action<bool> cb)
                callback = cb;

            var extras = GetExtras(entity);
            bool moved = extras.MoveTo(point);
            callback?.Invoke(moved);
        }

        static void OnHandleWaypointMovement(object payload)
        {
            if (payload is not Entity entity)
                return;

            var extras = GetExtras(entity);
            extras.HandleWaypointMovement();
        }

        static void OnFaceToRequested(object payload)
        {
            if (payload is not object[] args || args.Length < 2)
                return;

            if (args[0] is not Entity entity || args[1] is not Vector3 dir)
                return;

            var extras = GetExtras(entity);
            extras.FaceTo(dir);
        }
    }
}