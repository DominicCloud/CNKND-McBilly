using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "NewSound", menuName = "Audio/Sound Definition")]
public class SoundDefinition : ScriptableObject
{
    [Header("Clip")]
    [SerializeField] private AudioClip clip;
    
    [Header("Playback")]
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [Range(0.5f, 2f)] [SerializeField] private float minPitch = 0.95f;
    [Range(0.5f, 2f)] [SerializeField] private float maxPitch = 1.05f;
    [SerializeField] private bool loop = false;
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 0f;
    
    [Header("Mixer")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    
    public AudioClip Clip => clip;
    public float Volume => volume;
    public bool Loop => loop;
    public float SpatialBlend => spatialBlend;
    public AudioMixerGroup MixerGroup => mixerGroup;
    public float GetPitch() => Random.Range(minPitch, maxPitch);
    
}
