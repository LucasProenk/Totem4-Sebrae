using UnityEngine;

/*
  Totem 04 — O Ritmo das Escolhas
  CursorManager.cs

  Esconde o cursor do mouse em todas as cenas, do início ao fim da aplicação.
  Não precisa arrastar em nenhum GameObject — roda sozinho antes de qualquer
  cena carregar.
*/
public static class CursorManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EsconderCursor()
    {
        Cursor.visible = false;
    }
}
