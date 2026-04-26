using System.Collections.Generic;
using ANSTUDIO.PluginSystem;
using PLAYERTWO.ARPGProject;
using UnityEngine;

namespace ANSTUDIO.PluginSystem.Plugins.ProjectileHeightPlugin
{
    public readonly struct ProjectileDetectedEvent
    {
        public readonly Projectile projectile;

        public ProjectileDetectedEvent(Projectile projectile)
        {
            this.projectile = projectile;
        }
    }

    public readonly struct ProjectileTickEvent
    {
        public readonly Projectile projectile;

        public ProjectileTickEvent(Projectile projectile)
        {
            this.projectile = projectile;
        }
    }

    [Plugin("ProjectileHeightPlugin", DisplayName = "ProjectileHeightPlugin", Version = "1.0.0", LoadOrder = 200)]
    public class ProjectileHeightPlugin : IPlugin
    {
        private readonly Dictionary<int, Transform> m_targetsByProjectile = new();

        private ProjectileHeightPluginRuntime m_runtime;

        public void Initialize()
        {
            EventBus.Subscribe<ProjectileDetectedEvent>(OnProjectileDetected);
            EventBus.Subscribe<ProjectileTickEvent>(OnProjectileTick);

            if (m_runtime)
                return;

            var instance = new GameObject(nameof(ProjectileHeightPluginRuntime));
            Object.DontDestroyOnLoad(instance);
            m_runtime = instance.AddComponent<ProjectileHeightPluginRuntime>();
            m_runtime.Initialize(this);
        }

        public void Shutdown()
        {
            EventBus.Unsubscribe<ProjectileDetectedEvent>(OnProjectileDetected);
            EventBus.Unsubscribe<ProjectileTickEvent>(OnProjectileTick);

            m_targetsByProjectile.Clear();

            if (!m_runtime)
                return;

            Object.Destroy(m_runtime.gameObject);
            m_runtime = null;
        }

        private void OnProjectileDetected(ProjectileDetectedEvent args)
        {
            if (!args.projectile)
                return;

            var key = args.projectile.GetInstanceID();

            if (m_targetsByProjectile.ContainsKey(key))
                return;

            var target = FindBestTarget(args.projectile);

            if (target)
                m_targetsByProjectile[key] = target;
        }

        private void OnProjectileTick(ProjectileTickEvent args)
        {
            if (!args.projectile)
                return;

            var key = args.projectile.GetInstanceID();

            if (!m_targetsByProjectile.TryGetValue(key, out var target) || !target)
            {
                m_targetsByProjectile.Remove(key);
                return;
            }

            var direction = target.position - args.projectile.transform.position;

            if (direction.sqrMagnitude > 0f)
                args.projectile.transform.forward = direction.normalized;
        }

        private static Transform FindBestTarget(Projectile projectile)
        {

            var origin = projectile.transform.position;

            return TryFindTargetUnderCursor(projectile, origin, out var cursorTarget)
                ? cursorTarget
                : null;
        }

        private static bool TryFindTargetUnderCursor(Projectile projectile, Vector3 origin, out Transform target)
        {
            target = null;

            var camera = Camera.main;

            if (!camera)
                return false;

            var pointerPosition = EntityInputs.GetPointerPosition();
            var ray = camera.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            var bestDistanceFromRay = float.MaxValue;

            foreach (var hit in hits)

            {
                if (!hit.collider || !hit.collider.IsEntity())
                    continue;

                if (!hit.collider.TryGetComponent(out Entity entity) || entity.isDead)

                    continue;

                var direction = entity.position - origin;
                var distance = direction.magnitude;

                if (distance > projectile.maxDistance)
                    continue;

                if (Vector3.Dot(projectile.transform.forward, direction.normalized) < 0.1f)
                    continue;

                var hitDistance = hit.distance;

                if (hitDistance < bestDistanceFromRay)

                {
                    bestDistanceFromRay = hitDistance;
                    target = entity.transform;

                }
            }

            return target;
        }


        private void UpdateProjectiles()
        {
            var projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);

            foreach (var projectile in projectiles)
            {
                if (!projectile)
                    continue;

                var id = projectile.GetInstanceID();

                if (m_runtime.MarkSeen(id))
                    EventBus.Publish(new ProjectileDetectedEvent(projectile));

                EventBus.Publish(new ProjectileTickEvent(projectile));
            }
        }

        private class ProjectileHeightPluginRuntime : MonoBehaviour
        {
            private readonly HashSet<int> m_seen = new();

            private ProjectileHeightPlugin m_plugin;

            public void Initialize(ProjectileHeightPlugin plugin)
            {
                m_plugin = plugin;
            }

            public bool MarkSeen(int projectileId)
            {
                return m_seen.Add(projectileId);
            }

            private void Update()
            {
                m_plugin?.UpdateProjectiles();
            }
        }
    }
}
