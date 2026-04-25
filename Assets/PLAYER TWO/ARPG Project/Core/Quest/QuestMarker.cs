using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(QuestGiver))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Marker")]
    public class QuestMarker : MonoBehaviour
    {
        public Vector3 worldMarkerOffset = new(0, 1, 0);
        public Vector3 worldRotationOffset = Vector3.zero;

        protected GameObject m_availableQuestMarker;
        protected GameObject m_inProgressQuestMarker;
        protected GameObject m_availableQuestIcon;
        protected GameObject m_inProgressQuestIcon;

        protected QuestGiver m_giver;

        protected virtual void Awake()
        {
            Initialize();
            InstantiateMarkers();
        }

        protected virtual void Initialize()
        {
            m_giver = GetComponent<QuestGiver>();
            m_giver.onStateChange.AddListener(OnStateChange);
        }

        protected virtual void InstantiateMarkers()
        {
            QuestMarkerManager.instance.InstantiateMarkers(
                transform,
                worldMarkerOffset,
                worldRotationOffset,
                ref m_availableQuestMarker,
                ref m_inProgressQuestMarker,
                ref m_availableQuestIcon,
                ref m_inProgressQuestIcon
            );
        }

        protected virtual void DisableAll()
        {
            SetActive(
                false,
                m_availableQuestMarker,
                m_availableQuestIcon,
                m_inProgressQuestMarker,
                m_inProgressQuestIcon
            );
        }

        protected virtual void SetActive(bool active, params GameObject[] gameObjects)
        {
            foreach (var obj in gameObjects)
                SetActive(obj, active);
        }

        protected virtual void SetActive(GameObject obj, bool active)
        {
            if (obj)
                obj.SetActive(active);
        }

        protected virtual void OnStateChange(QuestGiver.State state)
        {
            DisableAll();

            switch (state)
            {
                default:
                case QuestGiver.State.None:
                    break;
                case QuestGiver.State.QuestAvailable:
                    SetActive(true, m_availableQuestMarker, m_availableQuestIcon);
                    break;
                case QuestGiver.State.QuestInProgress:
                    SetActive(true, m_inProgressQuestMarker, m_inProgressQuestIcon);
                    break;
            }
        }
    }
}
