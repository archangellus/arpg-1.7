using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Marker Manager")]
    public class QuestMarkerManager : Singleton<QuestMarkerManager>
    {
        [Header("World Markers")]
        [Tooltip(
            "The Game Object that represents the marker when a Quest Giver has a Quest available."
        )]
        public GameObject availableQuestMarker;

        [Tooltip(
            "The Game Object that represents the marker when a Quest Giver has a Quest in progress."
        )]
        public GameObject inProgressQuestMarker;

        [Header("Map Markers")]
        [Tooltip("Reference the type of Map Marker to use for available quests.")]
        public MapMarkerType availableQuestType;

        [Tooltip("Reference the type of Map Marker to use for in-progress quests.")]
        public MapMarkerType inProgressQuestType;

        protected MapMarker m_mapMarker;

        public virtual void InstantiateMarkers(
            Transform target,
            Vector3 worldOffset,
            Vector3 worldRotationOffset,
            ref GameObject availableMarker,
            ref GameObject inProgressMarker,
            ref GameObject availableIcon,
            ref GameObject inProgressIcon
        )
        {
            InstantiateObject(
                target,
                worldOffset,
                worldRotationOffset,
                ref availableMarker,
                availableQuestMarker
            );
            InstantiateObject(
                target,
                worldOffset,
                worldRotationOffset,
                ref inProgressMarker,
                inProgressQuestMarker
            );
            InstantiateMapMarker(target, ref availableIcon, availableQuestType);
            InstantiateMapMarker(target, ref inProgressIcon, inProgressQuestType);
        }

        public virtual void InstantiateMapMarker(
            Transform target,
            ref GameObject instance,
            MapMarkerType type
        )
        {
            if (instance || !type)
                return;

            instance = new GameObject("Map Marker");
            instance.transform.SetParent(target, false);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_mapMarker = instance.AddComponent<MapMarker>();
            m_mapMarker.type = type;
        }

        protected virtual void InstantiateObject(
            Transform target,
            Vector3 worldOffset,
            Vector3 worldRotationOffset,
            ref GameObject instance,
            Object go
        )
        {
            if (instance || !go)
                return;

            instance = (GameObject)Instantiate(go, Vector3.zero, Quaternion.identity);
            instance.transform.SetParent(target, false);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localPosition += worldOffset;
            instance.transform.localRotation = Quaternion.Euler(worldRotationOffset);
        }
    }
}
