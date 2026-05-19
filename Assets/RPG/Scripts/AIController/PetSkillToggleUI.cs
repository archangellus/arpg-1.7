using UnityEngine;

public class PetSkillToggleUI : MonoBehaviour
{
    [Tooltip("UI shown when the pet toggle skill is OFF (pet not active).")]
    public GameObject inactiveUi;

    [Tooltip("UI shown when the pet toggle skill is ON (pet active).")]
    public GameObject activeUi;

    public void SetPetActiveState(bool active)
    {
        if (activeUi != null)
            activeUi.SetActive(active);

        if (inactiveUi != null)
            inactiveUi.SetActive(!active);
    }

    private void OnEnable()
    {
        SetPetActiveState(false);
    }
}
