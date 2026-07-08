using UnityEngine;

/*
  Totem 04 — O Ritmo das Escolhas
  AudioPersistente.cs

  Toca um som que precisa continuar mesmo depois de trocar de cena
  (ex: o som de "iniciar o jogo" apertado na home, que deve continuar
  tocando durante o fade até a cena do jogo e a contagem regressiva).

  Não precisa arrastar em nenhum GameObject — se cria sozinho na primeira
  vez que alguma parte do código chamar "AudioPersistente.Instance".

  Uso:
    AudioPersistente.Instance.Tocar(meuClip);
    AudioPersistente.Instance.Parar();
*/
public class AudioPersistente : MonoBehaviour
{
    private static AudioPersistente instancia;

    public static AudioPersistente Instance
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("AudioPersistente");
                instancia = go.AddComponent<AudioPersistente>();
            }
            return instancia;
        }
    }

    private AudioSource audioSource;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void Tocar(AudioClip clip)
    {
        if (clip == null) return;

        // Se já estiver tocando esse mesmo clipe, não reinicia — deixa terminar inteiro
        if (audioSource.isPlaying && audioSource.clip == clip)
            return;

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.Play();
    }

    public void Parar()
    {
        audioSource.Stop();
    }
}