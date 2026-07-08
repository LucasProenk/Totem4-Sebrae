using UnityEngine;

/*
  Totem 04 — animação de fundo tipo "galáxia" pra tela final.
  Orbes espalhados pela tela inteira, cada um sendo "soprado" numa direção
  própria (reta, constante, com uma leve flutuação lateral) e saindo por um
  lado da tela pra reaparecer do outro, com um brilho que pisca devagar,
  usando as mesmas cores dos quadrantes da arte de fundo.

  Coloca esse script em um GameObject vazio na cena (ex: "GalaxiaBG").
*/
public class animacaofinal : MonoBehaviour
{
    [Header("Orbes")]
    [SerializeField] private int quantidade = 90;
    [SerializeField] private float tamanhoMin = 0.12f;
    [SerializeField] private float tamanhoMax = 0.4f;

    [Header("Brilho (piscando)")]
    [SerializeField] private float brilhoMin = 0.25f;
    [SerializeField] private float brilhoMax = 1f;
    [SerializeField] private float velocidadePiscarMin = 0.3f;
    [SerializeField] private float velocidadePiscarMax = 1.1f;

    [Header("Movimento (sopradas pelo vento, cada uma numa direção)")]
    [SerializeField] private float velocidadeDriftMin = 0.15f;
    [SerializeField] private float velocidadeDriftMax = 0.5f;
    [SerializeField] private float amplitudeOndulacaoMin = 0.05f;
    [SerializeField] private float amplitudeOndulacaoMax = 0.25f;
    [SerializeField] private float velocidadeOndulacaoMin = 0.3f;
    [SerializeField] private float velocidadeOndulacaoMax = 0.9f;

    [Header("Cores (mesmas dos quadrantes do fundo, mas mais vivas)")]
    [SerializeField] private Color corTopoEsquerda = new Color(0.10f, 0.65f, 1.00f);
    [SerializeField] private Color corTopoDireita = new Color(1.00f, 0.82f, 0.10f);
    [SerializeField] private Color corBaixoEsquerda = new Color(0.30f, 1.00f, 0.35f);
    [SerializeField] private Color corBaixoDireita = new Color(1.00f, 0.20f, 0.15f);
    [Range(0f, 1f)]
    [SerializeField] private float chanceBranco = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float saturacaoMinima = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float brilhoMinimoCor = 0.95f;

    private class Orbe
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos;
        public Vector2 direcao;
        public float velocidade;
        public float faseOndulacao;
        public float freqOndulacao;
        public float amplitudeOndulacao;
        public bool ehBranco;
        public float fasePiscar;
        public float velocidadePiscar;
    }

    private Orbe[] orbes;
    private Sprite spriteOrbe;
    private float largura;
    private float altura;

    private void Start()
    {
        spriteOrbe = CriarSpriteOrbe();
        CalcularAreaDaCamera();

        orbes = new Orbe[quantidade];
        for (int i = 0; i < quantidade; i++)
            orbes[i] = CriarOrbe();
    }

    private void Update()
    {
        float t = Time.time;

        foreach (var o in orbes)
        {
            o.pos += o.direcao * o.velocidade * Time.deltaTime;
            EnrolarNaBorda(ref o.pos);

            // flutuação leve pro lado, perpendicular à direção do vento — dá o efeito de
            // "sendo soprado" em vez de deslizar reto que nem trem
            Vector2 perpendicular = new Vector2(-o.direcao.y, o.direcao.x);
            float ondulacao = Mathf.Sin(t * o.freqOndulacao + o.faseOndulacao) * o.amplitudeOndulacao;
            Vector2 posVisual = o.pos + perpendicular * ondulacao;
            o.t.localPosition = new Vector3(posVisual.x, posVisual.y, 0f);

            Color corBase = o.ehBranco ? Color.white : CorPorPosicao(o.pos);

            // curva suave de "respirar": sobe o brilho e desce a intensidade, sem ficar linear
            float piscarLinear = (Mathf.Sin(t * o.velocidadePiscar + o.fasePiscar) + 1f) * 0.5f;
            float piscar = Mathf.SmoothStep(0f, 1f, piscarLinear);
            float alpha = Mathf.Lerp(brilhoMin, brilhoMax, piscar);
            o.sr.color = new Color(corBase.r, corBase.g, corBase.b, alpha);
        }
    }

    // Espaço de mundo comum, igual ao overlay do telaFraseFinal — cobre a tela toda mesmo
    // com a câmera girada 90° pro totem em pé
    private void CalcularAreaDaCamera()
    {
        Camera cam = Camera.main;
        largura = cam.orthographicSize * 2f;
        altura = cam.orthographicSize * 2f * cam.aspect;
    }

    // Quando o orbe sai de um lado da tela, reaparece do lado oposto — assim o vento nunca para
    private void EnrolarNaBorda(ref Vector2 pos)
    {
        float metadeLargura = largura / 2f;
        float metadeAltura = altura / 2f;

        if (pos.x > metadeLargura) pos.x -= largura;
        else if (pos.x < -metadeLargura) pos.x += largura;

        if (pos.y > metadeAltura) pos.y -= altura;
        else if (pos.y < -metadeAltura) pos.y += altura;
    }

    private Orbe CriarOrbe()
    {
        var go = new GameObject("orbe");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spriteOrbe;
        sr.sortingOrder = -5;

        var o = new Orbe { t = go.transform, sr = sr };

        o.pos = new Vector2(
            Random.Range(-largura / 2f, largura / 2f),
            Random.Range(-altura / 2f, altura / 2f));

        // cada orbe ganha sua própria direção de vento, fixa pra vida toda dela
        float angulo = Random.Range(0f, Mathf.PI * 2f);
        o.direcao = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));
        o.velocidade = Random.Range(velocidadeDriftMin, velocidadeDriftMax);

        o.amplitudeOndulacao = Random.Range(amplitudeOndulacaoMin, amplitudeOndulacaoMax);
        o.freqOndulacao = Random.Range(velocidadeOndulacaoMin, velocidadeOndulacaoMax);
        o.faseOndulacao = Random.Range(0f, Mathf.PI * 2f);

        o.velocidadePiscar = Random.Range(velocidadePiscarMin, velocidadePiscarMax);
        o.fasePiscar = Random.Range(0f, Mathf.PI * 2f);

        o.ehBranco = Random.value < chanceBranco;

        float tamanho = Random.Range(tamanhoMin, tamanhoMax);
        o.t.localScale = new Vector3(tamanho, tamanho, 1f);

        return o;
    }

    // Mistura as 4 cores dos cantos de acordo com a posição do orbe, pra combinar com o degradê do fundo
    private Color CorPorPosicao(Vector2 pos)
    {
        float tx = Mathf.InverseLerp(-largura / 2f, largura / 2f, pos.x);
        float ty = Mathf.InverseLerp(-altura / 2f, altura / 2f, pos.y);

        Color baixo = Color.Lerp(corBaixoEsquerda, corBaixoDireita, tx);
        Color topo = Color.Lerp(corTopoEsquerda, corTopoDireita, tx);
        Color misturada = Color.Lerp(baixo, topo, ty);

        return Vivificar(misturada);
    }

    // O lerp entre cores diferentes lava a saturação (fica meio pastel); aqui a gente garante um
    // mínimo de saturação e brilho pra cor sair mais viva/neon, mesmo depois da mistura
    private Color Vivificar(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        s = Mathf.Max(s, saturacaoMinima);
        v = Mathf.Max(v, brilhoMinimoCor);
        return Color.HSVToRGB(h, s, v);
    }

    // Sprite de orbe: núcleo com brilho suave ao redor, sem raios (diferente da estrela do menu) —
    // mais parecido com uma partícula de nebulosa/galáxia
    private Sprite CriarSpriteOrbe()
    {
        const int tam = 64;
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

                float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }
}
