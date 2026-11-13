using System;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // variables
    
    [Header("Singleton Instance")]
    public static AudioManager Instance;

    [Header("Main Music Clips")] [SerializeField]
    private AudioClip[] backgroundMusic;
    
    [Header("Audio Sources")] 
    [SerializeField]
    private AudioSource musicSource;
    [SerializeField]
    private AudioSource sfxSource;
    
    // Use singleton pattern to ensure only one instance of AudioManager exists
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        PlayBackgroundMusic();
    }
    
    public void PlayBackgroundMusic()
    {
        if (backgroundMusic.Length > 0)
        {
            if (musicSource != null)
            {
                musicSource.clip = backgroundMusic[UnityEngine.Random.Range(0, backgroundMusic.Length)];
                musicSource.loop = true;
                musicSource.Play();
            }
        }
    }
    

    public void PlaySFX(SoundDefinition clip)
    {
        if (clip == null || clip.Clip == null) return;
            
        sfxSource.outputAudioMixerGroup = clip.MixerGroup;
        sfxSource.pitch = clip.GetPitch();
        sfxSource.PlayOneShot(clip.Clip, clip.Volume);
    }
    
}
