using System;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [Serializable]
    public class PluginColorProfile
    {
        public Color keyword;
        public Color comment;
        public Color str;
        public Color method;
        public Color type;
        public Color number;
        public Color iface;
        public Color member;
        public Color dlg;   // delegate
    }
}
