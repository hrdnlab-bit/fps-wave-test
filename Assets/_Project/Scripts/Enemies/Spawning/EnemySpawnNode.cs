using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Nodo di spawn dei nemici: un punto a terra, di solito dietro un riparo. Non decide nulla da solo:
    /// <see cref="EnemyDirector"/> lo valuta a runtime (anello attorno ai player, non visibile, raggiungibile).
    /// Lo stato in gioco si vede nei gizmo del director (verde valido, rosso visibile, grigio irraggiungibile).
    /// </summary>
    public sealed class EnemySpawnNode : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.25f, 0.35f);
            Gizmos.DrawLine(p, p + Vector3.up * 1.8f);
        }
    }
}
