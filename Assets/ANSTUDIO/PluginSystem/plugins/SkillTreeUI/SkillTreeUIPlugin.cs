using UnityEngine;

namespace PLAYERTWO.ARPGProject.SkillTreeUI
{
    [Plugin("skill-tree-ui", DisplayName = "Skill Tree UI", Version = "1.0.0", LoadOrder = 250)]
    public class SkillTreeUIPlugin : IPlugin
    {
        public void Initialize()
        {
            Debug.Log("[SkillTreeUI] Plugin initialized. Add SkillTreeUIController to a UI canvas to configure the tree.");
        }

        public void Shutdown()
        {
            Debug.Log("[SkillTreeUI] Plugin shutdown.");
        }
    }
}
