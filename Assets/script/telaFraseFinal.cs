using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class telaFraseFinal : MonoBehaviour
{
    [Header("Cena para onde volta")]
    [SerializeField] private string cenaHome = "home";

    [Header("Tempos (segundos)")]
    [SerializeField] private float duracaoExibicao = 10f;
    [SerializeField] private float duracaoFade = 0.6f;
    [SerializeField] private Color corFade = Color.black;

    private SpriteRenderer overlayFade;

    private void Start()
    {
        CriarOverlayFade();
        StartCoroutine(EsperarEVoltarParaHome());
    }

    private IEnumerator EsperarEVoltarParaHome()
    {
        yield return new WaitForSeconds(duracaoExibicao);

        float tempoDecorrido = 0f;
        while (tempoDecorrido < duracaoFade)
        {
            tempoDecorrido += Time.deltaTime;
            overlayFade.color = new Color(corFade.r, corFade.g, corFade.b, Mathf.Clamp01(tempoDecorrido / duracaoFade));
            yield return null;
        }
        overlayFade.color = corFade;

        SceneManager.LoadScene(cenaHome);
    }

    // Espaço de mundo comum (sem rotação própria), igual ao Background/IconePlay — assim a câmera
    // girada 90° (totem em pé) cobre a tela toda certinho, sem precisar de Canvas nenhum
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
