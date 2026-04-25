using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Record Transform Hierarchy")]
    public class RecordTransformHierarchy : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Recorder Settings")]
        public AnimationClip clip;

        [Tooltip("The frame rate of the animation clip")]
        public int frameRate = 60;

        protected GameObjectRecorder m_recorder;

        protected void Start()
        {
            m_recorder = new GameObjectRecorder(gameObject);
            m_recorder.BindComponentsOfType<Transform>(gameObject, true);
        }

        protected void LateUpdate()
        {
            if (clip == null)
                return;

            m_recorder.TakeSnapshot(Time.deltaTime);
        }

        protected void OnDisable()
        {
            if (clip == null)
                return;

            if (m_recorder.isRecording)
            {
                m_recorder.SaveToClip(clip, frameRate);
            }
        }
#endif
    }
}
