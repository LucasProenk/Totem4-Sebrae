using UnityEngine;

public class animaçaobg : MonoBehaviour
{
    [Header("Área do mapa (unidades do mundo)")]
    [SerializeField] private float largura = 5.6f;
    [SerializeField] private float altura = 10f;

    [Header("Ponto de nascimento")]
    [SerializeField] private float origemAlturaY = 0.6f; // desloca o nascimento pra cima do centro do mapa
    [SerializeField] private float variacaoOrigem = 0.2f; // jitter ao redor do ponto de nascimento

    [Header("Partículas")]
    [SerializeField] private int quantidade = 50;
    [SerializeField] private float velocidadeMin = 0.8f;
    [SerializeField] private float velocidadeMax = 1.8f;
    [SerializeField] private float tamanhoMin = 0.0006f;
    [SerializeField] private float tamanhoMax = 0.0015f;
    [Range(0f, 1f)]
    [SerializeField] private float espalhamentoCanto = 0.5f; // 0 = mira exatamente no canto, 1 = espalha por todo o quadrante
    [SerializeField] private float espalhamentoLateral = 0.6f; // o quanto ela "abre" pros lados enquanto viaja

    [Header("Obstáculo")]
    [Tooltip("Arraste aqui GameObjects vazios com Circle Collider 2D — as partículas não vão atravessar, vão deslizar pela borda. Pode arrastar quantos precisar")]
    [SerializeField] private CircleCollider2D[] obstaculos;
    [Tooltip("Arraste aqui GameObjects vazios com Box Collider 2D — mesma função do de cima, mas retangular. Pode arrastar quantos precisar")]
    [SerializeField] private BoxCollider2D[] obstaculosBox;

    // Um canto por quadrante, com a cor do círculo correspondente na arte
    private struct Canto 
    {
        public Vector2 posicao;
        public Color cor;
        public Canto(Vector2 posicao, Color cor) { this.posicao = posicao; this.cor = cor; }
    }

    private Canto[] cantos;

    private class Particula
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 origem;
        public Vector2 destino;
        public Color corBase;
        public float velocidade;
        public float vida;
        public float faseOndulacao;
        public float freqOndulacao;
        public float ladoOndulacao;
    }

    private Particula[] particulas;
    private Sprite spriteBrilho;

    private void Start()
    {
        spriteBrilho = CriarSpriteBrilho();

        cantos = new[]
        {
            new Canto(new Vector2(-largura / 2f,  altura / 2f), new Color(0.10f, 0.65f, 1.00f)), // topo-esquerda: descanso
            new Canto(new Vector2( largura / 2f,  altura / 2f), new Color(1.00f, 0.82f, 0.10f)), // topo-direita: aprendizado
            new Canto(new Vector2(-largura / 2f, -altura / 2f), new Color(0.30f, 1.00f, 0.35f)), // baixo-esquerda: relacionamentos
            new Canto(new Vector2( largura / 2f, -altura / 2f), new Color(1.00f, 0.20f, 0.15f)), // baixo-direita: trabalho
        };

        particulas = new Particula[quantidade];
        for (int i = 0; i < quantidade; i++)
            particulas[i] = CriarParticula();
    }

    private void Update()
    {
        foreach (var p in particulas)
        {
            p.vida += Time.deltaTime;

            float distancia = Vector2.Distance(p.origem, p.destino);
            float progresso = Mathf.Clamp01(p.vida / (distancia / p.velocidade));

            // ease-out: sai rápido (como se fosse jogada) e vai perdendo força até se espalhar no canto
            float suave = 1f - Mathf.Pow(1f - progresso, 3f);

            Vector2 pos = Vector2.Lerp(p.origem, p.destino, suave);

            // abertura lateral que CRESCE com o progresso, dando a sensação de "espalhar" à medida que viaja
            Vector2 dir = (p.destino - p.origem).normalized;
            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            float abertura = progresso * espalhamentoLateral * p.ladoOndulacao;
            float oscilacao = Mathf.Sin(p.vida * p.freqOndulacao + p.faseOndulacao);
            pos += perpendicular * (abertura + oscilacao * abertura * 0.3f);

            foreach (var circulo in obstaculos)
                pos = DesviarDoObstaculo(pos, circulo);
            foreach (var box in obstaculosBox)
                pos = DesviarDoObstaculoBox(pos, box);

            p.t.localPosition = new Vector3(pos.x, pos.y, 0f);

            // some sutil ao nascer no centro e ao chegar no canto
            float alpha = Mathf.Sin(progresso * Mathf.PI);
            p.sr.color = new Color(p.corBase.r, p.corBase.g, p.corBase.b, alpha);

            if (progresso >= 1f)
                ReiniciarParticula(p);
        }
    }

    // Se a posição cair dentro do obstáculo, empurra pra borda do círculo — como o resto do
    // trajeto continua avançando, o efeito visual é de deslizar pela lateral e seguir o caminho
    private Vector2 DesviarDoObstaculo(Vector2 pos, CircleCollider2D circulo)
    {
        if (circulo == null)
            return pos;

        Vector2 centro = circulo.transform.position;
        float raio = circulo.radius * Mathf.Max(circulo.transform.lossyScale.x, circulo.transform.lossyScale.y);

        Vector2 delta = pos - centro;
        float dist = delta.magnitude;
        if (dist >= raio)
            return pos;

        Vector2 direcaoParaFora = dist > 0.0001f ? delta / dist : Vector2.up;
        return centro + direcaoParaFora * raio;
    }

    // Mesma ideia do obstáculo circular, mas empurrando pra fora pela borda mais próxima do retângulo
    private Vector2 DesviarDoObstaculoBox(Vector2 pos, BoxCollider2D box)
    {
        if (box == null)
            return pos;

        Bounds b = box.bounds;
        if (!b.Contains(new Vector3(pos.x, pos.y, b.center.z)))
            return pos;

        float distEsquerda = pos.x - b.min.x;
        float distDireita = b.max.x - pos.x;
        float distBaixo = pos.y - b.min.y;
        float distCima = b.max.y - pos.y;
        float menor = Mathf.Min(Mathf.Min(distEsquerda, distDireita), Mathf.Min(distBaixo, distCima));

        if (menor == distEsquerda) return new Vector2(b.min.x, pos.y);
        if (menor == distDireita) return new Vector2(b.max.x, pos.y);
        if (menor == distBaixo) return new Vector2(pos.x, b.min.y);
        return new Vector2(pos.x, b.max.y);
    }

    private Particula CriarParticula()
    {
        var go = new GameObject("particula");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spriteBrilho;
        sr.sortingOrder = -5;

        var p = new Particula { t = go.transform, sr = sr };
        ConfigurarParticula(p, iniciarNoMeioDoCaminho: true);
        return p;
    }

    private void ReiniciarParticula(Particula p) => ConfigurarParticula(p, iniciarNoMeioDoCaminho: false);

    private void ConfigurarParticula(Particula p, bool iniciarNoMeioDoCaminho)
    {
        Canto canto = cantos[Random.Range(0, cantos.Length)];

        // nasce perto de um ponto acima do centro do mapa, com um pequeno jitter
        p.origem = new Vector2(
            Random.Range(-variacaoOrigem, variacaoOrigem),
            origemAlturaY + Random.Range(-variacaoOrigem, variacaoOrigem));

        // chega perto do canto escolhido, com espalhamento pra dentro do quadrante
        float espalhoX = Random.Range(0f, largura / 2f * espalhamentoCanto) * -Mathf.Sign(canto.posicao.x);
        float espalhoY = Random.Range(0f, altura / 2f * espalhamentoCanto) * -Mathf.Sign(canto.posicao.y);
        p.destino = canto.posicao + new Vector2(espalhoX, espalhoY);

        p.velocidade = Random.Range(velocidadeMin, velocidadeMax);
        p.faseOndulacao = Random.Range(0f, Mathf.PI * 2f);
        p.freqOndulacao = Random.Range(1.5f, 3f);
        p.ladoOndulacao = Random.value < 0.5f ? -1f : 1f;

        float distancia = Vector2.Distance(p.origem, p.destino);
        p.vida = iniciarNoMeioDoCaminho ? Random.Range(0f, distancia / p.velocidade) : 0f;

        float tamanho = Random.Range(tamanhoMin, tamanhoMax);
        p.t.localScale = new Vector3(tamanho, tamanho, 1f);

        p.corBase = Random.value < 0.15f ? Color.white : canto.cor;
        p.sr.color = new Color(p.corBase.r, p.corBase.g, p.corBase.b, 0f);
    }

    // Gera um sprite de bolinha simples (círculo com borda suave), sem depender de textura externa
    private Sprite CriarSpriteBrilho()
    {
        const int tam = 96;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        var centro = new Vector2(tam / 2f, tam / 2f);
        float raioMax = tam / 2f;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float nx = (x - centro.x) / raioMax;
                float ny = (y - centro.y) / raioMax;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                float alpha = Mathf.Clamp01(1f - dist * 1.05f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }
}
