using UnityEngine;

namespace PLAYERTWO.ARPGProject.SamplePlugin
{
    /// <summary>
    /// Example plugin that logs attack events.
    /// </summary>
    [Plugin("sample-attack-logger", DisplayName = "Sample Attack Logger", Version = "1.0.0", LoadOrder = 200)]
    public class AttackLogger : IPlugin
    {
        public void Initialize()
        {
            EventBus.Attack += OnAttack;
            Debug.Log("[SamplePlugin] AttackLogger initialized");
        }

        public void Shutdown()
        {
            EventBus.Attack -= OnAttack;
            Debug.Log("[SamplePlugin] AttackLogger shutdown");
        }

        private void OnAttack(Entity attacker, Entity defender)
        {
            Debug.Log($"[SamplePlugin] {attacker.name} attacked {defender.name}");
        }
    }
}