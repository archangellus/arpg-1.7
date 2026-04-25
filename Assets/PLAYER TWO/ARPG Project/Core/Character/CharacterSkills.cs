using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterSkills
    {
        public Skill[] initialAvailableSkills;
        public Skill[] initialEquippedSkills;
        public int initialSelected = 0;

        public Skill[] currentAvailableSkills =>
            m_skills ? m_skills.skills.ToArray() : initialAvailableSkills;

        public Skill[] currentEquippedSkills =>
            m_skills ? m_skills.GetEquippedSkills() : initialEquippedSkills;

        public int currentSelected => m_skills ? m_skills.index : initialSelected;

        protected EntitySkillManager m_skills;

        public CharacterSkills(Character data)
        {
            initialAvailableSkills = data.availableSkills;
            initialEquippedSkills = data.equippedSkills;
        }

        public CharacterSkills(
            Skill[] initialAvailableSkills,
            Skill[] initialEquippedSkills,
            int initialSelected
        )
        {
            this.initialAvailableSkills = initialAvailableSkills;
            this.initialEquippedSkills = initialEquippedSkills;
            this.initialSelected = initialSelected;
        }

        /// <summary>
        /// Initializes a given Entity Skill Manager.
        /// </summary>
        /// <param name="skills">The Entity Skill Manager you want to initialize.</param>
        public virtual void InitializeSkills(EntitySkillManager skills)
        {
            m_skills = skills;
            m_skills.skills = new List<Skill>(initialAvailableSkills);
            m_skills.equipped = new List<SkillInstance>();

            for (int i = 0; i < initialEquippedSkills.Length; i++)
            {
                var data = initialEquippedSkills[i];
                var instance = data ? m_skills.GetSkillInstance(data) : null;
                m_skills.equipped.Add(instance);
            }

            m_skills.ChangeTo(initialSelected);
        }

        public static CharacterSkills CreateFromSerializer(SkillsSerializer serializer)
        {
            var availableSkills = new Skill[serializer.availableSkills.Length];
            var equippedSkills = new Skill[serializer.equippedSkills.Length];

            for (int i = 0; i < availableSkills.Length; i++)
            {
                availableSkills[i] = GameDatabase.instance.FindElementById<Skill>(
                    serializer.availableSkills[i].skillId
                );
            }

            for (int i = 0; i < equippedSkills.Length; i++)
            {
                equippedSkills[i] = GameDatabase.instance.FindElementById<Skill>(
                    serializer.equippedSkills[i].skillId
                );
            }

            return new CharacterSkills(availableSkills, equippedSkills, serializer.selected);
        }
    }
}
