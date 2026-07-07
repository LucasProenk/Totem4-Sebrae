using UnityEngine;

public class iconePlayPiscando : MonoBehaviour
{
    [Header("Tamanho (unidades do mundo)")]
    [Tooltip("A posição é a do próprio Transform deste objeto — arraste ele no Scene view até encaixar em cima do ícone de play")]
    [SerializeField] private float raio = 0.32f;

    [Header("Piscar")]
    [SerializeField] private Color cor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private float velocidadePiscar = 2.5f;
    [Range(0f, 1f)]
    [SerializeField] private float alphaMin = 0.15f;
    [Range(0f, 1f)]
    [SerializeField] private float alphaMax = 0.95f;
    [SerializeField] private float escalaMin = 0.9f;
    [SerializeField] private float escalaMax = 1.15f;

    private SpriteRenderer sr;

    private void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CriarSpriteBrilho();
        sr.color = cor;
        sr.sortingOrder = -4; // acima do fundo
    }

    private void Update()
    {
        float onda = (Mathf.Sin(Time.time * velocidadePiscar) + 1f) * 0.5f; // 0..1

        sr.color = new Color(cor.r, cor.g, cor.b, Mathf.Lerp(alphaMin, alphaMax, onda));

        float escala = Mathf.Lerp(escalaMin, escalaMax, onda) * raio * 2f;
        transform.localScale = new Vector3(escala, escala, 1f);
    }

    // Halo circular suave (sem depender de textura externa) que fica por cima do ícone de play
    private Sprite CriarSpriteBrilho()
    {
        const int tam = 64;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var centro = new Vector2(tam / 2f, tam / 2f);
        float raioMax = tam / 2f;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centro) / raioMax;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }
}
