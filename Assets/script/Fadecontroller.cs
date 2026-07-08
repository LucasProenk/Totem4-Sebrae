using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
  Totem 04 — O Ritmo das Escolhas
  FadeController.cs

  Controlador único de fade pra tela toda (preto), que sobrevive à troca de
  cena (DontDestroyOnLoad). Não precisa arrastar em nenhum GameObject — se
  ainda não existir um na cena, ele se cria sozinho na primeira vez que
  alguma outra parte do código usar "FadeController.Instance".

  Usa um Canvas em Screen Space - Overlay, então cobre a tela inteira
  independente da câmera (inclusive com a câmera girada 90° do totem).

  Uso:
    FadeController.Instance.FadeToBlack(0.6f);           // escurece
    FadeController.Instance.FadeFromBlack(0.6f);         // revela
    FadeController.Instance.TrocarCena("cena", 0.6f, 0.6f); // escurece, troca de cena, revela
*/
public class FadeController : MonoBehaviour
{
    private static FadeController instancia;

    public static FadeController Instance
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("FadeController");
                instancia = go.AddComponent<FadeController>();
            }
            return instancia;
        }
    }

    private Image imagemFade;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        CriarOverlay();
    }

    private void CriarOverlay()
    {
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // sempre por cima de tudo

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imagemGO = new GameObject("FadeImage");
        imagemGO.transform.SetParent(canvasGO.transform, false);

        imagemFade = imagemGO.AddComponent<Image>();
        imagemFade.color = new Color(0f, 0f, 0f, 0f);
        imagemFade.raycastTarget = false; // não bloqueia clique/toque em nada

        RectTransform rt = imagemFade.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Escurece a tela até ficar preta. Pode ser usado junto com outra coisa acontecendo (ex: LEDs piscando).</summary>
    public Coroutine FadeToBlack(float duracao) => StartCoroutine(FadeParaAlvo(1f, duracao));

    /// <summary>Revela a tela (de preto até transparente).</summary>
    public Coroutine FadeFromBlack(float duracao) => StartCoroutine(FadeParaAlvo(0f, duracao));

    /// <summary>
    /// Escurece (se ainda não estiver preto), troca de cena, e revela a cena nova.
    /// Roda inteiro nesta instância persistente, entao funciona mesmo depois
    /// que o objeto que chamou for destruído pela troca de cena.
    /// </summary>
    public void TrocarCena(string nomeCena, float duracaoFadeOut, float duracaoFadeIn)
    {
        StartCoroutine(RotinaTrocarCena(nomeCena, duracaoFadeOut, duracaoFadeIn));
    }

    private IEnumerator RotinaTrocarCena(string nomeCena, float duracaoFadeOut, float duracaoFadeIn)
    {
        if (imagemFade.color.a < 0.99f)
            yield return FadeParaAlvo(1f, duracaoFadeOut);

        SceneManager.LoadScene(nomeCena);
        yield return null; // espera um frame pra cena nova terminar de carregar

        yield return FadeParaAlvo(0f, duracaoFadeIn);
    }

    private IEnumerator FadeParaAlvo(float alvoAlpha, float duracao)
    {
        float inicioAlpha = imagemFade.color.a;

        if (duracao <= 0f)
        {
            imagemFade.color = new Color(0f, 0f, 0f, alvoAlpha);
            yield break;
        }

        float tempoDecorrido = 0f;
        while (tempoDecorrido < duracao)
        {
            tempoDecorrido += Time.deltaTime;
            float alpha = Mathf.Lerp(inicioAlpha, alvoAlpha, tempoDecorrido / duracao);
            imagemFade.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        imagemFade.color = new Color(0f, 0f, 0f, alvoAlpha);
    }
}