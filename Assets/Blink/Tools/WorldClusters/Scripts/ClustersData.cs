using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace BLINK.WorldClusters
{
    [System.Serializable]
    public class ClusterDATA
    {
        public string clusterGroupName = "New Cluster Group";
        public bool show, showEntries = true, showOverrides, showGizmos;
        public List<ClusterEntry> Entries = new List<ClusterEntry>();
        public List<ClusterOverride> overrides = new List<ClusterOverride>();
    }

    public enum ClUSTER_ENTRY_TYPE
    {
        GameObject,
        Light,
        Renderer,
        ParticleSystem,
        MaterialChange,
        TagChange,
        LayerChange,
        UnityEvent,
        SoundPlay
    }

    public enum ClUSTER_SOUND_ACTION_TYPE
    {
        Play,
        PlayOneShot,
        Stop,
        Pause,
        UnPause
    }

    [System.Serializable]
    public class ClusterEntry
    {
        public ClUSTER_ENTRY_TYPE Type;
        public List<GameObject> GameObjectList = new List<GameObject>();
        public List<Light> LightList = new List<Light>();
        public List<Renderer> RendererList = new List<Renderer>();
        public List<ParticleSystem> ParticleSystemList = new List<ParticleSystem>();
        public List<AudioSource> AudioSourceList = new List<AudioSource>();
        public ClusterAction action = new ClusterAction();
        public bool hideRendererNotDisable, lightTransition;
        public Coroutine LightTransitionCoroutine;
        public float lightTransitionTime, enableLightIntensityAmount = 1, disableLightIntensityAmount = 0;

        public bool soundOverrideAudioSourceSettings = true;
        public bool soundOverrideClip = true;
        public AudioClip soundClip;
        public AudioMixerGroup soundOutputMixerGroup;
        public bool soundMute;
        public bool soundLoop;
        public bool soundPlayOnAwake;
        public bool soundBypassEffects;
        public bool soundBypassListenerEffects;
        public bool soundBypassReverbZones;
        public bool soundIgnoreListenerPause;
        public bool soundIgnoreListenerVolume;
        public float soundVolume = 1f;
        public float soundPitch = 1f;
        public int soundPriority = 128;
        public float soundStereoPan;
        public float soundSpatialBlend;
        public float soundReverbZoneMix = 1f;
        public float soundDopplerLevel = 1f;
        public float soundSpread;
        public float soundMinDistance = 1f;
        public float soundMaxDistance = 500f;
        public AudioRolloffMode soundRolloffMode = AudioRolloffMode.Logarithmic;

        public bool show, layerAppliedOnChild;
    }

    public enum ClUSTER_ACTION_EVENT_TYPE
    {
        Enter,
        Exit,
    }

    public enum ClUSTER_ACTION_TYPE
    {
        Disable,
        Enable
    }

    [System.Serializable]
    public class ClusterAction
    {
        public ClUSTER_ACTION_EVENT_TYPE ActionEventType1 = ClUSTER_ACTION_EVENT_TYPE.Enter, ActionEventType2 = ClUSTER_ACTION_EVENT_TYPE.Exit;
        public ClUSTER_ACTION_TYPE enterActionType = ClUSTER_ACTION_TYPE.Disable, exitActionType = ClUSTER_ACTION_TYPE.Enable;
        public ClUSTER_SOUND_ACTION_TYPE enterSoundAction = ClUSTER_SOUND_ACTION_TYPE.Play, exitSoundAction = ClUSTER_SOUND_ACTION_TYPE.Stop;
        public float enterDelay, exitDelay;
        public Material enterMaterial, exitMaterial;
        public string enterTag, exitTag;
        public int enterLayer, exitLayer;
        public ClusterConditions conditions;

        public UnityEvent enterEvents;
        public UnityEvent exitEvents;
    }

    public enum ClUSTER_COLLISION_CONDITION_TYPE
    {
        GameObjectName,
        LayerMask,
        Tag
    }


    public enum ClUSTER_CONDITION_REQUIREMENT_TYPE
    {
        Optional,
        Required
    }

    [System.Serializable]
    public class CollisionCondition
    {
        public ClUSTER_COLLISION_CONDITION_TYPE type;
        public ClUSTER_CONDITION_REQUIREMENT_TYPE requirementType;
        public string gameObjectName;
        public string tagName;
        public int layer;
    }

    public enum ClUSTER_OVERRIDE_TYPE
    {
        CusterGroup
    }
    [System.Serializable]
    public class ClusterOverride
    {
        public ClUSTER_OVERRIDE_TYPE type;
        public int clusterGroupIndex;
    }


    [System.Serializable]
    public class ActiveCluster
    {
        public Cluster cluster;
        public int clusterGroupIndex = -1;
        public bool isOverriden;
        public int overridenByClusterGroupIndex = -1;
        public ClusterCollider lastClusterCollider;
    }
}
