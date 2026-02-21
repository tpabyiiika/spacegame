using UnityEngine;

public class Portal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Tile"))
            return;

        if (GameSessionDirector.AdvanceMapViaPortal())
            return;

        // Ignore extra tile triggers immediately after a successful map advance.
        if (GameSessionDirector.IsPortalRepeatGraceActive())
            return;

        if (GameSessionDirector.IsPortalWinLocked())
            return;

        GameManager.Instance.ToEnd(true);
    }
}
