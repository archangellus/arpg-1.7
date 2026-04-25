using UnityEngine.InputSystem;

namespace PLAYERTWO.ARPGProject
{
    public partial class EntityInputs
    {
        protected InputAction m_setDestinationAction;
        protected InputAction m_attackModeAction;
        protected InputAction m_skillAction;
        protected InputAction m_consumeItem0;
        protected InputAction m_consumeItem1;
        protected InputAction m_consumeItem2;
        protected InputAction m_consumeItem3;
        protected InputAction m_selectSkill0;
        protected InputAction m_selectSkill1;
        protected InputAction m_selectSkill2;
        protected InputAction m_selectSkill3;
        protected InputAction m_directionalMovement;
        protected InputAction m_attackAction;
        protected InputAction m_interactAction;

        protected virtual void InitializeActions()
        {
            m_setDestinationAction = actions["Set Destination"];
            m_skillAction = actions["Skill"];
            m_attackModeAction = actions["Attack Mode"];
            m_consumeItem0 = actions["Consume Item 0"];
            m_consumeItem1 = actions["Consume Item 1"];
            m_consumeItem2 = actions["Consume Item 2"];
            m_consumeItem3 = actions["Consume Item 3"];
            m_selectSkill0 = actions["Select Skill 0"];
            m_selectSkill1 = actions["Select Skill 1"];
            m_selectSkill2 = actions["Select Skill 2"];
            m_selectSkill3 = actions["Select Skill 3"];
            m_directionalMovement = actions["Directional Movement"];
            m_attackAction = actions["Attack"];
            m_interactAction = actions["Interact"];
        }

        protected virtual void InitializeActionsCallbacks()
        {
            m_setDestinationAction.performed += OnSetDestination;
            m_setDestinationAction.canceled += OnSetDestinationCancelled;
            m_directionalMovement.performed += OnDirectionalMovement;
            m_skillAction.performed += OnSkill;
            m_skillAction.canceled += OnSkillCancelled;
            m_attackModeAction.performed += OnAttackMode;
            m_attackModeAction.canceled += OnAttackModeCancelled;
            m_consumeItem0.performed += OnConsumeItem0;
            m_consumeItem1.performed += OnConsumeItem1;
            m_consumeItem2.performed += OnConsumeItem2;
            m_consumeItem3.performed += OnConsumeItem3;
            m_selectSkill0.performed += OnSelectSkill0;
            m_selectSkill1.performed += OnSelectSkill1;
            m_selectSkill2.performed += OnSelectSkill2;
            m_selectSkill3.performed += OnSelectSkill3;
            m_attackAction.performed += OnAttack;
            m_attackAction.canceled += OnAttackCancelled;
            m_interactAction.performed += OnInteract;
        }

        protected virtual void FinalizeActionCallbacks()
        {
            if (!enabled)
                return;

            m_setDestinationAction.performed -= OnSetDestination;
            m_setDestinationAction.canceled -= OnSetDestinationCancelled;
            m_skillAction.performed -= OnSkill;
            m_skillAction.canceled -= OnSkillCancelled;
            m_attackModeAction.performed -= OnAttackMode;
            m_attackModeAction.canceled -= OnAttackModeCancelled;
            m_consumeItem0.performed -= OnConsumeItem0;
            m_consumeItem1.performed -= OnConsumeItem1;
            m_consumeItem2.performed -= OnConsumeItem2;
            m_consumeItem3.performed -= OnConsumeItem3;
            m_selectSkill0.performed -= OnSelectSkill0;
            m_selectSkill1.performed -= OnSelectSkill1;
            m_selectSkill2.performed -= OnSelectSkill2;
            m_selectSkill3.performed -= OnSelectSkill3;
            m_attackAction.performed -= OnAttack;
            m_attackAction.canceled -= OnAttackCancelled;
            m_interactAction.performed -= OnInteract;
        }
    }
}
