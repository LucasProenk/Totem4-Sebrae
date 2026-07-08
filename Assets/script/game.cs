using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    private enum Cor { Verde, Vermelho, Amarelo, Azul }

    [Header("Cena para onde vai ao errar (a frase final volta pra home sozinha depois de um tempo)")]
    [SerializeField] private string cenaFraseFinal = "frasefinal";

    [Header("Cores de cada categoria (iguais às do fundo)")]
    [SerializeField] private Color corVerde = new Color(0.30f, 1.00f, 0.35f);    // Relacionamentos
    [SerializeField] private Color corVermelho = new Color(1.00f, 0.05f, 0.05f); // Trabalho e Realização
    [SerializeField] private Color corAmarelo = new Color(1.00f, 0.82f, 0.10f);  // Aprendizado
    [SerializeField] private Color corAzul = new Color(0.10f, 0.65f, 1.00f);     // Descanso

    [Header("Teclas de teste (funcionam junto com os botões físicos, útil pra testar sem o Teensy plugado)")]
    [SerializeField] private KeyCode teclaVerde = KeyCode.Alpha1;
    [SerializeField] private KeyCode teclaVermelho = KeyCode.Alpha2;
    [SerializeField] private KeyCode teclaAmarelo = KeyCode.Alpha3;
    [SerializeField] private KeyCode teclaAzul = KeyCode.Alpha4;

    [Header("Integração com Teensy (botões físicos)")]
    [Tooltip("Liga a escuta dos eventos do TeensyButtonManager e o controle dos LEDs físicos durante a exibição da sequência.")]
    [SerializeField] private bool usarBotoesFisicos = true;

    [Header("Som quando a contagem chega em 3")]
    [Tooltip("Arraste aqui o áudio que toca quando o número da contagem chega em 3")]
    [SerializeField] private AudioClip somInicio;

    [Header("Som de cada cor (toca só durante a exibição da sequência)")]
    [Tooltip("Arraste aqui o áudio que toca quando o botão VERDE acende na sequência")]
    [SerializeField] private AudioClip somVerde;
    [Tooltip("Arraste aqui o áudio que toca quando o botão VERMELHO acende na sequência")]
    [SerializeField] private AudioClip somVermelho;
    [Tooltip("Arraste aqui o áudio que toca quando o botão AMARELO acende na sequência")]
    [SerializeField] private AudioClip somAmarelo;
    [Tooltip("Arraste aqui o áudio que toca quando o botão AZUL acende na sequência")]
    [SerializeField] private AudioClip somAzul;
    [Tooltip("Volume dos sons de cor (1 = volume normal do clipe, 0.75 = 25% mais baixo)")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeSomBotoes = 0.75f;
    [Tooltip("Arraste aqui o áudio que toca quando o jogador erra (ou fica parado sem clicar)")]
    [SerializeField] private AudioClip somErro;

    [Header("Tempos (segundos)")]
    [SerializeField] private int duracaoContagem = 5;
    [SerializeField] private float tempoMostrandoCor = 0.8f;
    [SerializeField] private float tempoEntreCores = 0.35f;
    [SerializeField] private float pausaEntreRodadas = 0.8f;
    [Tooltip("Se o jogador ficar esse tempo sem apertar nenhum botão, conta como erro (igual apertar o botão errado).")]
    [SerializeField] private float tempoLimiteInput = 5f;

    [Header("Velocidade progressiva (fica mais rápido a cada acerto)")]
    [SerializeField] private float reducaoPorAcerto = 0.02f;
    [SerializeField] private float tempoMinimoMostrandoCor = 0.3f;
    [SerializeField] private float tempoMinimoEntreCores = 0.15f;

    [Header("Fade ao errar")]
    [SerializeField] private float duracaoFade = 0.6f;

    [Header("Pisca dos botões ao errar")]
    [SerializeField] private int vezesPiscarErro = 4;
    [Tooltip("Tempo (segundos) que cada botão fica aceso ou apagado em cada pisca — não é o ciclo completo.")]
    [SerializeField] private float duracaoPiscaErro = 0.2f;

    [Header("Placar")]
    [Tooltip("Largura máxima que o texto da bola pode ocupar (unidades de mundo) — se \"VAI!\" ou algo maior que um dígito não couber nesse espaço, ele encolhe automaticamente pra caber dentro da bola")]
    [SerializeField] private float larguraMaximaPlacar = 1.1f;

    [Header("Referências de cena")]
    [Tooltip("Arraste aqui o objeto de texto (TextMesh) que mostra a palavra da categoria embaixo. Assim dá pra posicionar ele arrastando no Scene view, do jeito que quiser.")]
    [SerializeField] private TextMesh txtPalavra;
    [Tooltip("Arraste aqui o objeto de texto (TextMesh) que mostra o número/placar dentro da bola. Controla fonte, tamanho e posição direto no Inspector/Scene view, igual o Txt Palavra.")]
    [SerializeField] private TextMesh txtPlacar;

    private static readonly string[] PalavrasVermelho = { "TRABALHO", "REALIZAÇÃO" };

    private Dictionary<KeyCode, Cor> teclas;
    private Dictionary<Cor, (string nome, Color cor)> categorias;
    private Dictionary<Cor, AudioClip> sons;
    private int indiceVermelho;

    private AudioSource audioSource;

    private readonly List<Cor> sequencia = new List<Cor>();
    private int acertos;
    private int indiceEsperado;
    private bool aceitandoInput;
    private float tempoDesdeUltimoInput;

    private void Start()
    {
        if (txtPalavra == null)
            Debug.LogError("game: arraste o objeto de texto da palavra pro campo 'Txt Palavra' no Inspector.");
        if (txtPlacar == null)
            Debug.LogError("game: arraste o objeto de texto do placar pro campo 'Txt Placar' no Inspector.");

        teclas = new Dictionary<KeyCode, Cor>
        {
            { teclaVerde, Cor.Verde },
            { teclaVermelho, Cor.Vermelho },
            { teclaAmarelo, Cor.Amarelo },
            { teclaAzul, Cor.Azul },
        };

        categorias = new Dictionary<Cor, (string, Color)>
        {
            { Cor.Verde, ("RELACIONAMENTOS", corVerde) },
            { Cor.Vermelho, (null, corVermelho) }, // palavra revezada em ProximaPalavra, não usa nome fixo
            { Cor.Amarelo, ("APRENDIZADO", corAmarelo) },
            { Cor.Azul, ("DESCANSO", corAzul) },
        };

        sons = new Dictionary<Cor, AudioClip>
        {
            { Cor.Verde, somVerde },
            { Cor.Vermelho, somVermelho },
            { Cor.Amarelo, somAmarelo },
            { Cor.Azul, somAzul },
        };

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (usarBotoesFisicos)
        {
            if (TeensyButtonManager.Instance != null)
            {
                TeensyButtonManager.Instance.OnButtonDown += TratarBotaoFisico;
                TeensyButtonManager.Instance.ApagarTodosLeds(); // garante que nada ficou aceso de uma partida anterior
            }
            else
            {
                Debug.LogWarning("game: 'Usar Botoes Fisicos' está ligado, mas não achei um TeensyButtonManager na cena. Confirme se o GameObject com esse script existe e está ativo.");
            }
        }

        StartCoroutine(RodarJogo());
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
        if (!aceitandoInput)
            return;

        foreach (var par in teclas)
        {
            if (Input.GetKeyDown(par.Key))
            {
                ReceberInput(par.Value);
                break;
            }
        }
    }

    // Chamado toda vez que o Teensy avisa que um botão físico foi pressionado.
    // O LED já acendeu sozinho no hardware nesse exato momento — aqui só validamos a jogada.
    private void TratarBotaoFisico(CorBotao corBotao)
    {
        if (!aceitandoInput)
            return;

        Cor? cor = ConverterCorBotao(corBotao);
        if (cor.HasValue)
            ReceberInput(cor.Value);
    }

    private Cor? ConverterCorBotao(CorBotao corBotao)
    {
        switch (corBotao)
        {
            case CorBotao.Verde: return Cor.Verde;
            case CorBotao.Vermelho: return Cor.Vermelho;
            case CorBotao.Amarelo: return Cor.Amarelo;
            case CorBotao.Azul: return Cor.Azul;
            default: return null;
        }
    }

    // Caminho inverso do ConverterCorBotao, usado pra acender o LED certo durante a exibição da sequência
    private CorBotao ConverterCorParaBotao(Cor cor)
    {
        switch (cor)
        {
            case Cor.Verde: return CorBotao.Verde;
            case Cor.Vermelho: return CorBotao.Vermelho;
            case Cor.Amarelo: return CorBotao.Amarelo;
            case Cor.Azul: return CorBotao.Azul;
            default: return CorBotao.Verde;
        }
    }

    private IEnumerator RodarJogo()
    {
        yield return StartCoroutine(ContagemRegressiva());

        sequencia.Clear();
        acertos = 0;
        AtualizarPlacar();

        while (true)
        {
            sequencia.Add(ProximaCorAleatoria());

            yield return StartCoroutine(MostrarSequencia());
            yield return StartCoroutine(EsperarInputDoJogador());

            if (!aceitandoInput && indiceEsperado < sequencia.Count)
                yield break; // errou: EsperarInputDoJogador já disparou o fade

            yield return new WaitForSeconds(pausaEntreRodadas);
        }
    }

    private IEnumerator ContagemRegressiva()
    {
        txtPalavra.text = "SIGA A SEQUÊNCIA DE CORES";
        txtPalavra.color = Color.white;

        bool controlaLeds = usarBotoesFisicos && TeensyButtonManager.Instance != null;

        if (!controlaLeds)
        {
            Debug.LogWarning("game: não vou controlar os LEDs na contagem porque usarBotoesFisicos=" + usarBotoesFisicos +
                " e TeensyButtonManager.Instance=" + (TeensyButtonManager.Instance == null ? "null" : "existe"));
        }

        // Acende os 4 botões durante toda a contagem, como um "se preparar"
        if (controlaLeds)
        {
            TeensyButtonManager.Instance.SetLed(CorBotao.Verde, true);
            TeensyButtonManager.Instance.SetLed(CorBotao.Vermelho, true);
            TeensyButtonManager.Instance.SetLed(CorBotao.Amarelo, true);
            TeensyButtonManager.Instance.SetLed(CorBotao.Azul, true);
        }

        for (int i = duracaoContagem; i >= 1; i--)
        {
            DefinirTextoPlacar(i.ToString());

            // Toca o som de início assim que a contagem chega em 3
            if (i == 3 && somInicio != null)
                audioSource.PlayOneShot(somInicio);

            if (controlaLeds && i == 1)
                TeensyButtonManager.Instance.ApagarTodosLeds();

            yield return new WaitForSeconds(1f);
        }
        DefinirTextoPlacar("VAI!");
        yield return new WaitForSeconds(0.5f);

        // Segurança: garante que está tudo apagado antes da primeira sequência,
        // mesmo se "duracaoContagem" for menor que 4
        if (controlaLeds)
            TeensyButtonManager.Instance.ApagarTodosLeds();

        txtPalavra.text = "";
    }

    private IEnumerator MostrarSequencia()
    {
        aceitandoInput = false;

        bool controlaLeds = usarBotoesFisicos && TeensyButtonManager.Instance != null;

        float reducao = acertos * reducaoPorAcerto;
        float tempoMostrar = Mathf.Max(tempoMinimoMostrandoCor, tempoMostrandoCor - reducao);
        float tempoEntre = Mathf.Max(tempoMinimoEntreCores, tempoEntreCores - reducao);

        foreach (var cor in sequencia)
        {
            var (nome, corCategoria) = categorias[cor];
            txtPalavra.text = cor == Cor.Vermelho ? ProximaPalavraVermelho() : nome;
            txtPalavra.color = corCategoria;

            if (controlaLeds)
                TeensyButtonManager.Instance.SetLed(ConverterCorParaBotao(cor), true);

            TocarSomDaCor(cor);

            yield return new WaitForSeconds(tempoMostrar);

            txtPalavra.text = "";

            if (controlaLeds)
                TeensyButtonManager.Instance.SetLed(ConverterCorParaBotao(cor), false);

            yield return new WaitForSeconds(tempoEntre);
        }
    }

    // Toca o som daquela cor, se tiver algum clip arrastado no Inspector pra ela
    private void TocarSomDaCor(Cor cor)
    {
        if (sons.TryGetValue(cor, out AudioClip clip) && clip != null)
            audioSource.PlayOneShot(clip, volumeSomBotoes);
    }

    // Nunca deixa repetir a mesma cor da rodada anterior, pra sequência não empacar/repetir seguido
    private Cor ProximaCorAleatoria()
    {
        Cor cor;
        do
        {
            cor = (Cor)Random.Range(0, 4);
        } while (sequencia.Count > 0 && cor == sequencia[sequencia.Count - 1]);

        return cor;
    }

    // Em vez de mostrar "TRABALHO E REALIZAÇÃO" junto, reveza uma palavra por vez a cada aparição do vermelho
    private string ProximaPalavraVermelho()
    {
        string palavra = PalavrasVermelho[indiceVermelho];
        indiceVermelho = (indiceVermelho + 1) % PalavrasVermelho.Length;
        return palavra;
    }

    private IEnumerator EsperarInputDoJogador()
    {
        indiceEsperado = 0;
        aceitandoInput = true;
        tempoDesdeUltimoInput = 0f;

        while (aceitandoInput && indiceEsperado < sequencia.Count)
        {
            tempoDesdeUltimoInput += Time.deltaTime;

            if (tempoDesdeUltimoInput >= tempoLimiteInput)
            {
                aceitandoInput = false;
                StartCoroutine(ErroEIrParaFraseFinal());
                yield break;
            }

            yield return null;
        }
    }

    private void ReceberInput(Cor corDigitada)
    {
        if (corDigitada == sequencia[indiceEsperado])
        {
            acertos++;
            AtualizarPlacar();
            indiceEsperado++;
            tempoDesdeUltimoInput = 0f; // reinicia a contagem do tempo limite a cada acerto
        }
        else
        {
            aceitandoInput = false;
            StartCoroutine(ErroEIrParaFraseFinal());
        }
    }

    private void AtualizarPlacar() => DefinirTextoPlacar(acertos.ToString());

    // Se o texto (ex: "VAI!") for mais largo que a bola, encolhe até caber
    private void DefinirTextoPlacar(string texto)
    {
        txtPlacar.transform.localScale = Vector3.one;
        txtPlacar.text = texto;

        float largura = txtPlacar.GetComponent<MeshRenderer>().bounds.size.x;
        if (largura > larguraMaximaPlacar && largura > 0f)
        {
            float escala = larguraMaximaPlacar / largura;
            txtPlacar.transform.localScale = new Vector3(escala, escala, 1f);
        }
    }

    private IEnumerator ErroEIrParaFraseFinal()
    {
        bool controlaLeds = usarBotoesFisicos && TeensyButtonManager.Instance != null;

        if (somErro != null)
            audioSource.PlayOneShot(somErro);

        // Começa a escurecer a tela e faz os 4 botões piscarem ao mesmo tempo
        FadeController.Instance.FadeToBlack(duracaoFade);

        if (controlaLeds)
            yield return StartCoroutine(PiscarTodosOsBotoes(vezesPiscarErro, duracaoPiscaErro));

        // Garante que a tela já terminou de escurecer, caso o pisca termine antes do fade
        float duracaoPisca = controlaLeds ? vezesPiscarErro * duracaoPiscaErro * 2f : 0f;
        float tempoRestante = duracaoFade - duracaoPisca;
        if (tempoRestante > 0f)
            yield return new WaitForSeconds(tempoRestante);

        if (controlaLeds)
            TeensyButtonManager.Instance.ApagarTodosLeds();

        // Tela já está preta aqui, então só troca de cena e revela do outro lado
        FadeController.Instance.TrocarCena(cenaFraseFinal, 0f, duracaoFade);
    }

    // Pisca os 4 botões físicos ao mesmo tempo, "vezes" vezes (acende/apaga = 1 pisca)
    private IEnumerator PiscarTodosOsBotoes(int vezes, float duracaoMeioCiclo)
    {
        for (int i = 0; i < vezes; i++)
        {
            DefinirLedsTodos(true);
            yield return new WaitForSeconds(duracaoMeioCiclo);
            DefinirLedsTodos(false);
            yield return new WaitForSeconds(duracaoMeioCiclo);
        }
    }

    private void DefinirLedsTodos(bool ligado)
    {
        TeensyButtonManager.Instance.SetLed(CorBotao.Verde, ligado);
        TeensyButtonManager.Instance.SetLed(CorBotao.Vermelho, ligado);
        TeensyButtonManager.Instance.SetLed(CorBotao.Amarelo, ligado);
        TeensyButtonManager.Instance.SetLed(CorBotao.Azul, ligado);
    }

}