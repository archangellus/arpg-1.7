using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Animation Event Listener")]
    public class EntityAnimationEventListener : MonoBehaviour
    {
        public UnityEvent onAttack;

        public virtual void OnAttack() => onAttack.Invoke();
    }
}
