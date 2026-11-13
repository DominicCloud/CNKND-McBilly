using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private SoundDefinition walkSound;
    [SerializeField] private SoundDefinition dashSound;
    [SerializeField] private SoundDefinition jumpSound;

    void OnEnable() => player.OnDashed += HandleDash;
    void OnDisable() => player.OnDashed -= HandleDash;

    void HandleDash() => AudioManager.Instance.PlaySFX(dashSound);
}
