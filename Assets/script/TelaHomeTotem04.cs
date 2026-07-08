using UnityEngine;
using UnityEngine.SceneManagement;

/*
  Totem 04 — O Ritmo das Escolhas
  TelaHomeTotem04.cs

  Coloca esse script em um GameObject vazio na cena da tela home (ex: "HomeController").
  Quando o jogador aperta o botão VERDE (fisico, via Teensy) ou a tecla de teste,
  toca o som de início (por inteiro, mesmo que a troca de cena aconteça no meio)
  e carrega a cena do jogo.

  Se o nome da sua cena de jogo nao for "telajogo", so trocar o campo
  "Cena Do Jogo" no Inspector.
*/
public class TelaHomeTotem04 : MonoBehaviour
{
    [Header("Cena que o jogo abre ao apertar o botão verde")]
    [SerializeField] private string cenaDoJogo = "telajogo";

    [Header("Duração do fade (segundos) ao iniciar o jogo")]
    [SerializeField] private float duracaoFade = 0.6f;

    [Header("Tecla de teste (funciona junto com o botão físico, útil sem o Teensy plugado)")]
    [SerializeField] private KeyCode teclaDeTeste = KeyCode.Alpha1;

    [Header("Integração com Teensy (botão físico)")]
    [SerializeField] private bool usarBotoesFisicos = true;

    [Header("Som ao apertar o verde pra começar")]
    [Tooltip("Arraste aqui o áudio que toca quando o jogador aperta o botão verde. Toca até o fim, mesmo com a troca de cena.")]
    [SerializeField] private AudioClip somIniciar;

    private bool jaIniciou;

    private void Start()
    {
        if (usarBotoesFisicos)
        {
            if (TeensyButtonManager.Instance != null)
            {
                TeensyButtonManager.Instance.OnButtonDown += TratarBotaoFisico;
            }
            else
            {
                Debug.LogWarning("TelaHomeTotem04: 'Usar Botoes Fisicos' está ligado, mas não achei um TeensyButtonManager na cena.");
            }
        }
    }

    private void OnDestroy()
    {
        if (usarBotoesFisicos && TeensyButtonManager.Instance != null)
        {
            TeensyButtonManager.Instance.OnButtonDown -= TratarBotaoFisico;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaDeTeste))
        {
            IniciarJogo();
        }
    }

    private void TratarBotaoFisico(CorBotao cor)
    {
        if (cor == CorBotao.Verde)
        {
            IniciarJogo();
        }
    }

    private void IniciarJogo()
    {
        if (jaIniciou) return; // evita iniciar duas vezes se apertar rápido demais
        jaIniciou = true;

        if (somIniciar != null)
            TocarSomCompleto(somIniciar);

        FadeController.Instance.TrocarCena(cenaDoJogo, duracaoFade, duracaoFade);
    }

    // Toca o clipe num GameObject próprio que sobrevive à troca de cena e se destrói
    // sozinho assim que o som termina — assim o áudio nunca é cortado no meio.
    private void TocarSomCompleto(AudioClip clip)
    {
        var go = new GameObject("SomInicioJogo");
        DontDestroyOnLoad(go);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.Play();

        Destroy(go, clip.length);
    }
}