using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Tooltip("Liga a escuta dos eventos do TeensyButtonManager. O LED acende sozinho no hardware ao apertar (feedback local do Teensy) — aqui só recebemos o clique pra validar a sequência do jogo.")]
    [SerializeField] private bool usarBotoesFisicos = true;

    [Header("Tempos (segundos)")]
    [SerializeField] private int duracaoContagem = 5;
    [SerializeField] private float tempoMostrandoCor = 0.8f;
    [SerializeField] private float tempoEntreCores = 0.35f;
    [SerializeField] private float pausaEntreRodadas = 0.8f;

    [Header("Velocidade progressiva (fica mais rápido a cada acerto)")]
    [SerializeField] private float reducaoPorAcerto = 0.02f;
    [SerializeField] private float tempoMinimoMostrandoCor = 0.3f;
    [SerializeField] private float tempoMinimoEntreCores = 0.15f;

    [Header("Fade ao errar")]
    [SerializeField] private float duracaoFade = 0.6f;
    [SerializeField] private Color corFade = Color.black;

    [Header("Posição da bola (unidades de mundo, iguais às do fundo/IconePlay — não é fração de tela)")]
    [Tooltip("Esse texto é criado em tempo de execução, então não dá pra arrastar no Scene view. Ajuste os valores aqui e aperte Play pra conferir. (0,0) é o centro da bola no meio do fundo.")]
    [SerializeField] private Vector2 posicaoPlacar = new Vector2(0f, 0f);

    [Header("Tamanho do texto (unidades de mundo)")]
    [SerializeField] private float tamanhoPlacar = 0.12f;
    [Tooltip("Largura máxima que o texto da bola pode ocupar (unidades de mundo) — se \"VAI!\" ou algo maior que um dígito não couber nesse espaço, ele encolhe automaticamente pra caber dentro da bola")]
    [SerializeField] private float larguraMaximaPlacar = 1.1f;

    [Header("Fonte da bola")]
    [Tooltip("Nome de uma fonte instalada no Windows (ex: Arial Black, Impact, Verdana, Consolas). Se não achar, usa a fonte padrão do Unity")]
    [SerializeField] private string nomeFonte = "Arial Black";

    [Header("Referências de cena")]
    [Tooltip("Arraste aqui o objeto de texto (TextMesh) que mostra a palavra da categoria embaixo. Assim dá pra posicionar ele arrastando no Scene view, do jeito que quiser.")]
    [SerializeField] private TextMesh txtPalavra;

    private static readonly string[] PalavrasVermelho = { "TRABALHO", "REALIZAÇÃO" };

    private Dictionary<KeyCode, Cor> teclas;
    private Dictionary<Cor, (string nome, Color cor)> categorias;
    private int indiceVermelho;

    private Font fonte;
    private TextMesh txtPlacar; // mostra a contagem regressiva no início e, depois, os acertos
    private SpriteRenderer overlayFade;

    private readonly List<Cor> sequencia = new List<Cor>();
    private int acertos;
    private int indiceEsperado;
    private bool aceitandoInput;

    private void Start()
    {
        if (txtPalavra == null)
            Debug.LogError("game: arraste o objeto de texto da palavra pro campo 'Txt Palavra' no Inspector.");

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

        if (usarBotoesFisicos)
        {
            if (TeensyButtonManager.Instance != null)
            {
                TeensyButtonManager.Instance.OnButtonDown += TratarBotaoFisico;
            }
            else
            {
                Debug.LogWarning("game: 'Usar Botoes Fisicos' está ligado, mas não achei um TeensyButtonManager na cena. Confirme se o GameObject com esse script existe e está ativo.");
            }
        }

        CriarUI();
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

        for (int i = duracaoContagem; i >= 1; i--)
        {
            DefinirTextoPlacar(i.ToString());
            yield return new WaitForSeconds(1f);
        }
        DefinirTextoPlacar("VAI!");
        yield return new WaitForSeconds(0.5f);

        txtPalavra.text = "";
    }

    private IEnumerator MostrarSequencia()
    {
        aceitandoInput = false;

        float reducao = acertos * reducaoPorAcerto;
        float tempoMostrar = Mathf.Max(tempoMinimoMostrandoCor, tempoMostrandoCor - reducao);
        float tempoEntre = Mathf.Max(tempoMinimoEntreCores, tempoEntreCores - reducao);

        foreach (var cor in sequencia)
        {
            var (nome, corCategoria) = categorias[cor];
            txtPalavra.text = cor == Cor.Vermelho ? ProximaPalavraVermelho() : nome;
            txtPalavra.color = corCategoria;

            yield return new WaitForSeconds(tempoMostrar);

            txtPalavra.text = "";
            yield return new WaitForSeconds(tempoEntre);
        }
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

        while (aceitandoInput && indiceEsperado < sequencia.Count)
            yield return null;
    }

    private void ReceberInput(Cor corDigitada)
    {
        if (corDigitada == sequencia[indiceEsperado])
        {
            acertos++;
            AtualizarPlacar();
            indiceEsperado++;
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
        float tempoDecorrido = 0f;
        while (tempoDecorrido < duracaoFade)
        {
            tempoDecorrido += Time.deltaTime;
            overlayFade.color = new Color(corFade.r, corFade.g, corFade.b, Mathf.Clamp01(tempoDecorrido / duracaoFade));
            yield return null;
        }
        overlayFade.color = corFade;

        SceneManager.LoadScene(cenaFraseFinal);
    }

    // Tudo aqui é criado em espaço de mundo comum (sem rotação própria), igual ao Background/IconePlay —
    // assim a câmera girada 90° (totem em pé) já deixa o texto na orientação certa junto com o resto da arte
    private void CriarUI()
    {
        fonte = Font.CreateDynamicFontFromOSFont(nomeFonte, 48);
        if (fonte == null)
            fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        txtPlacar = CriarTexto("Placar", posicaoPlacar, tamanhoPlacar, new Color(1f, 0.85f, 0.35f));
        CriarOverlayFade();
    }

    private TextMesh CriarTexto(string nomeObjeto, Vector2 posicaoMundo, float tamanhoCaractere, Color cor)
    {
        var go = new GameObject(nomeObjeto);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(posicaoMundo.x, posicaoMundo.y, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.font = fonte;
        tm.characterSize = tamanhoCaractere;
        tm.fontSize = 48;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = cor;
        tm.text = "";

        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder = 10;
        mr.material = tm.font.material;

        return tm;
    }

    // Retângulo do tamanho exato do que a câmera enxerga — cobre a tela inteira mesmo girada
    private void CriarOverlayFade()
    {
        var go = new GameObject("OverlayFade");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        overlayFade = go.AddComponent<SpriteRenderer>();
        overlayFade.sprite = CriarSpriteSolido();
        overlayFade.color = new Color(corFade.r, corFade.g, corFade.b, 0f);
        overlayFade.sortingOrder = 100;

        Camera cam = Camera.main;
        float largura = cam.orthographicSize * 2f;
        float altura = cam.orthographicSize * 2f * cam.aspect;
        go.transform.localScale = new Vector3(largura, altura, 1f);
    }

    private Sprite CriarSpriteSolido()
    {
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
    }
}