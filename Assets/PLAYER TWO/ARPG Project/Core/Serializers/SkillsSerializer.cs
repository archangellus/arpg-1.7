using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class SkillsSerializer
    {
        [System.Serializable]
        public class Skill
        {
            public int skillId = -1;
        }

        public Skill[] availableSkills;
        public Skill[] equippedSkills;

        public int selected;

        public SkillsSerializer(CharacterSkills skills)
        {
            InitializeArray(ref availableSkills, skills.currentAvailableSkills);
            InitializeArray(ref equippedSkills, skills.currentEquippedSkills);
            selected = skills.currentSelected;
        }

        protected virtual void InitializeArray(ref Skill[] array, ARPGProject.Skill[] other)
        {
            array = new Skill[other.Length];

            for (int i = 0; i < array.Length; i++)
            {
                if (other[i] == null)
                {
                    array[i] = new Skill();
                    continue;
                }

                array[i] = new Skill()
                {
                    skillId = GameDatabase.instance
                        .GetElementId<ARPGProject.Skill>(other[i])
                };
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static SkillsSerializer FromJson(string json) =>
            JsonUtility.FromJson<SkillsSerializer>(json);
    }
}
